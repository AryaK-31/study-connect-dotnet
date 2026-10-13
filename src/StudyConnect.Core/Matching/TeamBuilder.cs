using StudyConnect.Core.Common;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Extensions;

namespace StudyConnect.Core.Matching;

/// <summary>A complete team proposed by the <see cref="TeamBuilder"/>.</summary>
public sealed record TeamSuggestion(
    IReadOnlyList<StudentProfile> Members,
    double Score,
    double AveragePairScore,
    IReadOnlyList<AvailabilitySlot> CommonSlots,
    IReadOnlyList<TeamRole> RolesCovered)
{
    /// <summary>Members other than the student who asked for suggestions.</summary>
    public IEnumerable<StudentProfile> OtherMembers(int userId) => Members.Where(m => m.UserId != userId);
}

/// <summary>Suggests complete teams for a student.</summary>
public interface ITeamBuilder
{
    IReadOnlyList<TeamSuggestion> Suggest(StudentProfile me, IReadOnlyList<StudentProfile> pool, int teamSize, int maxSuggestions = 3);
}

/// <summary>
/// Finds the best possible teams for a student.
/// <para>
/// Algorithm: shortlist the <see cref="ShortlistSize"/> most compatible candidates, enumerate every
/// combination of (teamSize - 1) of them, and score each resulting team by
/// 70% average pairwise compatibility (every member with every other member, memoised in a dictionary),
/// 15% role coverage (distinct roles / members) and 15% shared free time across all members.
/// </para>
/// With a shortlist of 12 and teams of 3 this evaluates only C(12,2) = 66 teams, so it is instant.
/// </summary>
public sealed class TeamBuilder : ITeamBuilder
{
    public const int ShortlistSize = 12;

    private const double PairWeight = 0.70;
    private const double RoleWeight = 0.15;
    private const double TimeWeight = 0.15;
    private const int IdealCommonSlots = 3;

    private readonly ICompatibilityEngine _engine;

    public TeamBuilder(ICompatibilityEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public IReadOnlyList<TeamSuggestion> Suggest(StudentProfile me, IReadOnlyList<StudentProfile> pool, int teamSize, int maxSuggestions = 3)
    {
        ArgumentNullException.ThrowIfNull(me);
        ArgumentNullException.ThrowIfNull(pool);

        if (teamSize < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(teamSize), "A team needs at least two members.");
        }

        // Memoise pair scores: the same pair appears in many combinations.
        var pairScores = new Dictionary<(int, int), double>();

        double PairScore(StudentProfile a, StudentProfile b)
        {
            (int, int) key = a.UserId < b.UserId ? (a.UserId, b.UserId) : (b.UserId, a.UserId);
            if (!pairScores.TryGetValue(key, out double score))
            {
                score = _engine.Evaluate(a, b).Score;
                pairScores[key] = score;
            }

            return score;
        }

        List<StudentProfile> shortlist = pool
            .Where(p => p.UserId != me.UserId)
            .TopBy(ShortlistSize, p => PairScore(me, p))
            .ToList();

        int othersNeeded = Math.Min(teamSize - 1, shortlist.Count);
        if (othersNeeded == 0)
        {
            return Array.Empty<TeamSuggestion>();
        }

        var suggestions = new List<TeamSuggestion>();

        foreach (IReadOnlyList<StudentProfile> combination in Combinatorics.Combinations(shortlist, othersNeeded))
        {
            var members = new List<StudentProfile>(othersNeeded + 1) { me };
            members.AddRange(combination);

            double averagePair = AveragePairScore(members, PairScore);

            List<TeamRole> roles = members.Select(m => m.PreferredRole).Distinct().OrderBy(r => r).ToList();
            double roleCoverage = (double)roles.Count / members.Count;

            List<AvailabilitySlot> commonSlots = members
                .Select(m => (IEnumerable<AvailabilitySlot>)m.Availability)
                .Aggregate((acc, next) => acc.Intersect(next))
                .OrderBy(s => s.Day == DayOfWeek.Sunday ? 7 : (int)s.Day)
                .ThenBy(s => s.Block)
                .ToList();
            double timeScore = Math.Min(commonSlots.Count, IdealCommonSlots) / (double)IdealCommonSlots;

            double teamScore = averagePair * PairWeight + roleCoverage * 100 * RoleWeight + timeScore * 100 * TimeWeight;

            suggestions.Add(new TeamSuggestion(members, Math.Round(teamScore, 1), Math.Round(averagePair, 1), commonSlots, roles));
        }

        return suggestions
            .OrderByDescending(s => s.Score)
            .Take(Math.Max(1, maxSuggestions))
            .ToList();
    }

    /// <summary>Average compatibility over every unordered pair of members.</summary>
    private static double AveragePairScore(IReadOnlyList<StudentProfile> members, Func<StudentProfile, StudentProfile, double> pairScore)
    {
        double total = 0;
        int pairs = 0;

        for (int i = 0; i < members.Count; i++)
        {
            for (int j = i + 1; j < members.Count; j++)
            {
                total += pairScore(members[i], members[j]);
                pairs++;
            }
        }

        return pairs == 0 ? 0 : total / pairs;
    }
}
