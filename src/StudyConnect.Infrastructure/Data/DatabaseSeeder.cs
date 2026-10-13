using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyConnect.Core.Common;
using StudyConnect.Core.Entities;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Extensions;
using StudyConnect.Core.Matching;
using StudyConnect.Core.Security;

namespace StudyConnect.Infrastructure.Data;

/// <summary>
/// Reads demo data from JSON files and inserts it into the database.
/// </summary>
public sealed class DatabaseSeeder
{
    /// <summary>Password of every demo account (satisfies the password policy).</summary>
    public const string DemoPassword = "Demo@1234";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly StudyConnectDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly string _seedDirectory;
    private readonly ILogger _logger;

    public DatabaseSeeder(StudyConnectDbContext db, IPasswordHasher hasher, string seedDirectory, ILogger logger)
    {
        _db = db;
        _hasher = hasher;
        _seedDirectory = seedDirectory;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_seedDirectory))
        {
            _logger.LogWarning("Seed directory {Directory} not found - starting with an empty database.", _seedDirectory);
            return;
        }

        // Hash the demo password once and reuse it - hashing is deliberately slow.
        string demoHash = _hasher.Hash(DemoPassword);

        Dictionary<string, User> users = await SeedUsersAsync(demoHash, cancellationToken);
        Dictionary<string, Subject> subjects = await SeedSubjectsAsync(cancellationToken);
        await SeedEnrollmentsAsync(users, subjects, cancellationToken);
        await SeedTeamsAsync(users, subjects, cancellationToken);
        await SeedSessionsAsync(users, subjects, cancellationToken);
        await SeedConnectionsAsync(users, cancellationToken);
        await SeedMessagesAsync(users, cancellationToken);
        await SeedWelcomeNotificationsAsync(users.Values, cancellationToken);

        _logger.LogInformation("Seeded {Users} demo users. Log in with alice@uts.edu.au / {Password}", users.Count, DemoPassword);
    }

    private async Task<List<T>> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_seedDirectory, fileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning("Seed file {File} is missing - skipped.", path);
            return new List<T>();
        }

        await using FileStream stream = File.OpenRead(path);
        List<T>? items = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken);
        return items ?? new List<T>();
    }

    private async Task<Dictionary<string, User>> SeedUsersAsync(string demoHash, CancellationToken cancellationToken)
    {
        List<SeedUser> seedUsers = await ReadAsync<SeedUser>("users.json", cancellationToken);
        var users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        foreach (SeedUser seed in seedUsers)
        {
            var user = new User
            {
                Name = seed.Name,
                Email = seed.Email.Trim().ToLowerInvariant(),
                PasswordHash = demoHash,
                ContactNumber = seed.ContactNumber,
                Degree = seed.Degree,
                Bio = string.IsNullOrWhiteSpace(seed.Bio) ? null : seed.Bio,
                ProfileCompleted = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
                LastSeenUtc = DateTime.UtcNow
            };

            foreach (string interest in seed.Interests.Select(i => i.NormalizeTag()).Where(i => i.Length > 0).Distinct())
            {
                user.Interests.Add(new UserInterest { Name = interest });
            }

            foreach (string text in seed.Availability.Distinct())
            {
                if (AvailabilitySlot.TryParse(text, out AvailabilitySlot slot))
                {
                    user.Availability.Add(new UserAvailability { Day = slot.Day, Block = slot.Block });
                }
            }

            users[user.Email] = user;
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return users;
    }

    private async Task<Dictionary<string, Subject>> SeedSubjectsAsync(CancellationToken cancellationToken)
    {
        List<SeedSubject> seedSubjects = await ReadAsync<SeedSubject>("subjects.json", cancellationToken);
        var subjects = new Dictionary<string, Subject>(StringComparer.OrdinalIgnoreCase);

        foreach (SeedSubject seed in seedSubjects)
        {
            var subject = new Subject
            {
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                MinTeamSize = seed.MinTeamSize,
                MaxTeamSize = seed.MaxTeamSize
            };

            foreach (SeedLab lab in seed.Labs)
            {
                subject.Labs.Add(new LabSlot
                {
                    Code = lab.Code,
                    Day = Enum.Parse<DayOfWeek>(lab.Day, ignoreCase: true),
                    StartTime = TimeOnly.ParseExact(lab.Start, "HH:mm", CultureInfo.InvariantCulture),
                    EndTime = TimeOnly.ParseExact(lab.End, "HH:mm", CultureInfo.InvariantCulture),
                    Room = lab.Room,
                    Tutor = lab.Tutor
                });
            }

            subjects[subject.Code] = subject;
            _db.Subjects.Add(subject);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return subjects;
    }

    private async Task SeedEnrollmentsAsync(Dictionary<string, User> users, Dictionary<string, Subject> subjects, CancellationToken cancellationToken)
    {
        foreach (SeedEnrollment seed in await ReadAsync<SeedEnrollment>("enrollments.json", cancellationToken))
        {
            if (!users.TryGetValue(seed.Email, out User? user) || !subjects.TryGetValue(seed.SubjectCode, out Subject? subject))
            {
                continue;
            }

            LabSlot? lab = subject.Labs.FirstOrDefault(l => l.Code.Equals(seed.LabCode, StringComparison.OrdinalIgnoreCase));
            if (lab is null)
            {
                continue;
            }

            _db.Enrollments.Add(new Enrollment
            {
                UserId = user.Id,
                SubjectId = subject.Id,
                LabSlotId = lab.Id,
                PreferredRole = seed.Role,
                Skill = seed.Skill,
                Goal = seed.Goal,
                LookingForTeam = seed.LookingForTeam,
                Pitch = string.IsNullOrWhiteSpace(seed.Pitch) ? null : seed.Pitch
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTeamsAsync(Dictionary<string, User> users, Dictionary<string, Subject> subjects, CancellationToken cancellationToken)
    {
        foreach (SeedTeam seed in await ReadAsync<SeedTeam>("teams.json", cancellationToken))
        {
            if (!users.TryGetValue(seed.LeaderEmail, out User? leader) || !subjects.TryGetValue(seed.SubjectCode, out Subject? subject))
            {
                continue;
            }

            Enrollment? leaderEnrollment = await _db.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == leader.Id && e.SubjectId == subject.Id, cancellationToken);
            if (leaderEnrollment is null)
            {
                continue;
            }

            var team = new Team
            {
                SubjectId = subject.Id,
                LabSlotId = leaderEnrollment.LabSlotId,
                Name = seed.Name,
                ProjectIdea = seed.ProjectIdea,
                MaxMembers = seed.MaxMembers,
                LeaderId = leader.Id,
                IsOpen = seed.IsOpen
            };

            IEnumerable<string> memberEmails = new[] { seed.LeaderEmail }.Concat(seed.MemberEmails);
            foreach (string email in memberEmails)
            {
                if (!users.TryGetValue(email, out User? member))
                {
                    continue;
                }

                Enrollment? enrollment = await _db.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == member.Id && e.SubjectId == subject.Id, cancellationToken);
                if (enrollment is null)
                {
                    continue;
                }

                enrollment.LookingForTeam = false;
                team.Members.Add(new TeamMember { UserId = member.Id, Role = enrollment.PreferredRole });
            }

            foreach (string email in seed.InviteEmails)
            {
                if (users.TryGetValue(email, out User? invitee))
                {
                    team.Invitations.Add(new TeamInvitation
                    {
                        UserId = invitee.Id,
                        Kind = InvitationKind.Invite,
                        Message = $"Hi {invitee.FirstName}! We think you'd be a great fit for {seed.Name}."
                    });
                }
            }

            _db.Teams.Add(team);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSessionsAsync(Dictionary<string, User> users, Dictionary<string, Subject> subjects, CancellationToken cancellationToken)
    {
        foreach (SeedSession seed in await ReadAsync<SeedSession>("sessions.json", cancellationToken))
        {
            if (!users.TryGetValue(seed.CreatorEmail, out User? creator))
            {
                continue;
            }

            Subject? subject = seed.SubjectCode is null ? null : subjects.GetValueOrDefault(seed.SubjectCode);

            var session = new StudySession
            {
                Title = seed.Title,
                Topic = seed.Topic,
                StartsAt = DateTime.Today.AddDays(seed.DaysFromNow).AddHours(seed.Hour),
                DurationMinutes = seed.DurationMinutes,
                Location = seed.Location,
                Mode = seed.Mode,
                Capacity = seed.Capacity,
                CreatorId = creator.Id,
                SubjectId = subject?.Id
            };

            session.Attendees.Add(new SessionAttendee { UserId = creator.Id });
            foreach (string email in seed.AttendeeEmails)
            {
                if (users.TryGetValue(email, out User? attendee) && attendee.Id != creator.Id)
                {
                    session.Attendees.Add(new SessionAttendee { UserId = attendee.Id });
                }
            }

            _db.StudySessions.Add(session);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedConnectionsAsync(Dictionary<string, User> users, CancellationToken cancellationToken)
    {
        foreach (SeedConnection seed in await ReadAsync<SeedConnection>("connections.json", cancellationToken))
        {
            if (users.TryGetValue(seed.From, out User? from) && users.TryGetValue(seed.To, out User? to))
            {
                _db.Connections.Add(new Connection
                {
                    RequesterId = from.Id,
                    AddresseeId = to.Id,
                    Status = seed.Status,
                    RespondedAtUtc = seed.Status == ConnectionStatus.Pending ? null : DateTime.UtcNow.AddDays(-3)
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMessagesAsync(Dictionary<string, User> users, CancellationToken cancellationToken)
    {
        foreach (SeedMessage seed in await ReadAsync<SeedMessage>("messages.json", cancellationToken))
        {
            if (users.TryGetValue(seed.From, out User? from) && users.TryGetValue(seed.To, out User? to))
            {
                _db.DirectMessages.Add(new DirectMessage
                {
                    SenderId = from.Id,
                    RecipientId = to.Id,
                    ConversationKey = ConversationKey.For(from.Id, to.Id),
                    Text = seed.Text,
                    IsRead = seed.Read,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-seed.MinutesAgo)
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedWelcomeNotificationsAsync(IEnumerable<User> users, CancellationToken cancellationToken)
    {
        foreach (User user in users)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = NotificationType.System,
                Title = "Welcome to StudyConnect 👋",
                Body = "Enrol in your subjects and open Team Finder to meet team mates from your lab.",
                Link = "/team-finder"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ----- JSON shapes of the seed files -----

    private sealed class SeedUser
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public string? Degree { get; set; }
        public string? Bio { get; set; }
        public List<string> Interests { get; set; } = new();
        public List<string> Availability { get; set; } = new();
    }

    private sealed class SeedSubject
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int MinTeamSize { get; set; } = 2;
        public int MaxTeamSize { get; set; } = 3;
        public List<SeedLab> Labs { get; set; } = new();
    }

    private sealed class SeedLab
    {
        public string Code { get; set; } = string.Empty;
        public string Day { get; set; } = "Monday";
        public string Start { get; set; } = "09:00";
        public string End { get; set; } = "11:00";
        public string Room { get; set; } = string.Empty;
        public string Tutor { get; set; } = string.Empty;
    }

    private sealed class SeedEnrollment
    {
        public string Email { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string LabCode { get; set; } = string.Empty;
        public TeamRole Role { get; set; }
        public SkillLevel Skill { get; set; }
        public TargetGrade Goal { get; set; }
        public bool LookingForTeam { get; set; } = true;
        public string? Pitch { get; set; }
    }

    private sealed class SeedTeam
    {
        public string SubjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ProjectIdea { get; set; }
        public string LeaderEmail { get; set; } = string.Empty;
        public List<string> MemberEmails { get; set; } = new();
        public List<string> InviteEmails { get; set; } = new();
        public int MaxMembers { get; set; } = 3;
        public bool IsOpen { get; set; } = true;
    }

    private sealed class SeedSession
    {
        public string Title { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int DaysFromNow { get; set; }
        public int Hour { get; set; } = 10;
        public int DurationMinutes { get; set; } = 60;
        public string Location { get; set; } = "Online";
        public SessionMode Mode { get; set; }
        public int Capacity { get; set; } = 10;
        public string CreatorEmail { get; set; } = string.Empty;
        public string? SubjectCode { get; set; }
        public List<string> AttendeeEmails { get; set; } = new();
    }

    private sealed class SeedConnection
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public ConnectionStatus Status { get; set; }
    }

    private sealed class SeedMessage
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int MinutesAgo { get; set; }
        public bool Read { get; set; }
    }
}
