using System.Text.Json;
using Loggy.Models;
using Loggy.Models.Gemini;

namespace Loggy.Models.Tests;

public class SeriLogEventTests
{
    // -------------------------------------------------------------------------
    // Default values
    // -------------------------------------------------------------------------

    [Fact]
    public void SeriLogEvent_DefaultValues_AreCorrect()
    {
        var e = new SeriLogEvent();

        Assert.Equal(0, e.Id);
        Assert.Equal(default, e.Timestamp);
        Assert.Equal(string.Empty, e.Level);
        Assert.Equal(string.Empty, e.Message);
        Assert.Null(e.Exception);
        Assert.Null(e.Source);
        Assert.Null(e.TraceId);
        Assert.NotNull(e.Properties);
        Assert.Empty(e.Properties);
    }

    // -------------------------------------------------------------------------
    // JSON serialisation — round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void SeriLogEvent_RoundTrip_PreservesAllFields()
    {
        var original = new SeriLogEvent
        {
            Id = 42,
            Timestamp = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero),
            Level = "Error",
            Message = "Something failed",
            Exception = "NullReferenceException",
            Source = "MyService",
            TraceId = "abc-123"
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SeriLogEvent>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Timestamp, restored.Timestamp);
        Assert.Equal(original.Level, restored.Level);
        Assert.Equal(original.Message, restored.Message);
        Assert.Equal(original.Exception, restored.Exception);
        Assert.Equal(original.Source, restored.Source);
        Assert.Equal(original.TraceId, restored.TraceId);
    }

    [Fact]
    public void SeriLogEvent_Deserialise_CaseInsensitive()
    {
        // Serilog CLEF uses PascalCase
        var json = """
            {
              "Timestamp": "2024-01-01T09:00:00+00:00",
              "Level": "Warning",
              "Message": "Low disk space",
              "Exception": null
            }
            """;

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var e = JsonSerializer.Deserialize<SeriLogEvent>(json, opts);

        Assert.NotNull(e);
        Assert.Equal("Warning", e.Level);
        Assert.Equal("Low disk space", e.Message);
        Assert.Null(e.Exception);
    }

    [Fact]
    public void SeriLogEvent_Deserialise_NullableFieldsMissingFromJson_AreNull()
    {
        var json = """{"level":"Info","message":"ok"}""";
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var e = JsonSerializer.Deserialize<SeriLogEvent>(json, opts);

        Assert.Null(e!.Exception);
        Assert.Null(e.Source);
        Assert.Null(e.TraceId);
    }

    [Fact]
    public void SeriLogEvent_Deserialise_TimestampWithOffset_Preserved()
    {
        var json = """{"timestamp":"2024-03-10T08:30:00+05:30","level":"Info","message":"x"}""";
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var e = JsonSerializer.Deserialize<SeriLogEvent>(json, opts);

        Assert.Equal(new TimeSpan(5, 30, 0), e!.Timestamp.Offset);
        Assert.Equal(8, e.Timestamp.Hour);
    }

    [Fact]
    public void SeriLogEvent_Properties_CanHoldArbitraryJsonElements()
    {
        var json = """
            {
              "level": "Debug",
              "message": "handled",
              "properties": {
                "userId": 99,
                "tags": ["a","b"],
                "nested": {"ok": true}
              }
            }
            """;

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var e = JsonSerializer.Deserialize<SeriLogEvent>(json, opts);

        Assert.Equal(3, e!.Properties.Count);
        Assert.True(e.Properties.ContainsKey("userId"));
    }
}

public class EnumsTests
{
    [Theory]
    [InlineData(0, Enums.SchemaTypes.Serilog)]
    [InlineData(1, Enums.SchemaTypes.NLog)]
    [InlineData(2, Enums.SchemaTypes.Log4Net)]
    public void SchemaTypes_IntToEnum_Parses(int value, Enums.SchemaTypes expected)
    {
        var result = Enum.Parse<Enums.SchemaTypes>(value.ToString());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, Enums.SortOptions.ByException)]
    [InlineData(1, Enums.SortOptions.ByTimeStamp)]
    public void SortOptions_IntToEnum_Parses(int value, Enums.SortOptions expected)
    {
        var result = Enum.Parse<Enums.SortOptions>(value.ToString());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, Enums.ModelOptions.Gemini)]
    [InlineData(1, Enums.ModelOptions.Custom)]
    public void ModelOptions_IntToEnum_Parses(int value, Enums.ModelOptions expected)
    {
        var result = Enum.Parse<Enums.ModelOptions>(value.ToString());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SchemaTypes_InvalidValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Enum.Parse<Enums.SchemaTypes>("99"));
    }
}

public class GeminiModelTests
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // -------------------------------------------------------------------------
    // GeminiResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void GeminiResponse_Deserialise_ExtractsTextFromNestedStructure()
    {
        var json = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "Hello from Gemini" }
                    ]
                  }
                }
              ]
            }
            """;

        var response = JsonSerializer.Deserialize<GeminiResponse>(json, _opts);

        Assert.NotNull(response);
        Assert.Single(response.Candidates);
        Assert.Equal("Hello from Gemini",
            response.Candidates[0].Content.Parts[0].Text);
    }

    [Fact]
    public void GeminiResponse_EmptyJson_DefaultsToEmptyCandidates()
    {
        var response = JsonSerializer.Deserialize<GeminiResponse>("{}", _opts);
        Assert.NotNull(response);
        Assert.Empty(response!.Candidates);
    }

    [Fact]
    public void GeminiResponse_NoCandidates_FirstOrDefaultReturnsNull()
    {
        var response = new GeminiResponse();
        var text = response.Candidates.FirstOrDefault()?.Content.Parts.FirstOrDefault()?.Text;
        Assert.Null(text);
    }

    // -------------------------------------------------------------------------
    // LogAnalysis
    // -------------------------------------------------------------------------

    [Fact]
    public void LogAnalysis_Deserialise_FullPayload()
    {
        var json = """
            {
              "summary": "3 errors detected",
              "timeRange": "10:00 to 10:05",
              "patterns": [
                {
                  "title": "DB timeout",
                  "severity": "High",
                  "description": "Connection pool exhausted",
                  "recommendation": "Increase pool size",
                  "relatedEventIds": [1, 2, 3]
                }
              ],
              "errorCounts": {
                "critical": 1,
                "warnings": 2,
                "errors": 3,
                "info": 10
              }
            }
            """;

        var analysis = JsonSerializer.Deserialize<LogAnalysis>(json, _opts);

        Assert.NotNull(analysis);
        Assert.Equal("3 errors detected", analysis.Summary);
        Assert.Equal("10:00 to 10:05", analysis.TimeRange);
        Assert.Single(analysis.Patterns);

        var pattern = analysis.Patterns[0];
        Assert.Equal("DB timeout", pattern.Title);
        Assert.Equal("High", pattern.Severity);
        Assert.Equal(new[] { 1, 2, 3 }, pattern.RelatedEventIds);

        Assert.Equal(1, analysis.ErrorCounts.Critical);
        Assert.Equal(2, analysis.ErrorCounts.Warnings);
        Assert.Equal(3, analysis.ErrorCounts.Errors);
        Assert.Equal(10, analysis.ErrorCounts.Info);
    }

    [Fact]
    public void LogAnalysis_DefaultValues_AreEmpty()
    {
        var analysis = new LogAnalysis();

        Assert.Equal(string.Empty, analysis.Summary);
        Assert.Equal(string.Empty, analysis.TimeRange);
        Assert.Empty(analysis.Patterns);
        Assert.Equal(0, analysis.ErrorCounts.Critical);
        Assert.Equal(0, analysis.ErrorCounts.Errors);
    }

    [Fact]
    public void LogAnalysis_Deserialise_EmptyPatternsArray()
    {
        var json = """{"summary":"ok","timeRange":"","patterns":[],"errorCounts":{}}""";
        var analysis = JsonSerializer.Deserialize<LogAnalysis>(json, _opts);

        Assert.NotNull(analysis);
        Assert.Empty(analysis!.Patterns);
    }

    [Fact]
    public void LogPattern_RelatedEventIds_DefaultsToEmptyList()
    {
        var pattern = new LogPattern();
        Assert.NotNull(pattern.RelatedEventIds);
        Assert.Empty(pattern.RelatedEventIds);
    }

    // -------------------------------------------------------------------------
    // GeminiRequest serialisation (used when building outbound payloads)
    // -------------------------------------------------------------------------

    [Fact]
    public void GeminiRequest_Serialise_ProducesExpectedJsonShape()
    {
        var request = new GeminiRequest
        {
            Contents =
            [
                new GeminiContentPart
                {
                    Parts = [new GeminiPart { Text = "Analyze these logs" }]
                }
            ]
        };

        var json = JsonSerializer.Serialize(request);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("contents", out var contents));
        Assert.Equal(JsonValueKind.Array, contents.ValueKind);
        var firstPart = contents[0].GetProperty("parts")[0];
        Assert.Equal("Analyze these logs", firstPart.GetProperty("text").GetString());
    }
}
