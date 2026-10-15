using StudyConnect.Core.Enums;
using StudyConnect.Core.Extensions;

namespace StudyConnect.Core.Matching;

/// <summary>
/// Contract for one dimension of compatibility between two students.
/// New criteria can be plugged into the <see cref="CompatibilityEngine"/> without changing it
/// (open/closed principle).
/// </summary>
public interface IMatchCriterion
{
    /// <summary>Short name shown in the score breakdown.</summary>
    string Name { get; }

    /// <summary>Relative importance of the criterion in the weighted average.</summary>
    double Weight { get; }

    /// <summary>Returns a score between 0 (incompatible) and 1 (perfect) for the pair.</summary>
    double Evaluate(StudentProfile me, StudentProfile other);

    /// <summary>Human-readable explanation of the score (shown as a "why you match" reason).</summary>
    string Explain(StudentProfile me, StudentProfile other, double score);
}

/// <summary>
/// Base class for all criteria. Each concrete criterion overrides <see cref="Evaluate"/>
/// and can provide a custom <see cref="Explain"/> implementation.
/// </summary>
public abstract class MatchCriterion : IMatchCriterion
{
    protected MatchCriterion(string name, double weight)
    {
        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive.");
        }

        Name = name;
        Weight = weight;
    }

    public string Name { get; }

    public double Weight { get; }

    public abstract double Evaluate(StudentProfile me, StudentProfile other);

    public virtual string Explain(StudentProfile me, StudentProfile other, double score) =>
        $"{Name}: {score:P0}";

    /// <summary>Clamps any value into the [0, 1] range.</summary>
    protected static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);

    public override string ToString() => $"{Name} (weight {Weight})";
}

/// <summary>Assignment groups must come from the same lab, so this criterion is weighted heavily.</summary>
public sealed class SameLabCriterion : MatchCriterion
{
    public SameLabCriterion(double weight = 3) : base("Same lab", weight)
    {
    }

    public override double Evaluate(StudentProfile me, StudentProfile other) =>
        me.LabSlotId.HasValue && me.LabSlotId == other.LabSlotId ? 1 : 0;

    public override string Explain(StudentProfile me, StudentProfile other, double score) =>
        score >= 1 ? $"Same lab ({other.LabLabel ?? "your lab"})" : $"Different lab ({other.LabLabel ?? "unknown"})";
}

/// <summary>Similarity of interests measured with the Jaccard index.</summary>
public sealed class SharedInterestsCriterion : MatchCriterion
{
    public SharedInterestsCriterion(double weight = 3) : base("Shared interests", weight)
    {
    }

    public override double Evaluate(StudentProfile me, StudentProfile other)
    {
        if (me.Interests.Count == 0 || other.Interests.Count == 0)
        {
            return 0;
        }

        // Jaccard is harsh for small sets, so boost it slightly and cap at 1.
        return Clamp01(me.Interests.JaccardSimilarity(other.Interests) * 1.5);
    }

    public override string Explain(StudentProfile me, StudentProfile other, double score)
    {
        List<string> shared = me.Interests.Intersect(other.Interests).Select(i => i.ToTitleCase()).OrderBy(i => i).ToList();
        return shared.Count switch
        {
            0 => "No shared interests yet",
            1 => $"Shared interest: {shared[0]}",
            _ => $"{shared.Count} shared interests: {string.Join(", ", shared.Take(3))}"
        };
    }
}

/// <summary>How many weekly free slots the two students have in common.</summary>
public sealed class AvailabilityOverlapCriterion : MatchCriterion
{
    /// <summary>Number of common slots that is considered "plenty" (score 1).</summary>
    private const int IdealCommonSlots = 4;

    public AvailabilityOverlapCriterion(double weight = 2) : base("Availability overlap", weight)
    {
    }

    public override double Evaluate(StudentProfile me, StudentProfile other)
    {
        int common = me.Availability.Count(other.Availability.Contains);
        return Clamp01((double)common / IdealCommonSlots);
    }

    public override string Explain(StudentProfile me, StudentProfile other, double score)
    {
        int common = me.Availability.Count(other.Availability.Contains);
        return common == 0 ? "No common free time" : $"{common} common free time slot{(common == 1 ? string.Empty : "s")}";
    }
}

/// <summary>Teams work better with complementary roles (front-end + back-end + testing ...).</summary>
public sealed class RoleComplementCriterion : MatchCriterion
{
    public RoleComplementCriterion(double weight = 1.5) : base("Complementary roles", weight)
    {
    }

    public override double Evaluate(StudentProfile me, StudentProfile other)
    {
        if (me.PreferredRole != other.PreferredRole)
        {
            return 1;
        }

        // Two full-stack developers can still split work reasonably well.
        return me.PreferredRole == TeamRole.FullStack ? 0.6 : 0.3;
    }

    public override string Explain(StudentProfile me, StudentProfile other, double score) =>
        me.PreferredRole != other.PreferredRole
            ? $"Complements you as {other.PreferredRole.ToDisplayName()}"
            : $"Same preferred role ({other.PreferredRole.ToDisplayName()})";
}

/// <summary>Students aiming for the same grade usually have similar commitment levels.</summary>
public sealed class GoalAlignmentCriterion : MatchCriterion
{
    public GoalAlignmentCriterion(double weight = 1.5) : base("Goal alignment", weight)
    {
    }

    public override double Evaluate(StudentProfile me, StudentProfile other)
    {
        int maxDistance = Enum.GetValues<TargetGrade>().Length - 1;
        int distance = Math.Abs((int)me.Goal - (int)other.Goal);
        return Clamp01(1 - (double)distance / maxDistance);
    }

    public override string Explain(StudentProfile me, StudentProfile other, double score) =>
        me.Goal == other.Goal
            ? $"Also aiming for {other.Goal.ToDisplayName()}"
            : $"Aiming for {other.Goal.ToDisplayName()}";
}

/// <summary>Prefers team mates within one skill level of each other.</summary>
public sealed class SkillBalanceCriterion : MatchCriterion
{
    public SkillBalanceCriterion(double weight = 1) : base("Skill balance", weight)
    {
    }

    public override double Evaluate(StudentProfile me, StudentProfile other)
    {
        int distance = Math.Abs((int)me.Skill - (int)other.Skill);
        return distance switch
        {
            0 => 1,
            1 => 0.75,
            _ => 0.35
        };
    }

    public override string Explain(StudentProfile me, StudentProfile other, double score) =>
        $"{other.Skill.ToDisplayName()} skill level";
}
