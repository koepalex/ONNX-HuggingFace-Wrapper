namespace OnnxHuggingFaceWrapper.Models;

using System.Text.Json.Serialization;

//see https://huggingface.github.io/text-generation-inference/#/Text%20Generation%20Inference/compat_generate
internal sealed class TextGenerationRequest
{
    [JsonPropertyName("inputs")]
    public string? Inputs { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HuggingFaceTextParameters? Parameters { get; set; }
}