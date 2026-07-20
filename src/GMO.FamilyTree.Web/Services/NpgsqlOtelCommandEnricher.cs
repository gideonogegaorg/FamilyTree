using System.Diagnostics;
using System.Globalization;

using Npgsql;

namespace GMO.FamilyTree.Web.Services;

/// <summary>
/// Adds <c>db.statement</c> and safe parameter tags for Npgsql OTel command enrichment.
/// Numeric/scalar values are tagged as invariant strings; strings and other blobs are SHA-256 hex only.
/// </summary>
public static class NpgsqlOtelCommandEnricher
{
    public const string StatementTag = "db.statement";
    public const string ParameterTagPrefix = "db.statement.parameter.";

    public static void Enrich(Activity? activity, NpgsqlCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (activity is null)
            return;

        activity.SetTag(StatementTag, command.CommandText);

        var index = 0;
        foreach (NpgsqlParameter parameter in command.Parameters)
        {
            var name = string.IsNullOrEmpty(parameter.ParameterName)
                ? $"p{index}"
                : parameter.ParameterName;
            activity.SetTag(ParameterTagPrefix + name, FormatParameterValue(parameter.Value));
            index++;
        }
    }

    /// <summary>Formats a bind value for an OTel tag (safe for PII-bearing strings).</summary>
    public static string FormatParameterValue(object? value)
    {
        if (value is null || value is DBNull)
            return "null";

        return value switch
        {
            bool b => b ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null",
            Guid g => g.ToString("D", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("O", CultureInfo.InvariantCulture),
            Enum e => Convert.ToString(Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                ?? "null",
            string s => LogValueHasher.Hash(s),
            char[] chars => LogValueHasher.Hash(new string(chars)),
            byte[] bytes => LogValueHasher.Hash(bytes),
            _ => LogValueHasher.Hash(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().FullName ?? "unknown")
        };
    }
}