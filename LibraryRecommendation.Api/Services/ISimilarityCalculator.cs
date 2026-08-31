namespace LibraryRecommendation.Api.Services;

public interface ISimilarityCalculator
{
    double Compute(Dictionary<string, double> a, Dictionary<string, double> b);
}
