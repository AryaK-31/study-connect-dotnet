using StudyConnect.Core.Extensions;
using StudyConnect.Core.Matching;

namespace StudyConnect.Core.MachineLearning;

/// <summary>A group of students discovered by clustering their interests.</summary>
public sealed record StudyCommunity(int ClusterId, string Label, IReadOnlyList<string> TopTopics, IReadOnlyList<int> MemberIds);

/// <summary>
/// Discovers "study communities" - groups of students with similar interests - by combining
/// <see cref="TfIdfVectorizer"/> feature extraction with <see cref="KMeansClusterer"/>.
/// Each community is labelled with the highest-weighted terms of its centroid.
/// </summary>
public sealed class StudyCommunityAnalyzer
{
    public const int MinClusters = 2;
    public const int MaxClusters = 6;

    /// <summary>Picks a sensible number of clusters using the "rule of thumb" k ≈ √(n / 2).</summary>
    public static int SuggestClusterCount(int studentCount) =>
        Math.Clamp((int)Math.Round(Math.Sqrt(studentCount / 2.0)), MinClusters, MaxClusters);

    public IReadOnlyList<StudyCommunity> Analyze(IReadOnlyList<StudentProfile> students, int? clusterCount = null)
    {
        ArgumentNullException.ThrowIfNull(students);

        List<StudentProfile> withInterests = students.Where(s => s.Interests.Count > 0).ToList();
        if (withInterests.Count < MinClusters)
        {
            return Array.Empty<StudyCommunity>();
        }

        var vectorizer = new TfIdfVectorizer().Fit(withInterests.Select(s => s.Interests));
        List<double[]> vectors = withInterests.Select(s => vectorizer.Transform(s.Interests)).ToList();

        int k = clusterCount ?? SuggestClusterCount(withInterests.Count);
        KMeansResult result = new KMeansClusterer(k).Fit(vectors);
        IReadOnlyList<string> vocabulary = vectorizer.Vocabulary;

        return Enumerable.Range(0, result.ClusterCount)
            .Select(cluster => new
            {
                Cluster = cluster,
                Members = result.MembersOf(cluster).Select(i => withInterests[i]).ToList(),
                Centroid = result.Centroids[cluster]
            })
            .Where(group => group.Members.Count > 0)
            .Select(group =>
            {
                List<string> topics = group.Centroid
                    .Select((weight, index) => (Weight: weight, Index: index))
                    .Where(term => term.Weight > 0)
                    .OrderByDescending(term => term.Weight)
                    .Take(3)
                    .Select(term => vocabulary[term.Index].ToTitleCase())
                    .ToList();

                string label = topics.Count > 0 ? string.Join(" · ", topics) : "General";
                return new StudyCommunity(group.Cluster, label, topics, group.Members.Select(m => m.UserId).ToList());
            })
            .OrderByDescending(community => community.MemberIds.Count)
            .ToList();
    }
}
