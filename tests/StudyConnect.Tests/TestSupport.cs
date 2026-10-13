using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Entities;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Matching;
using StudyConnect.Core.Models;
using StudyConnect.Core.Security;
using StudyConnect.Core.Validation;
using StudyConnect.Infrastructure.Data;
using StudyConnect.Infrastructure.Realtime;
using StudyConnect.Infrastructure.Services;

namespace StudyConnect.Tests;

/// <summary>
/// A real SQLite database that lives in memory for the duration of one test.
/// Using SQLite (instead of a mock) means the EF Core mappings and LINQ queries are exercised for real.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<StudyConnectDbContext>()
            .UseSqlite(_connection)
            .Options;

        using StudyConnectDbContext db = CreateContext();
        db.Database.EnsureCreated();

        Factory = new TestDbContextFactory(Options);
    }

    public DbContextOptions<StudyConnectDbContext> Options { get; }

    public IDbContextFactory<StudyConnectDbContext> Factory { get; }

    public StudyConnectDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();

    private sealed class TestDbContextFactory : IDbContextFactory<StudyConnectDbContext>
    {
        private readonly DbContextOptions<StudyConnectDbContext> _options;

        public TestDbContextFactory(DbContextOptions<StudyConnectDbContext> options) => _options = options;

        public StudyConnectDbContext CreateDbContext() => new(_options);
    }
}

/// <summary>E-mail sender that records messages instead of sending them.</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = new();

    public bool DeliversExternally => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Builds services and demo data for the integration tests.</summary>
internal sealed class ServiceHarness : IDisposable
{
    public ServiceHarness()
    {
        Database = new TestDatabase();
        Realtime = new InMemoryRealtimeNotifier(NullLogger<InMemoryRealtimeNotifier>.Instance);
        Hasher = new Pbkdf2PasswordHasher(1_000); // fast hashing for tests
        Email = new CapturingEmailSender();
        Notifications = new NotificationService(Database.Factory, Realtime);
        Auth = new AuthService(Database.Factory, Hasher, new PasswordPolicy(), Email, NullLogger<AuthService>.Instance);
        Connections = new ConnectionService(Database.Factory, Notifications, Realtime, NullLogger<ConnectionService>.Instance);
        Messages = new MessageService(Database.Factory, Realtime);
        Teams = new TeamService(Database.Factory, Notifications, Realtime, NullLogger<TeamService>.Instance);
        Subjects = new SubjectService(Database.Factory);
        var engine = new CompatibilityEngine();
        Matchmaking = new MatchmakingService(Database.Factory, engine, new TeamBuilder(engine), Connections);
    }

    public TestDatabase Database { get; }

    public InMemoryRealtimeNotifier Realtime { get; }

    public IPasswordHasher Hasher { get; }

    public CapturingEmailSender Email { get; }

    public NotificationService Notifications { get; }

    public AuthService Auth { get; }

    public ConnectionService Connections { get; }

    public MessageService Messages { get; }

    public TeamService Teams { get; }

    public SubjectService Subjects { get; }

    public MatchmakingService Matchmaking { get; }

    /// <summary>Creates a subject with two labs and returns (subjectId, lab1Id, lab2Id).</summary>
    public (int SubjectId, int Lab1, int Lab2) CreateSubject(int maxTeamSize = 3)
    {
        using StudyConnectDbContext db = Database.CreateContext();
        var subject = new Subject { Code = "31927", Name = "Applications Development with .NET", MinTeamSize = 2, MaxTeamSize = maxTeamSize };
        subject.Labs.Add(new LabSlot { Code = "Lab 01", Day = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0), Room = "CB11", Tutor = "T1" });
        subject.Labs.Add(new LabSlot { Code = "Lab 02", Day = DayOfWeek.Monday, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(15, 0), Room = "CB11", Tutor = "T2" });
        db.Subjects.Add(subject);
        db.SaveChanges();

        List<LabSlot> labs = subject.Labs.OrderBy(l => l.Code).ToList();
        return (subject.Id, labs[0].Id, labs[1].Id);
    }

    /// <summary>Creates a student directly in the database, optionally enrolled in a lab.</summary>
    public int CreateStudent(string name, string[]? interests = null, int? subjectId = null, int? labId = null, TeamRole role = TeamRole.FullStack)
    {
        using StudyConnectDbContext db = Database.CreateContext();
        var user = new User
        {
            Name = name,
            Email = $"{name.Replace(" ", ".").ToLowerInvariant()}@test.edu",
            PasswordHash = Hasher.Hash("Demo@1234"),
            ProfileCompleted = true
        };

        foreach (string interest in interests ?? new[] { "c#" })
        {
            user.Interests.Add(new UserInterest { Name = interest });
        }

        user.Availability.Add(new UserAvailability { Day = DayOfWeek.Monday, Block = TimeBlock.Afternoon });
        db.Users.Add(user);
        db.SaveChanges();

        if (subjectId is int sid && labId is int lid)
        {
            db.Enrollments.Add(new Enrollment { UserId = user.Id, SubjectId = sid, LabSlotId = lid, PreferredRole = role });
            db.SaveChanges();
        }

        return user.Id;
    }

    public void Dispose() => Database.Dispose();
}
