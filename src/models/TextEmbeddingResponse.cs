namespace OnnxHuggingFaceWrapper.Models;

//see https://huggingface.github.io/text-embeddings-inference/#/Text%20Embeddings%20Inference/embed

internal sealed class TextEmbeddingResponse : List<List<List<ReadOnlyMemory<float>>>>
{
    public static TextEmbeddingResponse Create()
    {
        return new TextEmbeddingResponse
        {
            new List<List<ReadOnlyMemory<float>>>
            {
                new List<ReadOnlyMemory<float>>()
            }
        };
    }

    public void Add(ReadOnlyMemory<float> embedding)
    {
        this[0][0].Add(embedding);
    }
}