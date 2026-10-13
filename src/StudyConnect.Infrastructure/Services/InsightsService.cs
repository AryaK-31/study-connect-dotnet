using StudyConnect.Core.MachineLearning;

namespace StudyConnect.Infrastructure.Services;

/// <summary>
/// Data analytics for the Insights screen: aggregated statistics computed with LINQ and
/// study communities discovered with the K-Means machine-learning algorithm.
/// </summary>
public sealed class InsightsService : IInsightsService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly IRealtimeNotifier _realtime;

    public InsightsService(IDbContextFactory<StudyConnectDbContext> dbFactory, IRealtimeNotifier realtime)
    {
        _dbFactory = dbFactory;
        _realtime = realtime;
    }

    public async Task<InsightsReport> GetReportAsync(int? subjectId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<User> users = await db.Users
            .AsNoTracking()
            .Include(u => u.Interests)
            .Include(u => u.Availability)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        IQueryable<Enrollment> enrollmentQuery = db.Enrollments.AsNoTracking().Include(e => e.LabSlot).Include(e => e.Subject);
        if (subjectId is int id)
        {
            enrollmentQuery = enrollmentQuery.Where(e => e.SubjectId == id);
        }

        List<Enrollment> enrollments = await enrollmentQuery.ToListAsync(cancellationToken);

        IQueryable<Team> teamQuery = db.Teams.AsNoTracking();
        if (subjectId is int teamSubjectId)
        {
            teamQuery = teamQuery.Where(t => t.SubjectId == teamSubjectId);
        }

        int teamsFormed = await teamQuery.CountAsync(cancellationToken);
        HashSet<int> usersInTeams = (await db.TeamMembers
                .Where(m => subjectId == null || m.Team.SubjectId == subjectId)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        DateTime now = DateTime.Now;
        int upcomingSessions = await db.StudySessions.CountAsync(s => s.StartsAt >= now, cancellationToken);

        // An explicit C# anonymous method (delegate syntax) used as a LINQ predicate.
        Func<Enrollment, bool> isAvailable = delegate (Enrollment enrollment)
        {
            return enrollment.LookingForTeam && !usersInTeams.Contains(enrollment.UserId);
        };

        int lookingForTeam = enrollments.Where(isAvailable).Select(e => e.UserId).Distinct().Count();

        // ----- Charts (GroupBy / OrderBy / Select with lambda expressions) -----
        List<ChartDatum> topInterests = users
            .SelectMany(u => u.Interests)
            .GroupBy(i => i.Name)
            .Select(g => new ChartDatum(g.Key.ToTitleCase(), g.Count(), $"{g.Count()} students"))
            .OrderByDescending(d => d.Value)
            .ThenBy(d => d.Label)
            .Take(8)
            .ToList();

        // With "All subjects" selected the subject code is added so labs of different subjects are not confused.
        List<ChartDatum> labDemand = enrollments
            .GroupBy(e => new { SubjectCode = e.Subject.Code, e.LabSlot.Code, e.LabSlot.Day })
            .OrderBy(g => g.Key.SubjectCode)
            .ThenBy(g => g.Key.Code)
            .Select(g => new ChartDatum(
                subjectId is null ? $"{g.Key.SubjectCode} {g.Key.Code}" : $"{g.Key.Code} ({g.Key.Day.ToShortName()})",
                g.Count(isAvailable),
                $"{g.Count(isAvailable)} of {g.Count()} enrolled students still need a team"))
            .ToList();

        List<ChartDatum> roles = enrollments
            .GroupBy(e => e.PreferredRole)
            .Select(g => new ChartDatum(g.Key.ToDisplayName(), g.Count(), $"{g.Count()} students prefer this role"))
            .OrderByDescending(d => d.Value)
            .ToList();

        List<ChartDatum> availabilityByDay = WeeklyAvailability.OrderedDays
            .Select(day => new
            {
                Day = day,
                Slots = users.SelectMany(u => u.Availability).Count(a => a.Day == day)
            })
            .Select(x => new ChartDatum(x.Day.ToShortName(), x.Slots, $"{x.Slots} free time slots on {x.Day}"))
            .ToList();

        // ----- Machine learning: K-Means study communities -----
        List<StudentProfile> profiles = users
            .Select(u => new StudentProfile(u.Id, u.Name, u.Interests.Select(i => i.Name), u.Availability.Select(a => a.ToSlot())))
            .ToList();

        Dictionary<int, string> namesById = users.ToDictionary(u => u.Id, u => u.Name);

        List<CommunityView> communities = new StudyCommunityAnalyzer()
            .Analyze(profiles)
            .Select(c => new CommunityView(c, c.MemberIds.Select(memberId => namesById[memberId]).OrderBy(n => n).ToList()))
            .ToList();

        return new InsightsReport(
            users.Count,
            lookingForTeam,
            teamsFormed,
            upcomingSessions,
            topInterests,
            labDemand,
            roles,
            availabilityByDay,
            communities);
    }

    /// <summary>Number of students currently online (has an open browser tab).</summary>
    public int OnlineStudents => _realtime.OnlineCount;
}
