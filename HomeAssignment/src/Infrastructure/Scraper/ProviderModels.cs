namespace HomeAssignment.Infrastructure.Scraper;

/// <summary>
/// Shared provider models.
/// These are used by all implementations of <see cref="Providers.IActorSourceProvider"/>.
/// </summary>
public record MovieInfo(string Title, string MovieUrl, string FullCreditsUrl);

public record ActorEntry(string Name, string Id);

