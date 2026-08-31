namespace LibraryRecommendation.Api.Services;

public interface ITextVectorizer
{
    void Fit(IEnumerable<string> corpus);
    Dictionary<string, double> Transform(string document);
}
