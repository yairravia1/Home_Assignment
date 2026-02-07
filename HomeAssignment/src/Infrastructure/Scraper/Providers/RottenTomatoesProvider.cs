using HtmlAgilityPack;
using System.Text.Json;

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
    private readonly HtmlWeb _htmlWeb;

    public RottenTomatoesProvider(string bestMoviesUrl)
    {
        _bestMoviesUrl = bestMoviesUrl;
        _htmlWeb = new HtmlWeb
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

        var htmlDocument = LoadDocument(_bestMoviesUrl);

        // The editorial page renders a big table; each movie is usually linked with <a class="title" href="https://www.rottentomatoes.com/m/...">.
        var movieLinkNodes =
            htmlDocument.DocumentNode.SelectNodes(
                "//div[contains(@class,'articleContentBody')]//table//a[contains(@class,'title') and contains(@href,'rottentomatoes.com/m/')]")
            ?? htmlDocument.DocumentNode.SelectNodes(
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

        var htmlDocument = LoadDocument(fullCreditsUrl);

        // Rotten Tomatoes pages often render cast via embedded JSON (no plain <a href="/celebrity/..."> links in HTML).
        // Prefer parsing Schema.org JSON-LD which includes the cast list.
        var ldJsonActors = TryGetActorsFromLdJson(htmlDocument, count);
        if (ldJsonActors.Count > 0)
        {
            return ldJsonActors;
        }

        // Cast & Crew pages include both "Cast" and "Crew" entries. We want ACTORS only.
        // We start from links to "/celebrity/..." and keep only those whose nearby text includes "Actor".
        var celebrityLinks = htmlDocument.DocumentNode.SelectNodes("//a[contains(@href,'/celebrity/')]");
        if (celebrityLinks == null || celebrityLinks.Count == 0)
        {
            return new List<ActorEntry>();
        }

        return celebrityLinks
            .Select(link =>
            {
                // Some RT markup uses images/spans and has empty InnerText; fall back to attributes.
                var name =
                    HtmlEntity.DeEntitize(link.InnerText ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = HtmlEntity.DeEntitize(link.GetAttributeValue("aria-label", "")).Trim();
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = HtmlEntity.DeEntitize(link.GetAttributeValue("title", "")).Trim();
                }

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

    private static List<ActorEntry> TryGetActorsFromLdJson(HtmlDocument doc, int count)
    {
        try
        {
            var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (scripts == null || scripts.Count == 0)
            {
                return new List<ActorEntry>();
            }

            var results = new List<ActorEntry>();

            foreach (var script in scripts)
            {
                var json = script.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(json);
                foreach (var obj in EnumerateObjects(document.RootElement))
                {
                    // We only care about Movie objects that have an "actor" list.
                    if (!TryGetStringProperty(obj, "@type", out var type) ||
                        !type.Equals("Movie", StringComparison.OrdinalIgnoreCase))
                    {
                        // Some pages may omit @type or use arrays; if it still has "actor", accept it.
                        if (!obj.TryGetProperty("actor", out _))
                        {
                            continue;
                        }
                    }

                    if (!obj.TryGetProperty("actor", out var actorProp) || actorProp.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var actor in actorProp.EnumerateArray())
                    {
                        if (actor.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var name = "";
                        if (TryGetStringProperty(actor, "name", out var parsedName))
                        {
                            name = parsedName.Trim();
                        }

                        var url = TryGetUrlFromActor(actor);
                        var id = ExtractCelebrityId(url);

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        results.Add(new ActorEntry(name, id));
                        if (results.Count >= count)
                        {
                            break;
                        }
                    }

                    if (results.Count >= count)
                    {
                        break;
                    }
                }

                if (results.Count >= count)
                {
                    break;
                }
            }

            return results
                .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(count)
                .ToList();
        }
        catch
        {
            // If JSON-LD parsing fails, fall back to DOM scraping.
            return new List<ActorEntry>();
        }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Object)
                {
                    yield return el;
                }
            }
        }
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = "";
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        if (prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString() ?? "";
        return true;
    }

    private static string TryGetUrlFromActor(JsonElement actor)
    {
        // RT commonly uses "sameAs" for person profile URL.
        if (TryGetStringProperty(actor, "sameAs", out var sameAs))
        {
            return sameAs;
        }

        if (actor.TryGetProperty("sameAs", out var sameAsProp) && sameAsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in sameAsProp.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString() ?? "";
                    if (s.Contains("/celebrity/", StringComparison.OrdinalIgnoreCase))
                    {
                        return s;
                    }
                }
            }
        }

        if (TryGetStringProperty(actor, "url", out var url))
        {
            return url;
        }

        return "";
    }

    private HtmlDocument LoadDocument(string url)
    {
        var htmlDocument = _htmlWeb.Load(url);
        Thread.Sleep(1000); // Be polite to the server
        return htmlDocument;
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

