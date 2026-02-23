namespace GMO.Family.Web.Configuration;

/// <summary>
/// Extension methods for <see cref="IConfiguration"/>.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Returns the first non-null, non-whitespace value from the given configuration keys.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="keys">Keys to try in order (e.g. "Authentication:Google:ClientId", "GOOGLE_CLIENT_ID").</param>
    /// <returns>The first value that is not null or whitespace, or null if none.</returns>
    public static string? GetFirstNonEmpty(this IConfiguration configuration, params string[] keys)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (keys == null || keys.Length == 0) return null;

        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}