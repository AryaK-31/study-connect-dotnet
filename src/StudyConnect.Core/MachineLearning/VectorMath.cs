namespace StudyConnect.Core.MachineLearning;

/// <summary>Small linear-algebra helpers used by the machine-learning algorithms.</summary>
public static class VectorMath
{
    public static double Dot(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        EnsureSameLength(a, b);
        double sum = 0;
        for (int i = 0; i < a.Count; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    public static double Norm(IReadOnlyList<double> vector) => Math.Sqrt(Dot(vector, vector));

    /// <summary>Returns a unit-length copy of the vector (or a zero vector if its norm is 0).</summary>
    public static double[] Normalize(IReadOnlyList<double> vector)
    {
        double norm = Norm(vector);
        var result = new double[vector.Count];
        if (norm == 0)
        {
            return result;
        }

        for (int i = 0; i < vector.Count; i++)
        {
            result[i] = vector[i] / norm;
        }

        return result;
    }

    /// <summary>Cosine similarity between two vectors (0 when either vector is all zeros).</summary>
    public static double CosineSimilarity(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        double normA = Norm(a);
        double normB = Norm(b);
        return normA == 0 || normB == 0 ? 0 : Dot(a, b) / (normA * normB);
    }

    /// <summary>Squared Euclidean distance (avoids the square root, which K-Means does not need).</summary>
    public static double SquaredDistance(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        EnsureSameLength(a, b);
        double sum = 0;
        for (int i = 0; i < a.Count; i++)
        {
            double difference = a[i] - b[i];
            sum += difference * difference;
        }

        return sum;
    }

    private static void EnsureSameLength(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        if (a.Count != b.Count)
        {
            throw new ArgumentException($"Vectors must have the same length ({a.Count} vs {b.Count}).");
        }
    }
}
