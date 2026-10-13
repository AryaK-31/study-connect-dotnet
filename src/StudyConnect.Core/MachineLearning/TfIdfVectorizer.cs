using StudyConnect.Core.Extensions;

namespace StudyConnect.Core.MachineLearning;

/// <summary>
/// Turns each student's set of interest tags into a numeric feature vector using
/// TF-IDF weighting (term frequency × inverse document frequency).
/// Rare interests (e.g. "Quantum Computing") get a higher weight than very common ones
/// (e.g. "Web Development"), so they influence clustering more - exactly what we want when
/// grouping students by distinctive interests.
/// </summary>
public sealed class TfIdfVectorizer
{
    private readonly Dictionary<string, int> _vocabulary = new(StringComparer.Ordinal);
    private double[] _inverseDocumentFrequency = Array.Empty<double>();

    /// <summary>All known terms, in feature-index order.</summary>
    public IReadOnlyList<string> Vocabulary =>
        _vocabulary.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();

    public bool IsFitted => _vocabulary.Count > 0;

    /// <summary>Learns the vocabulary and IDF weights from a collection of documents (tag sets).</summary>
    public TfIdfVectorizer Fit(IEnumerable<IEnumerable<string>> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        List<List<string>> docs = documents
            .Select(doc => doc.Select(term => term.NormalizeTag()).Where(term => term.Length > 0).Distinct().ToList())
            .ToList();

        _vocabulary.Clear();
        foreach (string term in docs.SelectMany(doc => doc).Distinct().OrderBy(term => term, StringComparer.Ordinal))
        {
            _vocabulary[term] = _vocabulary.Count;
        }

        var documentFrequency = new int[_vocabulary.Count];
        foreach (string term in docs.SelectMany(doc => doc))
        {
            documentFrequency[_vocabulary[term]]++;
        }

        int documentCount = docs.Count;

        // Smoothed IDF (same formula as scikit-learn): ln((1 + n) / (1 + df)) + 1
        _inverseDocumentFrequency = documentFrequency
            .Select(df => Math.Log((1.0 + documentCount) / (1.0 + df)) + 1.0)
            .ToArray();

        return this;
    }

    /// <summary>Converts a document into an L2-normalised TF-IDF vector.</summary>
    public double[] Transform(IEnumerable<string> document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsFitted)
        {
            throw new InvalidOperationException("Call Fit before Transform.");
        }

        var vector = new double[_vocabulary.Count];
        foreach (string term in document.Select(t => t.NormalizeTag()).Distinct())
        {
            if (_vocabulary.TryGetValue(term, out int index))
            {
                vector[index] = _inverseDocumentFrequency[index];
            }
        }

        return VectorMath.Normalize(vector);
    }
}
