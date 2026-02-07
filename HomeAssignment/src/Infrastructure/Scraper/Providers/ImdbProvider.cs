using HtmlAgilityPack;

namespace HomeAssignment.Infrastructure.Scraper.Providers;

/// <summary>
/// IMDb provider:
/// - Top list source: IMDb Top chart (with a fallback to the "simple" HTML view).
/// - Movie cast source: each movie's "/fullcredits" page.
/// </summary>
public sealed class ImdbProvider : IActorSourceProvider
{
    private readonly string _topChartUrl;
    private readonly string _topChartSimpleUrl;
    private readonly Uri _baseUri;
    private readonly HtmlWeb _htmlWeb;

    public ImdbProvider(string topChartUrl, string topChartSimpleUrl)
    {
        _topChartUrl = topChartUrl;
        _topChartSimpleUrl = topChartSimpleUrl;
        _baseUri = new Uri(topChartUrl);
        _htmlWeb = new HtmlWeb
        {
            // User agent to mimic a real browser
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36"
        };
    }

    public IEnumerable<MovieInfo> GetTopMovies(int count)
    {
        var htmlDoc = LoadDocument(_topChartUrl);
        var movies = ExtractMoviesFromDom(htmlDoc).ToList();
        if (movies.Count >= count)
        {
            return movies.Take(count);
        }

        // Fallback to the classic/simple HTML table that renders all 250 entries.
        var simpleDoc = LoadDocument(_topChartSimpleUrl);
        var simpleMovies = ExtractMoviesFromSimpleTable(simpleDoc).ToList();
        return simpleMovies.Count > 0 ? simpleMovies.Take(count) : movies.Take(count);
    }

    public List<ActorEntry> GetTopActors(string fullCreditsUrl, int count)
    {
        var castDoc = LoadDocument(fullCreditsUrl);

        var actorEntries = ExtractActorEntriesFromCreditsList(castDoc).ToList();
        if (actorEntries.Count == 0)
        {
            actorEntries = ExtractActorEntriesFromCastTable(castDoc).ToList();
        }
        if (actorEntries.Count == 0)
        {
            actorEntries = ExtractActorEntriesFromNewLayout(castDoc).ToList();
        }

        return actorEntries
            .Where(actor => actor != null && !string.IsNullOrWhiteSpace(actor.Name))
            .Where(actor => !string.IsNullOrWhiteSpace(actor.Id))
            .GroupBy(actor => actor.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(count)
            .ToList();
    }

    private static IEnumerable<ActorEntry> ExtractActorEntriesFromCreditsList(HtmlDocument doc)
    {
        var listItems = doc.DocumentNode.SelectNodes("//li[@data-testid='name-credits-list-item']");
        if (listItems == null)
        {
            return Enumerable.Empty<ActorEntry>();
        }

        return listItems
            .Select(item =>
            {
                var nameLink = item.SelectSingleNode(".//a[contains(@class, 'name-credits--title-text')]");
                var href = nameLink?.GetAttributeValue("href", "") ?? "";
                var id = ExtractActorId(href);
                var name = nameLink?.InnerText ?? "";
                return new ActorEntry(HtmlEntity.DeEntitize(name).Trim(), id);
            })
            .Where(actor => !string.IsNullOrWhiteSpace(actor.Name));
    }

    private HtmlDocument LoadDocument(string url)
    {
        var htmlDocument = _htmlWeb.Load(url);
        Thread.Sleep(1000); // Be polite to the server
        return htmlDocument;
    }

    private string BuildAbsoluteUrl(string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return "";
        }

        if (relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return relativeUrl;
        }

        return new Uri(_baseUri, relativeUrl).ToString();
    }

    private static string BuildFullCreditsUrl(string movieUrl)
    {
        if (string.IsNullOrWhiteSpace(movieUrl))
        {
            return "";
        }

        var movieUri = new Uri(movieUrl);
        var fullCreditsUri = new Uri(movieUri, "fullcredits");
        return fullCreditsUri.ToString();
    }

    private IEnumerable<MovieInfo> ExtractMoviesFromDom(HtmlDocument doc)
    {
        var movieNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'ipc-metadata-list-summary-item')]");
        if (movieNodes == null)
        {
            return Enumerable.Empty<MovieInfo>();
        }

        return movieNodes
            .Select(node =>
            {
                var titleNode = node.SelectSingleNode(".//h3[contains(@class, 'ipc-title__text')]");
                var linkNode = node.SelectSingleNode(".//a[contains(@class, 'ipc-title-link-wrapper')]");

                var title = HtmlEntity.DeEntitize(titleNode?.InnerText ?? "Unknown Title").Trim();
                var relativeUrl = linkNode?.GetAttributeValue("href", "") ?? "";
                var fullMovieUrl = BuildAbsoluteUrl(relativeUrl);
                var fullCreditsUrl = BuildFullCreditsUrl(fullMovieUrl);

                return new MovieInfo(title, fullMovieUrl, fullCreditsUrl);
            })
            .Where(movie => !string.IsNullOrWhiteSpace(movie.MovieUrl))
            .GroupBy(movie => movie.MovieUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private IEnumerable<MovieInfo> ExtractMoviesFromSimpleTable(HtmlDocument doc)
    {
        var rows = doc.DocumentNode.SelectNodes("//tbody[contains(@class, 'lister-list')]/tr")
            ?? doc.DocumentNode.SelectNodes("//table[contains(@class, 'chart')]//tr");

        if (rows == null)
        {
            return Enumerable.Empty<MovieInfo>();
        }

        return rows
            .Select(row =>
            {
                var linkNode = row.SelectSingleNode(".//td[contains(@class, 'titleColumn')]/a");
                var title = HtmlEntity.DeEntitize(linkNode?.InnerText ?? "Unknown Title").Trim();
                var relativeUrl = linkNode?.GetAttributeValue("href", "") ?? "";
                var fullMovieUrl = BuildAbsoluteUrl(relativeUrl);
                var fullCreditsUrl = BuildFullCreditsUrl(fullMovieUrl);

                return new MovieInfo(title, fullMovieUrl, fullCreditsUrl);
            })
            .Where(movie => !string.IsNullOrWhiteSpace(movie.MovieUrl))
            .GroupBy(movie => movie.MovieUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private static IEnumerable<ActorEntry> ExtractActorEntriesFromCastTable(HtmlDocument doc)
    {
        var castTable = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'cast_list')]");
        if (castTable == null)
        {
            return Enumerable.Empty<ActorEntry>();
        }

        // The actor name is typically in the first <td> AFTER the photo cell.
        var nameLinks = castTable.SelectNodes(
            ".//tr[.//td[contains(@class, 'primary_photo')]]" +
            "/td[contains(@class, 'primary_photo')]/following-sibling::td[1]//a[1]");

        if (nameLinks == null || nameLinks.Count == 0)
        {
            // Fallback: look for the first link in the 2nd cell of each row.
            nameLinks = castTable.SelectNodes(".//tr/td[2]//a[1]");
        }

        if (nameLinks == null)
        {
            return Enumerable.Empty<ActorEntry>();
        }

        return nameLinks
            .Select(link =>
            {
                var name = link.InnerText ?? "";
                var href = link.GetAttributeValue("href", "");
                var id = ExtractActorId(href);
                return new ActorEntry(HtmlEntity.DeEntitize(name).Trim(), id);
            })
            .Where(actor => !string.IsNullOrWhiteSpace(actor.Name));
    }

    private static IEnumerable<ActorEntry> ExtractActorEntriesFromNewLayout(HtmlDocument doc)
    {
        var castHeader = doc.DocumentNode.SelectSingleNode("//h4[@id='cast']");
        if (castHeader == null)
        {
            return Enumerable.Empty<ActorEntry>();
        }

        var listNode = castHeader.SelectSingleNode("following-sibling::ul[1] | following-sibling::ol[1]");
        var listItems = listNode?.SelectNodes(".//li")
            ?? castHeader.SelectNodes("following-sibling::li");

        if (listItems == null)
        {
            return Enumerable.Empty<ActorEntry>();
        }

        return listItems
            .Select(item =>
            {
                var nameLink = item.SelectSingleNode(".//a");
                var href = nameLink?.GetAttributeValue("href", "") ?? "";
                var id = ExtractActorId(href);
                var name = nameLink?.InnerText ?? "";
                return new ActorEntry(HtmlEntity.DeEntitize(name).Trim(), id);
            })
            .Where(actor => !string.IsNullOrWhiteSpace(actor.Name));
    }

    private static string ExtractActorId(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "";
        }

        var marker = "/name/";
        var startIndex = href.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return "";
        }

        startIndex += marker.Length;
        var endIndex = href.IndexOf('/', startIndex);
        if (endIndex < 0)
        {
            endIndex = href.IndexOf('?', startIndex);
        }

        if (endIndex < 0)
        {
            endIndex = href.Length;
        }

        return href.Substring(startIndex, endIndex - startIndex);
    }
}

