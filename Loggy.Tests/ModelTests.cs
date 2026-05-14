using Loggy.Models;
using Loggy.Models.Gemini;
using System.Text.Json;

namespace Loggy.Tests;

public class ModelTests
{
    // ── LogEvent ──────────────────────────────────────────────────────────────

    public class LogEventTests
    {
        [Fact]
        public void LogEvent_DefaultSchema_IsNull()
        {
            var e = new LogEvent();
            Assert.Null(e.Schema);
        }

        [Fact]
        public void LogEvent_IdAndSchema_CanBeSet()
        {
            var e = new LogEvent
            {
                Id = 42,
                Schema = new() { ["Level"] = "Error" }
            };

            Assert.Equal(42, e.Id);
            Assert.Equal("Error", e.Schema["Level"]);
        }
    }

    // ── SeriLogEvent ──────────────────────────────────────────────────────────

    public class SeriLogEventTests
    {
        [Fact]
        public void SeriLogEvent_DefaultValues_AreCorrect()
        {
            var e = new SeriLogEvent();

            Assert.Equal(0, e.Id);
            Assert.Equal(string.Empty, e.Level);
            Assert.Equal(string.Empty, e.Message);
            Assert.Null(e.Exception);
            Assert.Null(e.Source);
            Assert.Null(e.TraceId);
            Assert.NotNull(e.Properties);
            Assert.Empty(e.Properties);
        }

        [Fact]
        public void SeriLogEvent_CanSetAllProperties()
        {
            var ts = DateTimeOffset.UtcNow;
            var e = new SeriLogEvent
            {
                Id = 1,
                Timestamp = ts,
                Level = "Error",
                Message = "Something broke",
                Exception = "NullReferenceException",
                Source = "WebAPI",
                TraceId = "abc-123",
                Properties = new()
                {
                    ["userId"] = JsonDocument.Parse("42").RootElement
                }
            };

            Assert.Equal(1, e.Id);
            Assert.Equal(ts, e.Timestamp);
            Assert.Equal("Error", e.Level);
            Assert.Equal("Something broke", e.Message);
            Assert.Equal("NullReferenceException", e.Exception);
            Assert.Equal("WebAPI", e.Source);
            Assert.Equal("abc-123", e.TraceId);
            Assert.Equal("42", e.Properties["userId"].ToString());
        }
    }

    // ── Enums ─────────────────────────────────────────────────────────────────

    public class EnumsTests
    {
        [Fact]
        public void SchemaTypes_HasExpectedValues()
        {
            var values = Enum.GetNames<Enums.SchemaTypes>();
            Assert.Contains("Serilog", values);
            Assert.Contains("NLog", values);
            Assert.Contains("Log4Net", values);
        }

        [Fact]
        public void SortOptions_HasExpectedValues()
        {
            var values = Enum.GetNames<Enums.SortOptions>();
            Assert.Contains("ByException", values);
            Assert.Contains("ByTimeStamp", values);
        }

        [Fact]
        public void ModelOptions_HasExpectedValues()
        {
            var values = Enum.GetNames<Enums.ModelOptions>();
            Assert.Contains("Gemini", values);
            Assert.Contains("Custom", values);
        }
    }

    // ── Gemini models ─────────────────────────────────────────────────────────

    public class GeminiModelTests
    {
        [Fact]
        public void GeminiResponse_DefaultCandidates_IsEmpty()
        {
            var r = new GeminiResponse();
            Assert.NotNull(r.Candidates);
            Assert.Empty(r.Candidates);
        }

        [Fact]
        public void LogAnalysis_DefaultCollections_AreInitialized()
        {
            var a = new LogAnalysis();
            Assert.NotNull(a.Patterns);
            Assert.Empty(a.Patterns);
            Assert.NotNull(a.ErrorCounts);
        }

        [Fact]
        public void LogPattern_DefaultRelatedEventIds_IsEmpty()
        {
            var p = new LogPattern();
            Assert.NotNull(p.RelatedEventIds);
            Assert.Empty(p.RelatedEventIds);
        }

        [Fact]
        public void ErrorCounts_DefaultValues_AreZero()
        {
            var ec = new ErrorCounts();
            Assert.Equal(0, ec.Critical);
            Assert.Equal(0, ec.Warnings);
            Assert.Equal(0, ec.Errors);
            Assert.Equal(0, ec.Info);
        }

        [Fact]
        public void GeminiResponse_JsonPropertyNames_AreCorrect()
        {
            // Verifies that [JsonPropertyName] attributes match the Gemini API contract
            var json = """{"candidates":[{"content":{"parts":[{"text":"hello"}]}}]}""";
            var response = JsonSerializer.Deserialize<GeminiResponse>(json);

            Assert.NotNull(response);
            Assert.Single(response.Candidates);
            Assert.Single(response.Candidates[0].Content.Parts);
            Assert.Equal("hello", response.Candidates[0].Content.Parts[0].Text);
        }

        [Fact]
        public void LogAnalysis_JsonPropertyNames_AreCorrect()
        {
            var json = """
            {
                "summary": "All good",
                "timeRange": "2024-01-01 to 2024-01-02",
                "patterns": [
                    {
                        "title": "High error rate",
                        "severity": "Critical",
                        "description": "Many errors",
                        "recommendation": "Check DB",
                        "relatedEventIds": [1, 2, 3]
                    }
                ],
                "errorCounts": { "critical": 2, "warnings": 5, "errors": 10, "info": 100 }
            }
            """;
            var analysis = JsonSerializer.Deserialize<LogAnalysis>(json);

            Assert.NotNull(analysis);
            Assert.Equal("All good", analysis.Summary);
            Assert.Single(analysis.Patterns);
            Assert.Equal("Critical", analysis.Patterns[0].Severity);
            Assert.Equal(3, analysis.Patterns[0].RelatedEventIds.Count);
            Assert.Equal(2, analysis.ErrorCounts.Critical);
            Assert.Equal(10, analysis.ErrorCounts.Errors);
        }
    }
}
