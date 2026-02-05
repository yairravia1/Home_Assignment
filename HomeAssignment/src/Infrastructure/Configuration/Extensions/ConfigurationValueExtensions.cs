using Microsoft.Extensions.Configuration;

namespace HomeAssignment.Infrastructure.Configuration.Extensions;

/// <summary>
/// Central place for reading required config values.
/// Keeps configuration extensions DRY and consistent.
/// </summary>
public static class ConfigurationValueExtensions
{
    public static string GetRequiredString(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing configuration value: {key}");
        }

        return value;
    }

    public static int GetRequiredInt(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!int.TryParse(value, out var result))
        {
            throw new InvalidOperationException($"Invalid integer configuration value: {key}");
        }

        return result;
    }
}

