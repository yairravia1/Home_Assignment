namespace HomeAssignment.Infrastructure.Configuration.Settings.ScraperSettings;

public record ScraperSettings(
    string Provider,
    string TopChartUrl,
    string TopChartSimpleUrl,
    string RottenTomatoesBestMoviesUrl,
    int MovieCount,
    int MaxRank,
    string SourceName);
