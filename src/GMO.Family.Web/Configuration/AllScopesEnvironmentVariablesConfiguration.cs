using System.Collections;

namespace GMO.Family.Web.Configuration;

/// <summary>
/// Loads environment variables from Process, User, and Machine (Windows System) so that
/// <see cref="IConfiguration"/> can read them without per-variable helpers. Precedence: Process over User over Machine.
/// </summary>
public sealed class AllScopesEnvironmentVariablesConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new AllScopesEnvironmentVariablesConfigurationProvider();
}

internal sealed class AllScopesEnvironmentVariablesConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Machine (Windows System) first, then User, then Process so that Process wins.
        var targets = new[]
        {
            EnvironmentVariableTarget.Machine,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Process
        };

        foreach (var target in targets)
        {
            try
            {
                var vars = Environment.GetEnvironmentVariables(target);
                if (vars == null) continue;
                foreach (DictionaryEntry entry in vars)
                {
                    var key = entry.Key?.ToString();
                    var value = entry.Value?.ToString();
                    if (!string.IsNullOrEmpty(key))
                        data[key] = value;
                }
            }
            catch
            {
                // Ignore; e.g. Machine scope may not be readable in some contexts.
            }
        }

        Data = data;
    }
}
