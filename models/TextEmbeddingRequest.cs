namespace OnnxHuggingFaceWrapper.Models;

using System.Text.Json.Serialization;

//see https://huggingface.github.io/text-embeddings-inference/#/Text%20Embeddings%20Inference/embed

internal sealed class TextEmbeddingRequest
{
    [JsonPropertyName("inputs")]
    public IList<string> Inputs { get; set; } = [];

    [JsonPropertyName("normalize")]
    public bool? Normalize { get; set; }

    [JsonPropertyName("truncate")]
    public bool? Truncate { get; set; }
}