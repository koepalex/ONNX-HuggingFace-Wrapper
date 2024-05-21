namespace OnnxHuggingFaceWrapper.Models;

using System.Text.Json.Serialization;

public class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatCompletionComplete>? Choices { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    [JsonPropertyName("usage")]
    public Usage? Usage { get; set; }
}

public class ChatCompletionComplete
{
    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("logprobs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionLogprobs? Logprobs { get; set; }

    [JsonPropertyName("message")]
    public Message? Message { get; set; }
}

public class Usage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class ChatCompletionLogprobs
{
    [JsonPropertyName("content")]
    public List<ChatCompletionLogprob> Content { get; set; }
}

public class ChatCompletionLogprob
{
    [JsonPropertyName("logprob")]
    public float Logprob { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; }

    [JsonPropertyName("top_logprobs")]
    public List<ChatCompletionTopLogprob> TopLogprobs { get; set; }
}

public class ChatCompletionTopLogprob
{
    [JsonPropertyName("logprob")]
    public float Logprob { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; }
}