namespace HomeAssignment.Infrastructure.Scraper.Configuration;

public record ScraperSettings(
    string Provider,
    string TopChartUrl,
    string TopChartSimpleUrl,
    string RottenTomatoesBestMoviesUrl,
    int MovieCount,
    int MaxRank,
    string SourceName);
