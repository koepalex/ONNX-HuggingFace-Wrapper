namespace OnnxHuggingFaceWrapper.Configuration;

public class AiModelSettings
{
    public string SmallLanguageModelPath { get; set; } = string.Empty;
    public string TextEncoderModelPath { get; set; } = string.Empty;
    public string TokenizerModelPath { get; set; } = string.Empty;
    public string SmallLanguageModelSystemPrompt { get; set; } = string.Empty;
}