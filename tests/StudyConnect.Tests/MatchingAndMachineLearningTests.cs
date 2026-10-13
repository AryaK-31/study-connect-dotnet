using StudyConnect.Core.Enums;
using StudyConnect.Core.MachineLearning;
using StudyConnect.Core.Matching;

namespace StudyConnect.Tests;

[TestFixture]
public class CompatibilityEngineTests
{
    private static readonly AvailabilitySlot MonAm = new(DayOfWeek.Monday, TimeBlock.Morning);
    private static readonly AvailabilitySlot MonPm = new(DayOfWeek.Monday, TimeBlock.Afternoon);
    private static readonly AvailabilitySlot FriEve = new(DayOfWeek.Friday, TimeBlock.Evening);

    private static StudentProfile Student(int id, int lab, TeamRole role, string[] interests, params AvailabilitySlot[] slots) =>
        new(id, $"Student {id}", interests, slots) { LabSlotId = lab, PreferredRole = role, LabLabel = $"Lab {lab}" };

    [Test]
    public void SameLabAndSharedInterests_ScoreHigherThanStrangers()
    {
        var engine = new CompatibilityEngine();
        StudentProfile me = Student(1, 1, TeamRole.Backend, new[] { "ML", "C#" }, MonAm, MonPm);
        StudentProfile goodMatch = Student(2, 1, TeamRole.Frontend, new[] { "ml", "c#" }, MonAm, MonPm);
        StudentProfile poorMatch = Student(3, 2, TeamRole.Backend, new[] { "Art" }, FriEve);

        double good = engine.Evaluate(me, goodMatch).Score;
        double poor = engine.Evaluate(me, poorMatch).Score;

        Assert.Multiple(() =>
        {
            Assert.That(good, Is.GreaterThan(poor));
            Assert.That(good, Is.InRange(0, 100));
            Assert.That(poor, Is.InRange(0, 100));
            Assert.That(engine.Evaluate(me, goodMatch).Quality, Is.EqualTo(MatchQuality.Excellent));
        });
    }

    [Test]
    public void Rank_SortsDescendingAndExcludesSelf()
    {
        var engine = new CompatibilityEngine();
        StudentProfile me = Student(1, 1, TeamRole.Backend, new[] { "ml" }, MonAm);
        var candidates = new[]
        {
            me,
            Student(2, 2, TeamRole.Backend, new[] { "art" }, FriEve),
            Student(3, 1, TeamRole.Frontend, new[] { "ml" }, MonAm),
            Student(4, 1, TeamRole.Testing, new[] { "web" }, MonPm)
        };

        IReadOnlyList<MatchResult> ranked = engine.Rank(me, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(ranked.Select(r => r.Candidate.UserId), Does.Not.Contain(1));
            Assert.That(ranked[0].Candidate.UserId, Is.EqualTo(3));
            Assert.That(ranked.Select(r => r.Score), Is.Ordered.Descending);
        });
    }

    [Test]
    public void Rank_WithTake_ReturnsOnlyTheBest()
    {
        var engine = new CompatibilityEngine();
        StudentProfile me = Student(1, 1, TeamRole.Backend, new[] { "ml" }, MonAm);
        IEnumerable<StudentProfile> candidates = Enumerable.Range(2, 10).Select(i => Student(i, i % 2, TeamRole.Frontend, new[] { "ml" }, MonAm));

        Assert.That(engine.Rank(me, candidates, take: 3), Has.Count.EqualTo(3));
    }

    [Test]
    public void CustomCriterion_IsUsedPolymorphically()
    {
        var engine = new CompatibilityEngine(new IMatchCriterion[] { new AlwaysPerfectCriterion() });
        MatchResult result = engine.Evaluate(Student(1, 1, TeamRole.Backend, new[] { "a" }), Student(2, 2, TeamRole.Backend, new[] { "b" }));

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(100));
            Assert.That(result.Breakdown.Single().Explanation, Is.EqualTo("Always perfect"));
        });
    }

    [Test]
    public void Engine_WithoutCriteria_Throws()
    {
        Assert.That(() => new CompatibilityEngine(Array.Empty<IMatchCriterion>()), Throws.ArgumentException);
    }

    [Test]
    public void Breakdown_ExplainsSharedInterests()
    {
        var engine = new CompatibilityEngine();
        MatchResult result = engine.Evaluate(
            Student(1, 1, TeamRole.Backend, new[] { "machine learning", "python" }, MonAm),
            Student(2, 1, TeamRole.Frontend, new[] { "Machine Learning", "Python" }, MonAm));

        Assert.That(result.TopReasons(5).Any(reason => reason.Contains("shared interests")), Is.True);
    }

    /// <summary>A trivial criterion proving that new criteria can be plugged in by subclassing.</summary>
    private sealed class AlwaysPerfectCriterion : MatchCriterion
    {
        public AlwaysPerfectCriterion() : base("Always", 1)
        {
        }

        public override double Evaluate(StudentProfile me, StudentProfile other) => 1;

        public override string Explain(StudentProfile me, StudentProfile other, double score) => "Always perfect";
    }
}

[TestFixture]
public class TeamBuilderTests
{
    private static StudentProfile Student(int id, TeamRole role, string interest, params AvailabilitySlot[] slots) =>
        new(id, $"S{id}", new[] { interest }, slots) { LabSlotId = 1, PreferredRole = role };

    [Test]
    public void Suggest_ReturnsTeamsOfTheRequestedSize_IncludingMe()
    {
        var builder = new TeamBuilder(new CompatibilityEngine());
        var slot = new AvailabilitySlot(DayOfWeek.Monday, TimeBlock.Afternoon);
        StudentProfile me = Student(1, TeamRole.Backend, "ml", slot);
        List<StudentProfile> pool = Enumerable.Range(2, 6)
            .Select(i => Student(i, (TeamRole)(i % 5), i % 2 == 0 ? "ml" : "art", slot))
            .ToList();

        IReadOnlyList<TeamSuggestion> suggestions = builder.Suggest(me, pool, teamSize: 3, maxSuggestions: 3);

        Assert.Multiple(() =>
        {
            Assert.That(suggestions, Has.Count.EqualTo(3));
            Assert.That(suggestions.All(s => s.Members.Count == 3), Is.True);
            Assert.That(suggestions.All(s => s.Members.Any(m => m.UserId == 1)), Is.True);
            Assert.That(suggestions.Select(s => s.Score), Is.Ordered.Descending);
            Assert.That(suggestions[0].CommonSlots, Does.Contain(slot));
        });
    }

    [Test]
    public void Suggest_PrefersComplementaryRolesAndSharedInterests()
    {
        var builder = new TeamBuilder(new CompatibilityEngine());
        var slot = new AvailabilitySlot(DayOfWeek.Monday, TimeBlock.Afternoon);
        StudentProfile me = Student(1, TeamRole.Backend, "ml", slot);
        var pool = new List<StudentProfile>
        {
            Student(2, TeamRole.Frontend, "ml", slot),
            Student(3, TeamRole.Testing, "ml", slot),
            Student(4, TeamRole.Backend, "pottery", new AvailabilitySlot(DayOfWeek.Sunday, TimeBlock.Evening))
        };

        TeamSuggestion best = builder.Suggest(me, pool, teamSize: 3).First();

        Assert.That(best.Members.Select(m => m.UserId), Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Suggest_WithEmptyPool_ReturnsNothing()
    {
        var builder = new TeamBuilder(new CompatibilityEngine());
        StudentProfile me = Student(1, TeamRole.Backend, "ml");
        Assert.That(builder.Suggest(me, Array.Empty<StudentProfile>(), 3), Is.Empty);
    }

    [Test]
    public void Suggest_TeamSizeBelowTwo_Throws()
    {
        var builder = new TeamBuilder(new CompatibilityEngine());
        Assert.That(() => builder.Suggest(Student(1, TeamRole.Backend, "ml"), Array.Empty<StudentProfile>(), 1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}

[TestFixture]
public class MachineLearningTests
{
    [Test]
    public void KMeans_SeparatesTwoObviousClusters()
    {
        var points = new List<double[]>
        {
            new[] { 0.0, 0.1 }, new[] { 0.2, 0.0 }, new[] { 0.1, 0.2 },
            new[] { 10.0, 10.1 }, new[] { 10.2, 9.9 }, new[] { 9.8, 10.0 }
        };

        KMeansResult result = new KMeansClusterer(2).Fit(points);

        Assert.Multiple(() =>
        {
            Assert.That(result.ClusterCount, Is.EqualTo(2));
            Assert.That(result.Assignments.Take(3).Distinct().Count(), Is.EqualTo(1), "first three points share a cluster");
            Assert.That(result.Assignments.Skip(3).Distinct().Count(), Is.EqualTo(1), "last three points share a cluster");
            Assert.That(result.Assignments[0], Is.Not.EqualTo(result.Assignments[3]));
            Assert.That(result.Inertia, Is.LessThan(1.0));
        });
    }

    [Test]
    public void KMeans_IsDeterministicForAFixedSeed()
    {
        var random = new Random(1);
        List<double[]> points = Enumerable.Range(0, 40).Select(_ => new[] { random.NextDouble(), random.NextDouble() }).ToList();

        KMeansResult first = new KMeansClusterer(4, seed: 7).Fit(points);
        KMeansResult second = new KMeansClusterer(4, seed: 7).Fit(points);

        Assert.That(first.Assignments, Is.EqualTo(second.Assignments));
    }

    [Test]
    public void KMeans_ClampsClusterCountToNumberOfPoints()
    {
        var points = new List<double[]> { new[] { 1.0 }, new[] { 2.0 } };
        Assert.That(new KMeansClusterer(5).Fit(points).ClusterCount, Is.EqualTo(2));
    }

    [Test]
    public void KMeans_RejectsInvalidArguments()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new KMeansClusterer(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new KMeansClusterer(2).Fit(new List<double[]> { new[] { 1.0 }, new[] { 1.0, 2.0 } }), Throws.ArgumentException);
            Assert.That(new KMeansClusterer(2).Fit(new List<double[]>()).Assignments, Is.Empty);
        });
    }

    [Test]
    public void TfIdf_GivesRareTermsMoreWeight_AndNormalisesVectors()
    {
        var vectorizer = new TfIdfVectorizer().Fit(new[]
        {
            new[] { "web", "ml" },
            new[] { "web" },
            new[] { "web", "quantum" }
        });

        double[] vector = vectorizer.Transform(new[] { "web", "quantum" });
        int webIndex = vectorizer.Vocabulary.ToList().IndexOf("web");
        int quantumIndex = vectorizer.Vocabulary.ToList().IndexOf("quantum");

        Assert.Multiple(() =>
        {
            Assert.That(vector[quantumIndex], Is.GreaterThan(vector[webIndex]));
            Assert.That(VectorMath.Norm(vector), Is.EqualTo(1.0).Within(1e-9));
        });
    }

    [Test]
    public void CommunityAnalyzer_GroupsStudentsWithSimilarInterests()
    {
        var none = Array.Empty<AvailabilitySlot>();
        var students = new List<StudentProfile>
        {
            new(1, "A", new[] { "machine learning", "python" }, none),
            new(2, "B", new[] { "machine learning", "python", "data science" }, none),
            new(3, "C", new[] { "python", "data science" }, none),
            new(4, "D", new[] { "react", "javascript" }, none),
            new(5, "E", new[] { "react", "javascript", "web development" }, none),
            new(6, "F", new[] { "javascript", "web development" }, none)
        };

        IReadOnlyList<StudyCommunity> communities = new StudyCommunityAnalyzer().Analyze(students, clusterCount: 2);

        Assert.Multiple(() =>
        {
            Assert.That(communities, Has.Count.EqualTo(2));
            // Every community is "pure": it contains only data/ML students (ids 1-3) or only web students (ids 4-6).
            Assert.That(communities.All(c => c.MemberIds.All(id => id <= 3) || c.MemberIds.All(id => id >= 4)), Is.True);
            Assert.That(communities.All(c => c.TopTopics.Count > 0), Is.True);
        });
    }

    [Test]
    public void CosineSimilarity_OfIdenticalVectorsIsOne()
    {
        Assert.That(VectorMath.CosineSimilarity(new[] { 1.0, 2.0 }, new[] { 2.0, 4.0 }), Is.EqualTo(1.0).Within(1e-9));
    }
}
