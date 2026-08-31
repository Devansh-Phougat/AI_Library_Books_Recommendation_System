using System.Text;

namespace LibraryRecommendation.Api.Services;

public class TfIdfVectorizer : ITextVectorizer
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "about", "above", "across", "after", "again", "against", "ahead",
        "all", "almost", "alone", "along", "alongside", "already", "also",
        "although", "altogether", "always", "am", "among", "amongst", "an",
        "and", "another", "any", "anybody", "anyone", "anything", "anywhere",
        "apart", "are", "around", "as", "aside", "at", "away", "back", "be",
        "became", "because", "become", "becomes", "becoming", "been", "before",
        "began", "begin", "begins", "begun", "behind", "being", "below",
        "beneath", "beside", "besides", "between", "beyond", "book", "both",
        "bring", "brings", "brought", "but", "by", "came", "can", "cannot",
        "come", "comes", "coming", "could", "day", "days", "did", "do",
        "does", "doing", "done", "down", "during", "each", "either", "else",
        "elsewhere", "end", "ends", "enough", "especially", "even",
        "eventually", "ever", "every", "everybody", "everyone", "everything",
        "everywhere", "except", "far", "few", "finally", "find", "finds",
        "first", "follow", "followed", "follows", "for", "form", "forms",
        "found", "from", "further", "gave", "get", "gets", "getting", "give",
        "given", "gives", "giving", "go", "goes", "going", "gone", "got",
        "had", "hardly", "has", "have", "having", "he", "hence", "her",
        "here", "hers", "herself", "him", "himself", "his", "house", "how",
        "however", "if", "in", "indeed", "inside", "instead", "into", "is",
        "it", "its", "itself", "just", "keep", "keeps", "kept", "kind",
        "kinds", "last", "later", "lead", "leads", "least", "less", "let",
        "lets", "life", "like", "likely", "lives", "look", "looks", "made",
        "make", "makes", "making", "man", "many", "may", "maybe", "me",
        "meanwhile", "men", "might", "more", "moreover", "most", "much",
        "must", "my", "myself", "name", "names", "near", "nearly", "neither",
        "never", "next", "no", "nobody", "none", "nor", "not", "nothing",
        "now", "nowhere", "of", "off", "often", "on", "once", "one", "only",
        "onto", "or", "other", "ought", "our", "ourselves", "out", "outside",
        "over", "own", "part", "parts", "perhaps", "place", "places", "plan",
        "plans", "put", "puts", "quite", "rather", "really", "said", "same",
        "say", "says", "see", "seem", "seemed", "seems", "seen", "set",
        "several", "shall", "she", "should", "since", "so", "some", "someone",
        "something", "sometimes", "somewhat", "somewhere", "soon", "still",
        "stories", "story", "such", "take", "taken", "takes", "taking",
        "tell", "tells", "than", "that", "the", "their", "them", "themselves",
        "then", "there", "therefore", "these", "they", "thing", "things",
        "this", "those", "though", "through", "throughout", "thus", "time",
        "times", "to", "together", "told", "too", "took", "toward", "towards",
        "turn", "turned", "turns", "under", "unless", "unlike", "until", "up",
        "upon", "us", "use", "used", "uses", "using", "usually", "very",
        "was", "way", "ways", "we", "well", "went", "were", "what",
        "whatever", "when", "whenever", "where", "whereas", "wherever",
        "whether", "which", "while", "who", "whoever", "whom", "whose", "why",
        "will", "with", "within", "without", "woman", "work", "works",
        "world", "would", "year", "years", "yet", "you", "your", "yourself"
    };

    private Dictionary<string, int>? _documentFrequencies;
    private int _corpusSize;

    public void Fit(IEnumerable<string> corpus)
    {
        var frequencies = new Dictionary<string, int>();
        var size = 0;

        foreach (var document in corpus)
        {
            size++;
            foreach (var term in Tokenize(document).Distinct())
            {
                frequencies[term] = frequencies.TryGetValue(term, out var df) ? df + 1 : 1;
            }
        }

        _documentFrequencies = frequencies;
        _corpusSize = size;
    }

    public Dictionary<string, double> Transform(string document)
    {
        if (_documentFrequencies is null)
        {
            throw new InvalidOperationException(
                "The vectorizer must be fitted with Fit() before Transform() can be called.");
        }

        var vector = new Dictionary<string, double>();
        var tokens = Tokenize(document);
        if (tokens.Count == 0 || _corpusSize == 0)
        {
            return vector;
        }

        var termCounts = new Dictionary<string, int>();
        foreach (var token in tokens)
        {
            termCounts[token] = termCounts.TryGetValue(token, out var count) ? count + 1 : 1;
        }

        foreach (var (term, count) in termCounts)
        {
            var tf = (double)count / tokens.Count;
            _documentFrequencies.TryGetValue(term, out var df);
            var idf = Math.Log((double)_corpusSize / (1 + df)) + 1.0;
            vector[term] = tf * idf;
        }

        var norm = Math.Sqrt(vector.Values.Sum(v => v * v));
        if (norm > 0.0)
        {
            foreach (var term in vector.Keys.ToList())
            {
                vector[term] /= norm;
            }
        }

        return vector;
    }

    private static List<string> Tokenize(string document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return new List<string>();
        }

        var cleaned = new StringBuilder(document.Length);
        foreach (var ch in document)
        {
            cleaned.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        return cleaned.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3 && !Stopwords.Contains(token))
            .ToList();
    }
}
