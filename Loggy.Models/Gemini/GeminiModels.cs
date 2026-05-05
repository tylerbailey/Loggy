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


}
