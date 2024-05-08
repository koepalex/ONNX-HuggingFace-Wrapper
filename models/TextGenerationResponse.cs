namespace OnnxHuggingFaceWrapper.Models;

using System.Text.Json.Serialization;

internal sealed class TextGenerationResponse
{
    [JsonPropertyName("generated_text")]
    public string? GeneratedText { get; set; }
}