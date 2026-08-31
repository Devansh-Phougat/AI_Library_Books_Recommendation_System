namespace LibraryRecommendation.Api.Services;

public class CosineSimilarity : ISimilarityCalculator
{
    public double Compute(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return 0.0;
        }

        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);

        var dotProduct = 0.0;
        foreach (var (term, weight) in smaller)
        {
            if (larger.TryGetValue(term, out var otherWeight))
            {
                dotProduct += weight * otherWeight;
            }
        }

        var magnitudeA = Math.Sqrt(a.Values.Sum(v => v * v));
        var magnitudeB = Math.Sqrt(b.Values.Sum(v => v * v));
        if (magnitudeA == 0.0 || magnitudeB == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
