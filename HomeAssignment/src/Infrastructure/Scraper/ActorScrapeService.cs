using HomeAssignment.Domain.Models;
using HomeAssignment.Infrastructure.Scraper.Providers;

namespace HomeAssignment.Infrastructure.Scraper;

public class ActorScrapeService
{
    private readonly IActorSourceProvider _scraperService;
    private readonly RankGenerator _rankGenerator;
    private readonly string _sourceName;
    private readonly HashSet<string> _seenExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _rankExhaustedLogged;

    public ActorScrapeService(
        IActorSourceProvider scraperService,
        RankGenerator rankGenerator,
        string sourceName)
    {
        _scraperService = scraperService;
        _rankGenerator = rankGenerator;
        _sourceName = sourceName;
    }

    public IEnumerable<MovieCast> ScrapeTopMoviesCast(int movieCount)
    {
        var movies = _scraperService.GetTopMovies(movieCount).ToList();
        if (movies.Count == 0)
        {
            Console.WriteLine("No movies found on the Top Chart page.");
            yield break;
        }

        Console.WriteLine($"Fetching Top {movieCount} Movies and their Cast...\n");

        foreach (var movie in movies)
        {
            Console.WriteLine($"MOVIE: {movie.Title}");
            Console.WriteLine($"   -> Scraping Cast from: {movie.FullCreditsUrl}");

            var actors = _scraperService.GetTopActors(movie.FullCreditsUrl, int.MaxValue);
            if (actors.Count == 0)
            {
                Console.WriteLine("   -> No actors found in cast section.");
                Console.WriteLine("------------------------------------------------");
                yield return new MovieCast(movie.Title, movie.FullCreditsUrl, new List<ActorRecord>());
                continue;
            }

            var records = new List<ActorRecord>();
            foreach (var actor in actors)
            {
                if (string.IsNullOrWhiteSpace(actor.Name) || string.IsNullOrWhiteSpace(actor.Id))
                {
                    continue;
                }

                if (!_seenExternalIds.Add(actor.Id))
                {
                    continue;
                }

                var rank = _rankGenerator.TryGetNextRank();
                if (rank == null && !_rankExhaustedLogged)
                {
                    Console.WriteLine("   -> Rank pool exhausted. Remaining actors will have no rank.");
                    _rankExhaustedLogged = true;
                }

                records.Add(new ActorRecord(actor.Id, actor.Name, rank, _sourceName));
            }

            yield return new MovieCast(movie.Title, movie.FullCreditsUrl, records);
        }
    }
}

public record MovieCast(string Title, string CreditsUrl, List<ActorRecord> Actors);