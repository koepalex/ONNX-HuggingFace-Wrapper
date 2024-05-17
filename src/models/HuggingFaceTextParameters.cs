namespace OnnxHuggingFaceWrapper.Models;

using System.Text.Json.Serialization;

//see https://huggingface.github.io/text-generation-inference/#/Text%20Generation%20Inference/compat_generate
internal sealed class HuggingFaceTextParameters
{
    [JsonPropertyName("top_k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; set; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; } = 1;

    [JsonPropertyName("repetition_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RepetitionPenalty { get; set; }

    [JsonPropertyName("max_new_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxNewTokens { get; set; }

    [JsonPropertyName("max_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxTime { get; set; }

    [JsonPropertyName("return_full_text")]
    public bool? ReturnFullText { get; set; }

    [JsonPropertyName("do_sample")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DoSample { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Details { get; set; }

    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Stop { get; set; }

    [JsonPropertyName("typical_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TypicalP { get; set; }

    [JsonPropertyName("best_of")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BestOf { get; set; }

    [JsonPropertyName("decoder_input_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DecoderInputDetails { get; set; }

    [JsonPropertyName("watermark")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Watermark { get; set; }

    [JsonPropertyName("frequency_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? FrequencyPenalty { get; set; }


    // "grammar": null,
    // "seed": null,    
    // "top_n_tokens": 5,
    // "truncate": null,

}