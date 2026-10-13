namespace StudyConnect.Core.MachineLearning;

/// <summary>Output of a K-Means run.</summary>
/// <param name="Assignments">Cluster index for each input point (same order as the input).</param>
/// <param name="Centroids">Final cluster centres.</param>
/// <param name="Iterations">Number of iterations until convergence.</param>
/// <param name="Inertia">Sum of squared distances of points to their centroid (lower = tighter clusters).</param>
public sealed record KMeansResult(int[] Assignments, double[][] Centroids, int Iterations, double Inertia)
{
    public int ClusterCount => Centroids.Length;

    /// <summary>Indices of the points that belong to <paramref name="cluster"/>.</summary>
    public IEnumerable<int> MembersOf(int cluster) =>
        Enumerable.Range(0, Assignments.Length).Where(i => Assignments[i] == cluster);
}

/// <summary>
/// Unsupervised machine-learning algorithm implemented from scratch:
/// <b>K-Means clustering with k-means++ initialisation</b> (Arthur &amp; Vassilvitskii, 2007).
/// <para>
/// 1. Pick the first centroid at random, then pick each next centroid with probability
///    proportional to its squared distance from the nearest existing centroid (k-means++).<br/>
/// 2. Assignment step: assign every point to its nearest centroid.<br/>
/// 3. Update step: move each centroid to the mean of its points.<br/>
/// 4. Repeat 2-3 until no assignment changes or <see cref="MaxIterations"/> is reached.
/// </para>
/// A fixed random seed keeps results deterministic, which makes the algorithm unit-testable.
/// </summary>
public sealed class KMeansClusterer
{
    public KMeansClusterer(int clusterCount, int maxIterations = 100, int seed = 42)
    {
        if (clusterCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(clusterCount), "At least one cluster is required.");
        }

        if (maxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations));
        }

        ClusterCount = clusterCount;
        MaxIterations = maxIterations;
        Seed = seed;
    }

    public int ClusterCount { get; }

    public int MaxIterations { get; }

    public int Seed { get; }

    public KMeansResult Fit(IReadOnlyList<double[]> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
        {
            return new KMeansResult(Array.Empty<int>(), Array.Empty<double[]>(), 0, 0);
        }

        int dimensions = points[0].Length;
        if (points.Any(p => p.Length != dimensions))
        {
            throw new ArgumentException("All points must have the same number of dimensions.", nameof(points));
        }

        int k = Math.Min(ClusterCount, points.Count);
        var random = new Random(Seed);
        double[][] centroids = InitialiseCentroids(points, k, random);

        var assignments = new int[points.Count];
        Array.Fill(assignments, -1);

        int iteration = 0;
        bool changed = true;

        while (changed && iteration < MaxIterations)
        {
            iteration++;
            changed = false;

            // Assignment step
            for (int i = 0; i < points.Count; i++)
            {
                int nearest = NearestCentroid(points[i], centroids);
                if (nearest != assignments[i])
                {
                    assignments[i] = nearest;
                    changed = true;
                }
            }

            // Update step
            for (int cluster = 0; cluster < k; cluster++)
            {
                List<int> members = Enumerable.Range(0, points.Count).Where(i => assignments[i] == cluster).ToList();

                if (members.Count == 0)
                {
                    // Empty cluster: re-seed it with the point that is worst served by its centroid.
                    centroids[cluster] = (double[])points[FarthestPoint(points, centroids, assignments)].Clone();
                    changed = true;
                    continue;
                }

                var mean = new double[dimensions];
                foreach (int index in members)
                {
                    for (int d = 0; d < dimensions; d++)
                    {
                        mean[d] += points[index][d];
                    }
                }

                for (int d = 0; d < dimensions; d++)
                {
                    mean[d] /= members.Count;
                }

                centroids[cluster] = mean;
            }
        }

        double inertia = Enumerable.Range(0, points.Count)
            .Sum(i => VectorMath.SquaredDistance(points[i], centroids[assignments[i]]));

        return new KMeansResult(assignments, centroids, iteration, inertia);
    }

    private static double[][] InitialiseCentroids(IReadOnlyList<double[]> points, int k, Random random)
    {
        var centroids = new List<double[]> { (double[])points[random.Next(points.Count)].Clone() };

        while (centroids.Count < k)
        {
            double[] distances = points
                .Select(p => centroids.Min(c => VectorMath.SquaredDistance(p, c)))
                .ToArray();

            double total = distances.Sum();
            if (total <= 0)
            {
                // Every point coincides with a centroid already; any point will do.
                centroids.Add((double[])points[random.Next(points.Count)].Clone());
                continue;
            }

            double target = random.NextDouble() * total;
            double cumulative = 0;
            int chosen = points.Count - 1;

            for (int i = 0; i < distances.Length; i++)
            {
                cumulative += distances[i];
                if (cumulative >= target)
                {
                    chosen = i;
                    break;
                }
            }

            centroids.Add((double[])points[chosen].Clone());
        }

        return centroids.ToArray();
    }

    private static int NearestCentroid(double[] point, double[][] centroids)
    {
        int best = 0;
        double bestDistance = double.MaxValue;

        for (int c = 0; c < centroids.Length; c++)
        {
            double distance = VectorMath.SquaredDistance(point, centroids[c]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = c;
            }
        }

        return best;
    }

    private static int FarthestPoint(IReadOnlyList<double[]> points, double[][] centroids, int[] assignments)
    {
        int farthest = 0;
        double maxDistance = -1;

        for (int i = 0; i < points.Count; i++)
        {
            int cluster = assignments[i] < 0 ? 0 : assignments[i];
            double distance = VectorMath.SquaredDistance(points[i], centroids[cluster]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthest = i;
            }
        }

        return farthest;
    }
}
