using HtmlAgilityPack;

namespace HomeAssignment.Infrastructure.Scraper.Providers;

/// <summary>
/// Rotten Tomatoes provider:
/// - Top list source: Rotten Tomatoes Editorial "Best Movies of All Time" page.
/// - Movie cast source: each movie's "/cast-and-crew" page.
/// </summary>
public sealed class RottenTomatoesProvider : IActorSourceProvider
{
    private static readonly Uri RottenTomatoesBaseUri = new("https://www.rottentomatoes.com/");

    private readonly string _bestMoviesUrl;
    private readonly HtmlWeb _web;

    public RottenTomatoesProvider(string bestMoviesUrl)
    {
        _bestMoviesUrl = bestMoviesUrl;
        _web = new HtmlWeb
        {
            // User agent to mimic a real browser (some sites behave differently without one).
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36"
        };
    }

    public IEnumerable<MovieInfo> GetTopMovies(int count)
    {
        if (count <= 0)
        {
            return Enumerable.Empty<MovieInfo>();
        }

        var doc = LoadDocument(_bestMoviesUrl);

        // The editorial page renders a big table; each movie is usually linked with <a class="title" href="https://www.rottentomatoes.com/m/...">.
        var movieLinkNodes =
            doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'articleContentBody')]//table//a[contains(@class,'title') and contains(@href,'rottentomatoes.com/m/')]")
            ?? doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'articleContentBody')]//a[contains(@href,'rottentomatoes.com/m/') and not(contains(@href,'/celebrity/'))]");

        if (movieLinkNodes == null || movieLinkNodes.Count == 0)
        {
            return Enumerable.Empty<MovieInfo>();
        }

        return movieLinkNodes
            .Select(link =>
            {
                var title = HtmlEntity.DeEntitize(link.InnerText ?? "").Trim();
                var movieUrl = link.GetAttributeValue("href", "").Trim();
                if (string.IsNullOrWhiteSpace(movieUrl))
                {
                    return null;
                }

                var castAndCrewUrl = BuildCastAndCrewUrl(movieUrl);
                return new MovieInfo(title, movieUrl, castAndCrewUrl);
            })
            .Where(movie => movie != null && !string.IsNullOrWhiteSpace(movie.MovieUrl))
            .GroupBy(movie => movie!.MovieUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()!)
            .Take(count);
    }

    public List<ActorEntry> GetTopActors(string fullCreditsUrl, int count)
    {
        if (count <= 0)
        {
            return new List<ActorEntry>();
        }

        var doc = LoadDocument(fullCreditsUrl);

        // Cast & Crew pages include both "Cast" and "Crew" entries. We want ACTORS only.
        // We start from links to "/celebrity/..." and keep only those whose nearby text includes "Actor".
        var celebrityLinks = doc.DocumentNode.SelectNodes("//a[contains(@href,'/celebrity/')]");
        if (celebrityLinks == null || celebrityLinks.Count == 0)
        {
            return new List<ActorEntry>();
        }

        return celebrityLinks
            .Select(link =>
            {
                var name = HtmlEntity.DeEntitize(link.InnerText ?? "").Trim();
                var href = link.GetAttributeValue("href", "").Trim();
                var id = ExtractCelebrityId(href);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                {
                    return null;
                }

                // Role type is usually rendered near the name ("Actor", "Director", "Writer", ...).
                // We scan a small ancestor window to classify.
                var contextText = GetContextText(link, maxAncestorLevels: 4);
                if (!contextText.Contains("Actor", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return new ActorEntry(name, id);
            })
            .Where(a => a != null)
            .GroupBy(a => a!.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()!)
            .Take(count)
            .ToList();
    }

    private HtmlDocument LoadDocument(string url)
    {
        var doc = _web.Load(url);
        Thread.Sleep(1000); // Be polite to the server
        return doc;
    }

    private static string BuildCastAndCrewUrl(string movieUrl)
    {
        if (string.IsNullOrWhiteSpace(movieUrl))
        {
            return "";
        }

        if (movieUrl.Contains("/cast-and-crew", StringComparison.OrdinalIgnoreCase))
        {
            return movieUrl;
        }

        return movieUrl.TrimEnd('/') + "/cast-and-crew";
    }

    private static string ExtractCelebrityId(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "";
        }

        // Examples:
        // - /celebrity/marlon_brando
        // - https://www.rottentomatoes.com/celebrity/marlon_brando
        var relative = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(href).AbsolutePath
            : href;

        const string marker = "/celebrity/";
        var startIndex = relative.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return "";
        }

        startIndex += marker.Length;
        var endIndex = relative.IndexOf('/', startIndex);
        if (endIndex < 0)
        {
            endIndex = relative.IndexOf('?', startIndex);
        }
        if (endIndex < 0)
        {
            endIndex = relative.Length;
        }

        return relative.Substring(startIndex, endIndex - startIndex);
    }

    private static string GetContextText(HtmlNode node, int maxAncestorLevels)
    {
        var current = node;
        for (var i = 0; i < maxAncestorLevels && current.ParentNode != null; i++)
        {
            current = current.ParentNode;
        }

        var text = HtmlEntity.DeEntitize(current.InnerText ?? "");
        return text.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }
}

