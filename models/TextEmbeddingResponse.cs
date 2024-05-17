namespace OnnxHuggingFaceWrapper.Models;

using System.Text.Json.Serialization;

//see https://huggingface.github.io/text-embeddings-inference/#/Text%20Embeddings%20Inference/embed

internal sealed class TextEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public IList<ReadOnlyMemory<float>> Embeddings { get; set; } = [];
}