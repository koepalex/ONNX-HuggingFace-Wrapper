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

internal sealed class Program : IDisposable
{
    private readonly Model _model;
    private readonly string _modelName;
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
        app.MapPost("/models/{modelName}", async (string _, TextGenerationRequest req)
            => program.GenerateTextAsync(req));
        app.MapPost("/v1/chat/completions", async (ChatRequest req)
            => program.ChatCompletionAsync(req));

        // start the WebAPI
        await app.RunAsync();
    }

    public Program(ExecutorWithMeasurementFactory executorFactory, IOptions<AiModelSettings> aiModelSettings, ILogger<Program> logger)
    {
        _logger = logger;
        _executorFactory = executorFactory;
        _aiModelSettings = aiModelSettings;
        // This will load `genai_config.json` get get details of the model
        _model = new Model(aiModelSettings.Value.ModelPath);
        _modelName = Path.GetFileNameWithoutExtension(aiModelSettings.Value.ModelPath);
        _tokenizer = new Tokenizer(_model);

    }

    private async IAsyncEnumerable<ChatCompletionResponse> ChatCompletionAsync([FromBody] ChatRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<|system|>\n{_aiModelSettings.Value.SystemPrompt}<|end|>");
        request.Messages.ForEach(msg => sb.AppendLine($"<|{msg.Role}|>\n{msg.Content}<|end|>"));
        sb.AppendLine("<|assistant|>");

        var input = sb.ToString();
        var inputSequences = _tokenizer.Encode(input);

        using var generatorParams = new GeneratorParams(_model);
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

        if (request.Stream)
        {
            using var tokenizerStream = _tokenizer.CreateStream();
            using var generator = new Generator(_model, generatorParams);
            int messageCounter = 0;
            while (!generator.IsDone())
            {
                // run a task, to avoid blocking the sending thread
                yield return await Task.Run(() =>
                {
                    generator.ComputeLogits();
                    generator.GenerateNextToken();

                    return new ChatCompletionResponse
                    {
                        Model = _modelName,
                        Object = string.Join('\n', tokenizerStream.Decode(generator.GetSequence(0)[^1])),
                        Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Id = messageCounter++.ToString(),
                        SystemFingerprint = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0", // use as fingerprint the version of the assembly
                    };
                });
            }
        }
        else
        {
            Sequences outputSequences;
            using (var measurement = _executorFactory.CreateExecutor())
            {
                outputSequences = _model.Generate(generatorParams);
            }

            string[] outputs = _tokenizer.DecodeBatch(outputSequences);
            yield return new ChatCompletionResponse
            {
                Model = _modelName,
                Object = string.Join('\n', outputs),
                Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Id = input.GetHashCode().ToString(),
                SystemFingerprint = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0", // use as fingerprint the version of the assembly
            };
        }
    }

    private async IAsyncEnumerable<TextGenerationResponse> GenerateTextAsync([FromBody] TextGenerationRequest request)
    {
        // use <|system|> for grounding prompts
        var inputs = new string[] {
            $"<|system|>\n{_aiModelSettings.Value.SystemPrompt}<|end|>",
            $"<|user|>\n{request.Inputs}<|end|>",
            "<|assistant|>"
        };
        var inputSequences = _tokenizer.Encode(string.Join('\n', inputs));

        using var generatorParams = new GeneratorParams(_model);

        request.Parameters?.TopK.Apply(x => generatorParams.SetSearchOption("top_k", x));
        request.Parameters?.TopP.Apply(x => generatorParams.SetSearchOption("top_p", x));
        request.Parameters?.Temperature.Apply(x => generatorParams.SetSearchOption("temperature", x), defaultValue: 0.7);
        request.Parameters?.RepetitionPenalty.Apply(x => generatorParams.SetSearchOption("repetition_penalty", x));
        // setting max_new_tokens or max_token depends on the model, setting max_length will work for all models
        request.Parameters?.MaxNewTokens.Apply(x => generatorParams.SetSearchOption("max_length", x));
        request.Parameters?.MaxTime.Apply(x => generatorParams.SetSearchOption("max_time", x));
        request.Parameters?.ReturnFullText.Apply(x => generatorParams.SetSearchOption("return_full_text", x));
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
            using var generator = new Generator(_model, generatorParams);
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
                outputSequences = _model.Generate(generatorParams);
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
        configuration
            .AddCommandLine(args)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
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
        _model?.Dispose();
        _tokenizer?.Dispose();
        _logger?.LogInformation("Exiting ...");
    }

}
