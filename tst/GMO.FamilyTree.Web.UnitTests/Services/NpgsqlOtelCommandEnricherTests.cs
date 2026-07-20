using System.Diagnostics;

using GMO.FamilyTree.Web.Services;

using Npgsql;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class NpgsqlOtelCommandEnricherTests
{
    [Fact]
    public void FormatParameterValue_null_is_sentinel()
    {
        Assert.Equal("null", NpgsqlOtelCommandEnricher.FormatParameterValue(null));
        Assert.Equal("null", NpgsqlOtelCommandEnricher.FormatParameterValue(DBNull.Value));
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData(42, "42")]
    [InlineData(42L, "42")]
    [InlineData(3.5, "3.5")]
    public void FormatParameterValue_scalars_are_invariant_plaintext(object value, string expected)
    {
        Assert.Equal(expected, NpgsqlOtelCommandEnricher.FormatParameterValue(value));
    }

    [Fact]
    public void FormatParameterValue_guid_and_dates_are_plaintext()
    {
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", NpgsqlOtelCommandEnricher.FormatParameterValue(guid));

        var date = new DateOnly(2020, 7, 19);
        Assert.Equal("2020-07-19", NpgsqlOtelCommandEnricher.FormatParameterValue(date));
    }

    [Fact]
    public void FormatParameterValue_string_is_sha256_hex_without_plaintext()
    {
        const string email = "user@example.com";
        var tagged = NpgsqlOtelCommandEnricher.FormatParameterValue(email);

        Assert.Equal(LogValueHasher.Hash(email), tagged);
        Assert.DoesNotContain("user@example.com", tagged, StringComparison.Ordinal);
        Assert.DoesNotContain("@", tagged, StringComparison.Ordinal);
        Assert.Equal(64, tagged.Length);
    }

    [Fact]
    public void FormatParameterValue_bytes_are_sha256_hex()
    {
        byte[] bytes = [1, 2, 3, 4];
        Assert.Equal(LogValueHasher.Hash(bytes), NpgsqlOtelCommandEnricher.FormatParameterValue(bytes));
    }

    [Fact]
    public void Enrich_sets_statement_and_parameter_tags()
    {
        using var activity = new Activity("test");
        activity.Start();

        using var command = new NpgsqlCommand("SELECT * FROM t WHERE id = @id AND email = @email");
        command.Parameters.AddWithValue("id", 7L);
        command.Parameters.AddWithValue("email", "secret@example.com");

        NpgsqlOtelCommandEnricher.Enrich(activity, command);

        Assert.Equal(command.CommandText, activity.GetTagItem(NpgsqlOtelCommandEnricher.StatementTag));
        Assert.Equal("7", activity.GetTagItem(NpgsqlOtelCommandEnricher.ParameterTagPrefix + "id"));
        Assert.Equal(
            LogValueHasher.Hash("secret@example.com"),
            activity.GetTagItem(NpgsqlOtelCommandEnricher.ParameterTagPrefix + "email"));
        Assert.DoesNotContain(
            "secret@example.com",
            string.Join('|', activity.Tags.Select(t => $"{t.Key}={t.Value}")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_null_activity_is_noop()
    {
        using var command = new NpgsqlCommand("SELECT 1");
        NpgsqlOtelCommandEnricher.Enrich(null, command);
    }
}

public class LogValueHasherTests
{
    [Fact]
    public void Hash_string_matches_known_sha256_hex()
    {
        // SHA-256("abc") uppercase hex
        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            LogValueHasher.Hash("abc"));
    }
}