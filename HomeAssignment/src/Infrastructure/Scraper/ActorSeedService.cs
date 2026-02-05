using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Enums;
using HomeAssignment.Infrastructure.Scraper.Configuration;
using HomeAssignment.Infrastructure.Scraper.Providers;

namespace HomeAssignment.Infrastructure.Scraper;

public class ActorSeedService
{
    private readonly IActorIngestionRepository _repository;
    private readonly ScraperSettings _settings;
    private readonly IActorSourceProvider _scraperService;

    public ActorSeedService(
        IActorIngestionRepository repository,
        IActorSourceProvider scraperService,
        ScraperSettings settings)
    {
        _repository = repository;
        _scraperService = scraperService;
        _settings = settings;
    }

    public void SeedTopChartActors()
    {
        if (!_repository.CanConnect())
        {
            Console.WriteLine("MongoDB is not reachable. Skipping scrape.");
            return;
        }

        var usedRanks = _repository.GetAssignedRanks(_settings.SourceName);
        var rankGenerator = new RankGenerator(usedRanks, _settings.MaxRank);
        var scrapeService = new ActorScrapeService(_scraperService, rankGenerator, _settings.SourceName);

        foreach (var movieCast in scrapeService.ScrapeTopMoviesCast(_settings.MovieCount))
        {
            if (movieCast.Actors.Count == 0)
            {
                continue;
            }

            var result = _repository.SaveActors(movieCast.Actors, SaveBehavior.SkipExisting);
            Console.WriteLine(
                $"   -> Stored {result.Inserted} new (attempted {result.Attempted}).");
            Console.WriteLine("------------------------------------------------");
        }
    }
}
