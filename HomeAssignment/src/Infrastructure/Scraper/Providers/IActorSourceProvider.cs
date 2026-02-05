namespace HomeAssignment.Infrastructure.Scraper.Providers;

public interface IActorSourceProvider
{
    IEnumerable<MovieInfo> GetTopMovies(int count);
    List<ActorEntry> GetTopActors(string fullCreditsUrl, int count);
}
