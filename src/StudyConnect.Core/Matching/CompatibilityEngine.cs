using StudyConnect.Core.Extensions;

namespace StudyConnect.Core.Matching;

/// <summary>Qualitative band of a compatibility score.</summary>
public enum MatchQuality
{
    Low = 0,
    Fair = 1,
    Good = 2,
    Excellent = 3
}

/// <summary>Score of one criterion for one pair of students.</summary>
public sealed record CriterionScore(string Criterion, double Weight, double Score, string Explanation)
{
    /// <summary>Contribution of the criterion to the weighted total.</summary>
    public double WeightedScore => Weight * Score;
}

/// <summary>Result of comparing the current student with one candidate.</summary>
public sealed class MatchResult
{
    public MatchResult(StudentProfile candidate, double score, IReadOnlyList<CriterionScore> breakdown)
    {
        Candidate = candidate;
        Score = score;
        Breakdown = breakdown;
    }

    public StudentProfile Candidate { get; }

    /// <summary>Overall compatibility from 0 to 100.</summary>
    public double Score { get; }

    public IReadOnlyList<CriterionScore> Breakdown { get; }

    public MatchQuality Quality => Score switch
    {
        >= 75 => MatchQuality.Excellent,
        >= 55 => MatchQuality.Good,
        >= 35 => MatchQuality.Fair,
        _ => MatchQuality.Low
    };

    /// <summary>The strongest positive reasons, used for the "why you match" chips.</summary>
    public IEnumerable<string> TopReasons(int count = 3) =>
        Breakdown
            .Where(b => b.Score >= 0.5)
            .OrderByDescending(b => b.WeightedScore)
            .Take(count)
            .Select(b => b.Explanation);
}

/// <summary>Computes how compatible two students are as study partners / team mates.</summary>
public interface ICompatibilityEngine
{
    IReadOnlyList<IMatchCriterion> Criteria { get; }

    MatchResult Evaluate(StudentProfile me, StudentProfile other);

    IReadOnlyList<MatchResult> Rank(StudentProfile me, IEnumerable<StudentProfile> candidates, double minimumScore = 0, int? take = null);
}

/// <summary>
/// Weighted multi-criteria compatibility engine.
/// score = 100 * Σ(weight_i * criterion_i) / Σ(weight_i)
/// </summary>
public sealed class CompatibilityEngine : ICompatibilityEngine
{
    /// <summary>Creates the engine with the default criteria used by the Team Finder.</summary>
    public CompatibilityEngine()
        : this(CreateDefaultCriteria())
    {
    }

    /// <summary>Creates the engine with a custom set of criteria (constructor overloading).</summary>
    public CompatibilityEngine(IEnumerable<IMatchCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        List<IMatchCriterion> list = criteria.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one criterion is required.", nameof(criteria));
        }

        Criteria = list;
    }

    public IReadOnlyList<IMatchCriterion> Criteria { get; }

    /// <summary>The criteria used for lab-based team matching.</summary>
    public static IReadOnlyList<IMatchCriterion> CreateDefaultCriteria() => new IMatchCriterion[]
    {
        new SameLabCriterion(),
        new SharedInterestsCriterion(),
        new AvailabilityOverlapCriterion(),
        new RoleComplementCriterion(),
        new GoalAlignmentCriterion(),
        new SkillBalanceCriterion()
    };

    /// <summary>Lighter criteria set used for the dashboard's "similar students" list (no subject context).</summary>
    public static CompatibilityEngine CreateInterestEngine() => new(new IMatchCriterion[]
    {
        new SharedInterestsCriterion(weight: 3),
        new AvailabilityOverlapCriterion(weight: 1.5)
    });

    public MatchResult Evaluate(StudentProfile me, StudentProfile other)
    {
        ArgumentNullException.ThrowIfNull(me);
        ArgumentNullException.ThrowIfNull(other);

        // LINQ projection with a lambda: evaluate every criterion polymorphically.
        List<CriterionScore> breakdown = Criteria
            .Select(criterion =>
            {
                double value = Math.Clamp(criterion.Evaluate(me, other), 0d, 1d);
                return new CriterionScore(criterion.Name, criterion.Weight, value, criterion.Explain(me, other, value));
            })
            .ToList();

        double totalWeight = breakdown.Sum(b => b.Weight);
        double score = totalWeight <= 0 ? 0 : breakdown.Sum(b => b.WeightedScore) / totalWeight * 100;

        return new MatchResult(other, Math.Round(score, 1), breakdown);
    }

    public IReadOnlyList<MatchResult> Rank(StudentProfile me, IEnumerable<StudentProfile> candidates, double minimumScore = 0, int? take = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        IEnumerable<MatchResult> scored = candidates
            .Where(candidate => candidate.UserId != me.UserId)
            .Select(candidate => Evaluate(me, candidate))
            .Where(result => result.Score >= minimumScore);

        if (take is int limit)
        {
            return scored.TopBy(limit, result => result.Score);
        }

        return scored
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
