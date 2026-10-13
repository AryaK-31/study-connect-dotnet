namespace StudyConnect.Infrastructure.Services;

/// <summary>Converts EF entities into the plain <see cref="StudentProfile"/> objects used by the algorithms.</summary>
internal static class ProfileMapper
{
    public static StudentProfile ToProfile(User user, Enrollment? enrollment = null) =>
        new(user.Id, user.Name, user.Interests.Select(i => i.Name), user.Availability.Select(a => a.ToSlot()))
        {
            Email = user.Email,
            Degree = user.Degree,
            Pitch = enrollment?.Pitch,
            LabSlotId = enrollment?.LabSlotId,
            LabLabel = enrollment?.LabSlot?.Code,
            PreferredRole = enrollment?.PreferredRole ?? TeamRole.FullStack,
            Skill = enrollment?.Skill ?? SkillLevel.Intermediate,
            Goal = enrollment?.Goal ?? TargetGrade.Distinction,
            LookingForTeam = enrollment?.LookingForTeam ?? false
        };
}

/// <summary>
/// Student matching. "Similar students" ports the original interest-based matching;
/// <see cref="FindTeammatesAsync"/> is the new centralised Team Finder: it only considers students
/// from the same subject and lab, ranks them with the multi-criteria <see cref="ICompatibilityEngine"/>
/// and asks the <see cref="ITeamBuilder"/> for complete team suggestions.
/// </summary>
public sealed class MatchmakingService : IMatchmakingService
{
    private static readonly ICompatibilityEngine InterestEngine = CompatibilityEngine.CreateInterestEngine();

    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly ICompatibilityEngine _engine;
    private readonly ITeamBuilder _teamBuilder;
    private readonly IConnectionService _connections;

    public MatchmakingService(
        IDbContextFactory<StudyConnectDbContext> dbFactory,
        ICompatibilityEngine engine,
        ITeamBuilder teamBuilder,
        IConnectionService connections)
    {
        _dbFactory = dbFactory;
        _engine = engine;
        _teamBuilder = teamBuilder;
        _connections = connections;
    }

    public async Task<IReadOnlyList<MatchResult>> GetSimilarStudentsAsync(int userId, int take = 9, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        User? me = await db.Users
            .AsNoTracking()
            .Include(u => u.Interests)
            .Include(u => u.Availability)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (me is null || me.Interests.Count == 0)
        {
            return Array.Empty<MatchResult>();
        }

        List<string> myInterests = me.Interests.Select(i => i.Name).ToList();

        // Students sharing at least one interest (same rule as the original $in query).
        List<User> others = await db.Users
            .AsNoTracking()
            .Include(u => u.Interests)
            .Include(u => u.Availability)
            .AsSplitQuery()
            .Where(u => u.Id != userId && u.Interests.Any(i => myInterests.Contains(i.Name)))
            .ToListAsync(cancellationToken);

        StudentProfile myProfile = ProfileMapper.ToProfile(me);
        return InterestEngine.Rank(myProfile, others.Select(u => ProfileMapper.ToProfile(u)), take: take);
    }

    public async Task<TeamFinderResult> FindTeammatesAsync(int userId, int subjectId, TeamFinderFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Subject? subject = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Labs)
            .FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);

        if (subject is null)
        {
            return TeamFinderResult.NotEnrolled(null);
        }

        List<Enrollment> enrollments = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.User).ThenInclude(u => u.Interests)
            .Include(e => e.User).ThenInclude(u => u.Availability)
            .Include(e => e.LabSlot)
            .Where(e => e.SubjectId == subjectId)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        Enrollment? mine = enrollments.FirstOrDefault(e => e.UserId == userId);
        if (mine is null)
        {
            return TeamFinderResult.NotEnrolled(subject);
        }

        List<Team> teams = await db.Teams
            .AsNoTracking()
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Include(t => t.LabSlot)
            .Where(t => t.SubjectId == subjectId)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        HashSet<int> inTeam = teams.SelectMany(t => t.Members).Select(m => m.UserId).ToHashSet();
        Team? myTeam = teams.FirstOrDefault(t => t.HasMember(userId));

        HashSet<int> pendingInvitees = new();
        if (myTeam is not null)
        {
            int myTeamId = myTeam.Id;
            List<int> invited = await db.TeamInvitations
                .Where(i => i.TeamId == myTeamId && i.Status == InvitationStatus.Pending)
                .Select(i => i.UserId)
                .ToListAsync(cancellationToken);
            pendingInvitees = invited.ToHashSet();
        }

        IReadOnlyDictionary<int, PeerStatus> peers = await _connections.GetPeerStatusesAsync(userId, cancellationToken);

        // ----- Filter candidates with LINQ + lambda expressions -----
        IEnumerable<Enrollment> candidates = enrollments.Where(e => e.UserId != userId);

        if (filter.SameLabOnly)
        {
            candidates = candidates.Where(e => e.LabSlotId == mine.LabSlotId);
        }

        if (filter.OnlyAvailable)
        {
            candidates = candidates.Where(e => e.LookingForTeam && !inTeam.Contains(e.UserId));
        }

        if (filter.Role is TeamRole role)
        {
            candidates = candidates.Where(e => e.PreferredRole == role);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string term = filter.Search.Trim();
            candidates = candidates.Where(e =>
                e.User.Name.ContainsIgnoreCase(term)
                || e.User.Interests.Any(i => i.Name.ContainsIgnoreCase(term))
                || e.Pitch.ContainsIgnoreCase(term));
        }

        StudentProfile me = ProfileMapper.ToProfile(mine.User, mine);
        List<StudentProfile> candidateProfiles = candidates.Select(e => ProfileMapper.ToProfile(e.User, e)).ToList();

        List<CandidateMatch> matches = _engine
            .Rank(me, candidateProfiles, filter.MinimumScore)
            .Select(result => new CandidateMatch(
                result,
                inTeam.Contains(result.Candidate.UserId),
                peers.TryGetValue(result.Candidate.UserId, out PeerStatus status) ? status : PeerStatus.None,
                pendingInvitees.Contains(result.Candidate.UserId)))
            .ToList();

        // ----- Complete-team suggestions (only while the student has no team yet) -----
        IReadOnlyList<TeamSuggestion> suggestions = Array.Empty<TeamSuggestion>();
        if (myTeam is null)
        {
            List<StudentProfile> pool = enrollments
                .Where(e => e.UserId != userId
                            && e.LabSlotId == mine.LabSlotId
                            && e.LookingForTeam
                            && !inTeam.Contains(e.UserId))
                .Select(e => ProfileMapper.ToProfile(e.User, e))
                .ToList();

            int teamSize = Math.Clamp(filter.TeamSize, subject.MinTeamSize, Team.MaximumAllowedMembers);
            suggestions = _teamBuilder.Suggest(me, pool, teamSize, maxSuggestions: 3);
        }

        List<Team> openTeams = teams
            .Where(t => t.LabSlotId == mine.LabSlotId && t.IsRecruiting && !t.HasMember(userId))
            .OrderByDescending(t => t.Members.Count)
            .ToList();

        int availableInMyLab = enrollments.Count(e =>
            e.UserId != userId && e.LabSlotId == mine.LabSlotId && e.LookingForTeam && !inTeam.Contains(e.UserId));

        return new TeamFinderResult(subject, mine, myTeam, matches, suggestions, openTeams, enrollments.Count, availableInMyLab);
    }
}
