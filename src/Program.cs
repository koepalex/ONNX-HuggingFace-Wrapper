namespace OnnxHuggingFaceWrapper;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OnnxHuggingFaceWrapper.Models;
using System.Net;
using System.Net.Mime;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using OnnxHuggingFaceWrapper.Configuration;
using System.Text;
using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public sealed class Program : IDisposable
{
    private readonly Model _languageModel;
    private readonly string _languageModelName;
    private readonly InferenceSession _embeddingsInferenceSession;
    private readonly InferenceSession _embeddingsTokenizerInferenceSession;
    private readonly Tokenizer _tokenizer;
    private readonly ILogger<Program> _logger;
    private readonly ExecutorWithMeasurementFactory _executorFactory;
    private readonly IOptions<AiModelSettings> _aiModelSettings;

    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureConfiguration(builder.Configuration, builder.Environment, args);
        ConfigureServices(builder.Services, builder.Configuration);
        var app = builder.Build();

        using var program = app.Services.GetService<Program>()!;

        app.UseExceptionHandler(errorApp =>
            errorApp.Run(async context =>
                await program.ErrorHandlingAsync(context))
        );

        // Define all Routes
        app.MapPost("/models/{_}", async (string _, TextGenerationRequest req)
            => program.GenerateTextAsync(req));
        app.MapPost("/v1/chat/completions", async (ChatRequest req)
            => await program.ChatCompletionAsync(req));
        app.MapPost("/pipeline/feature-extraction/{_}", async (string _, TextEmbeddingRequest req)
            => program.ExtractTextEmbeddings(req));

        // start the WebAPI
        await app.RunAsync();
    }

    public Program(ExecutorWithMeasurementFactory executorFactory, IOptions<AiModelSettings> aiModelSettings, ILogger<Program> logger)
    {
        _logger = logger;
        _executorFactory = executorFactory;
        _aiModelSettings = aiModelSettings;
        
        if (!Path.Exists(aiModelSettings.Value.SmallLanguageModelPath))
        {
            _logger.LogError("Model file not found at {modelPath}", aiModelSettings.Value.SmallLanguageModelPath);
            Environment.FailFast("Model file not found");
        }

        // This will load `genai_config.json` get get details of the model
        _languageModel = new Model(aiModelSettings.Value.SmallLanguageModelPath);
        _languageModelName = Path.GetFileNameWithoutExtension(aiModelSettings.Value.SmallLanguageModelPath);
        _tokenizer = new Tokenizer(_languageModel);
        

        if (!File.Exists(aiModelSettings.Value.TextEncoderModelPath))
        {
            _logger.LogError("Model file not found at {modelPath}", aiModelSettings.Value.TextEncoderModelPath);
            Environment.FailFast("Model file not found");
        }

        // Microsoft.ML.OnnxRuntimeGenAI has currently no support for embeddings, so we use the original Microsoft.ML.OnnxRuntime
        var options = new Microsoft.ML.OnnxRuntime.SessionOptions();
        options.RegisterOrtExtensions();
        _embeddingsInferenceSession = new InferenceSession(aiModelSettings.Value.TextEncoderModelPath, options);

        if (!File.Exists(aiModelSettings.Value.TokenizerModelPath))
        {
            _logger.LogError("Model file not found at {modelPath}", aiModelSettings.Value.TokenizerModelPath);
            Environment.FailFast("Model file not found");
        }

        // Load the tokenizer model
        _embeddingsTokenizerInferenceSession = new InferenceSession(aiModelSettings.Value.TokenizerModelPath, options);
    }

    private TextEmbeddingResponse ExtractTextEmbeddings([FromBody]TextEmbeddingRequest req)
    {
        var result = TextEmbeddingResponse.Create();
        foreach (var input in req.Inputs)
        {
            var textTokenized = TokenizeText(input);
            var textPromptEmbeddings = TextEncoder(textTokenized).ToArray();

            result.Add(textPromptEmbeddings);
        }
        return result;
    }

    private int[] TokenizeText(string text)
    {
        var inputTensor = new DenseTensor<string>(new string[] { text }, new int[] { 1 });
        // check session.InputNames for the expected names of input tensors
        var inputString = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor<string>("string_input", inputTensor) };
        
        // Run session and send the input data in to get inference output. 
        var tokens = _embeddingsTokenizerInferenceSession.Run(inputString);

        var inputIds = (tokens.ToList().First().Value as IEnumerable<long>).ToArray();

        // Cast inputIds to Int32
        var InputIdsInt = inputIds.Select(x => (int)x).ToArray();

        var modelMaxLength = 77;
        // Pad array with 49407 until length is modelMaxLength
        if (InputIdsInt.Length < modelMaxLength)
        {
            var pad = Enumerable.Repeat(49407, 77 - InputIdsInt.Length).ToArray();
            InputIdsInt = InputIdsInt.Concat(pad).ToArray();
        }

        return InputIdsInt;

    }

    private DenseTensor<float> TextEncoder(int[] tokenizedInput)
    {
        // Create input tensor.
        var input_ids = new DenseTensor<int>(tokenizedInput, new[] { 1, tokenizedInput.Count() });

        var input = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor<int>("input_ids", input_ids) };

        // // Set CUDA EP
        // var sessionOptions = config.GetSessionOptionsForEp();

        // var encodeSession = new InferenceSession(config.TextEncoderOnnxPath, sessionOptions);
        // Run inference.
        var encoded = _embeddingsInferenceSession.Run(input);

        var lastHiddenState = (encoded.ToList().First().Value as IEnumerable<float>).ToArray();
        var lastHiddenStateTensor = new DenseTensor<float>(lastHiddenState.ToArray(), new[] { 1, 77, 768 });

        return lastHiddenStateTensor;
    }

    private async Task<ChatCompletionResponse> ChatCompletionAsync([FromBody] ChatRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<|system|>\n{_aiModelSettings.Value.SmallLanguageModelSystemPrompt}<|end|>");
        request.Messages.ForEach(msg => sb.AppendLine($"<|{msg.Role}|>\n{msg.Content}<|end|>"));
        sb.AppendLine("<|assistant|>");

        var input = sb.ToString();
        var inputSequences = _tokenizer.Encode(input);

        using var generatorParams = new GeneratorParams(_languageModel);
        request.TopP.Apply(x => generatorParams.SetSearchOption("top_p", x));
        request.Temperature.Apply(x => generatorParams.SetSearchOption("temperature", x), defaultValue: 0.7f);
        request.MaxTokens.Apply(x => generatorParams.SetSearchOption("max_length", x));
        request.N.Apply(x => generatorParams.SetSearchOption("n", x));
        request.FrequencyPenalty.Apply(x => generatorParams.SetSearchOption("frequency_penalty", x));
        request.PresencePenalty.Apply(x => generatorParams.SetSearchOption("presence_penalty", x));
        request.Logprobs.Apply(x => generatorParams.SetSearchOption("logprobs", x));
        //request.LogitBias.Apply(x => generatorParams.SetSearchOption("logit_bias", x));
        //request.Stop.Apply(x => generatorParams.SetSearchOption("stop", x));
        request.Seed.Apply(x => generatorParams.SetSearchOption("seed", x));
        //request.ToolPrompt.Apply(x => generatorParams.SetSearchOption("tool_prompt", x));
        //request.ToolChoice.Apply(x => generatorParams.SetSearchOption("tool_choice", x));
        request.TopLogprobs.Apply(x => generatorParams.SetSearchOption("top_logprobs", x));
        //request.Tools.Apply(x => generatorParams.SetSearchOption("tools", x));

        generatorParams.SetInputSequences(inputSequences);

        // if (request.Stream)
        // {
        //     // using var tokenizerStream = _tokenizer.CreateStream();
        //     // using var generator = new Generator(_languageModel, generatorParams);
        //     // int messageCounter = 0;
        //     // while (!generator.IsDone())
        //     // {
        //     //     // run a task, to avoid blocking the sending thread
        //     //     yield return await Task.Run(() =>
        //     //     {
        //     //         generator.ComputeLogits();
        //     //         generator.GenerateNextToken();

        //     //         return new ChatCompletionResponse
        //     //         {
        //     //             Model = _languageModelName,
        //     //             Object = string.Join('\n', tokenizerStream.Decode(generator.GetSequence(0)[^1])),
        //     //             Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        //     //             Id = messageCounter++.ToString(),
        //     //             SystemFingerprint = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0", // use as fingerprint the version of the assembly
        //     //         };
        //     //     });
        //     // }
        // }
        // else
        // {
            Sequences outputSequences;
            using (var measurement = _executorFactory.CreateExecutor())
            {
                outputSequences = _languageModel.Generate(generatorParams);
            }

            string[] outputs = _tokenizer.DecodeBatch(outputSequences);
            return new ChatCompletionResponse
            {
                Model = _languageModelName,
                Object = null,
                Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Id = $"id-{input.GetHashCode()}",
                SystemFingerprint = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0", // use as fingerprint the version of the assembly
                Choices = new List<ChatCompletionComplete> { 
                    new ChatCompletionComplete { 
                        FinishReason = "completed",
                        Message = new Message { 
                            Content = string.Join('\n', outputs), 
                            Role = "assistant", 
                    },
                    Index = 0 } },
                Usage = new Usage {
                    CompletionTokens = outputs.Length,
                    TotalTokens = outputs.Length,
                    PromptTokens = input.Length,
                },
                
            };
        //}
    }

    private async IAsyncEnumerable<TextGenerationResponse> GenerateTextAsync([FromBody] TextGenerationRequest request)
    {
        // use <|system|> for grounding prompts
        var inputs = new string[] {
            $"<|system|>\n{_aiModelSettings.Value.SmallLanguageModelSystemPrompt}<|end|>",
            $"<|user|>\n{request.Inputs}<|end|>",
            "<|assistant|>"
        };
        var inputSequences = _tokenizer.Encode(string.Join('\n', inputs));

        using var generatorParams = new GeneratorParams(_languageModel);

        request.Parameters?.TopK.Apply(x => generatorParams.SetSearchOption("top_k", x));
        request.Parameters?.TopP.Apply(x => generatorParams.SetSearchOption("top_p", x));
        request.Parameters?.Temperature.Apply(x => generatorParams.SetSearchOption("temperature", x), defaultValue: 0.7);
        request.Parameters?.RepetitionPenalty.Apply(x => generatorParams.SetSearchOption("repetition_penalty", x));
        // setting max_new_tokens or max_token depends on the model, setting max_length will work for all models
        request.Parameters?.MaxNewTokens.Apply(x => generatorParams.SetSearchOption("max_length", x));
        request.Parameters?.MaxTime.Apply(x => generatorParams.SetSearchOption("max_time", x));
        //request.Parameters?.ReturnFullText.Apply(x => generatorParams.SetSearchOption("return_full_text", x));
        request.Parameters?.DoSample.Apply(x => generatorParams.SetSearchOption("do_sample", x));
        request.Parameters?.Details.Apply(x => generatorParams.SetSearchOption("details", x));
        //request.Parameters?.Stop.Apply(x => generatorParams.("stop", x));
        request.Parameters?.TypicalP.Apply(x => generatorParams.SetSearchOption("typical_p", x));
        request.Parameters?.BestOf.Apply(x => generatorParams.SetSearchOption("best_of", x));
        request.Parameters?.DecoderInputDetails.Apply(x => generatorParams.SetSearchOption("decoder_input_details", x));
        request.Parameters?.Watermark.Apply(x => generatorParams.SetSearchOption("watermark", x));

        generatorParams.SetInputSequences(inputSequences);

        if (request.Stream)
        {
            using var tokenizerStream = _tokenizer.CreateStream();
            using var generator = new Generator(_languageModel, generatorParams);
            while (!generator.IsDone())
            {
                yield return await Task.Run(() =>
                {
                    generator.ComputeLogits();
                    generator.GenerateNextToken();

                    return new TextGenerationResponse { GeneratedText = tokenizerStream.Decode(generator.GetSequence(0)[^1]) };
                });
            }
        }
        else
        {
            Sequences outputSequences;
            using (var measurement = _executorFactory.CreateExecutor())
            {
                outputSequences = _languageModel.Generate(generatorParams);
            }

            string[] outputs = _tokenizer.DecodeBatch(outputSequences);
            yield return new TextGenerationResponse { GeneratedText = string.Join('\n', outputs) };
        }
    }

    internal async Task ErrorHandlingAsync(HttpContext context)
    {
        context.Response.StatusCode = Convert.ToInt32(HttpStatusCode.InternalServerError);
        context.Response.ContentType = MediaTypeNames.Text.Plain;

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        _logger.LogError(exception, "An unexpected error occurred.");

        await context.Response.WriteAsync("An unexpected error occurred. Please try again later.");
    }

    private static void ConfigureConfiguration(IConfigurationManager configuration, IHostEnvironment environment, string[] args)
    {
        var assemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Program)).Location);
        configuration
            .AddCommandLine(args)
            .AddJsonFile(Path.Combine(assemblyPath, "appsettings.json"), optional: false, reloadOnChange: true)
            .AddJsonFile(Path.Combine(assemblyPath, $"appsettings.{environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(config =>
        {
            config
            .AddConsole()
            .AddFilter(level => level >= LogLevel.Trace);
        })
        .AddSingleton<ExecutorWithMeasurementFactory>()
        .AddTransient<Program>();

        services.Configure<AiModelSettings>(options => configuration.GetSection("AiModelSettings").Bind(options)); ;
    }

    public void Dispose()
    {
        _languageModel?.Dispose();
        _tokenizer?.Dispose();
        _logger?.LogInformation("Exiting ...");
    }

}
