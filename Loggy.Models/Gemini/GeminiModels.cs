using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Loggy.Models.Gemini
{
    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; set; } = [];
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContentPart Content { get; set; } = new();
    }

    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContentPart> Contents { get; set; }
    }

    public class GeminiContentPart
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; }
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class LogAnalysis
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("timeRange")]
        public string TimeRange { get; set; } = "";

        [JsonPropertyName("patterns")]
        public List<LogPattern> Patterns { get; set; } = [];

        [JsonPropertyName("errorCounts")]
        public ErrorCounts ErrorCounts { get; set; } = new();
    }

    public class LogPattern
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = "";

        [JsonPropertyName("relatedEventIds")]
        public List<int> RelatedEventIds { get; set; } = [];
    }

    public class ErrorCounts
    {
        [JsonPropertyName("critical")]
        public int Critical { get; set; }

        [JsonPropertyName("warnings")]
        public int Warnings { get; set; }

        [JsonPropertyName("errors")]
        public int Errors { get; set; }

        [JsonPropertyName("info")]
        public int Info { get; set; }
    }
}
