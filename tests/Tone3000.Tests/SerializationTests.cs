using System.Text.Json;
using Tone3000.Models;
using Tone3000.Serialization;

namespace Tone3000.Tests;

public class SerializationTests
{
    [Fact]
    public void Tone_deserializes_snake_case_enums_and_nulls()
    {
        var tone = JsonSerializer.Deserialize(Fixtures.ToneJson, Tone3000JsonContext.Default.Tone)!;

        Assert.Equal(42, tone.Id);
        Assert.Equal("fuzzface", tone.User.Name);
        Assert.True(tone.User.IsVerified);
        Assert.Equal(Gear.AmpCab, tone.Gear);
        Assert.Equal(ToneFormat.Nam, tone.Format);
        Assert.Equal(License.Cc0, tone.License);
        Assert.Null(tone.PublishedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), tone.CreatedAt);
        Assert.Equal([ModelSize.Standard, ModelSize.Nano, ModelSize.Unknown], tone.Sizes);
        Assert.Equal("Marshall", tone.Makes[0].Name);
        Assert.Equal(3, tone.A2ModelsCount);
    }

    [Fact]
    public void Unknown_enum_values_do_not_throw()
    {
        var json = Fixtures.ToneJson.Replace("\"amp-cab\"", "\"synth\"", StringComparison.Ordinal);
        var tone = JsonSerializer.Deserialize(json, Tone3000JsonContext.Default.Tone)!;
        Assert.Equal(Gear.Unknown, tone.Gear);
    }

    [Theory]
    [InlineData("\"2\"", Architecture.A2)]
    [InlineData("2", Architecture.A2)]
    [InlineData("\"1\"", Architecture.A1)]
    [InlineData("\"custom\"", Architecture.Custom)]
    [InlineData("null", null)]
    public void Model_architecture_reads_string_number_or_null(string wire, Architecture? expected)
    {
        var json = $$"""
        { "id": 9, "tone_id": 42, "user_id": 7, "created_at": "2026-10-01T12:00:00Z", "updated_at": "2026-10-01T12:00:00Z",
          "model_url": "https://www.tone3000.com/api/v1/models/9/file", "name": "Plexi A2", "size": "standard",
          "architecture_version": {{wire}}, "content_hash": "nam-content-v1:abc",
          "metadata": { "gain": 0.5, "calibrated": true, "rig": "Plexi" } }
        """;

        var model = JsonSerializer.Deserialize(json, Tone3000JsonContext.Default.Model)!;

        Assert.Equal(expected, model.ArchitectureVersion);
        Assert.Equal(0.5, model.Metadata!["gain"].GetDouble());
        Assert.True(model.Metadata["calibrated"].GetBoolean());
    }

    [Fact]
    public void Favorite_user_id_is_a_string()
    {
        var fav = JsonSerializer.Deserialize("""{ "id": 1, "tone_id": 42, "user_id": "7", "created_at": "2026-11-01T00:00:00Z" }""",
            Tone3000JsonContext.Default.Favorite)!;
        Assert.Equal("7", fav.UserId);
    }

    [Fact]
    public void Wire_values_round_trip()
    {
        Assert.Equal("downloads-all-time", WireValues.ToWire(TonesSort.DownloadsAllTime));
        Assert.Equal("1", WireValues.ToWire(Architecture.A1));
        Assert.Equal(License.CcByNcSa, WireValues.FromWire<License>("cc-by-nc-sa"));
        Assert.Throws<ArgumentOutOfRangeException>(() => WireValues.ToWire(Gear.Unknown));
    }
}
