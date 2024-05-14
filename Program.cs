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

internal sealed class Program : IDisposable
{
    // relative paths seems not to work
    // best clone huggingface repo and use the path to the model
    // NOTE: install GIT LFS and activate it before cloning the repo
    //const string modelPath = @"C:\Users\alkopke\src\HF\Phi-3-mini-4k-instruct-onnx\cpu_and_mobile\cpu-int4-rtn-block-32";
    //const string systemPrompt = "You are a helpful assistant. You are here to help me with my tasks.";

    private readonly Model _model;
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
        app.MapPost("/models/{modelName}", async (string modelName, TextGenerationRequest req)
            => program.GenerateTextAsync(modelName, req));

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
        _tokenizer = new Tokenizer(_model);

    }

    internal async IAsyncEnumerable<TextGenerationResponse> GenerateTextAsync(string modelName, [FromBody] TextGenerationRequest request)
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

        string[] outputs;
        if (request.Stream)
        {
            using var tokenizerStream = _tokenizer.CreateStream();
            using var generator = new Generator(_model, generatorParams);
            while (!generator.IsDone())
            {
                generator.ComputeLogits();
                generator.GenerateNextToken();
                
                yield return new TextGenerationResponse { GeneratedText = tokenizerStream.Decode(generator.GetSequence(0)[^1]) };
            }
        }
        else
        {
            Sequences outputSequences;
            using (var measurement = _executorFactory.CreateExecutor())
            {
                outputSequences = _model.Generate(generatorParams);
            }

            outputs = _tokenizer.DecodeBatch(outputSequences);
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
        
        services.Configure<AiModelSettings>(options => configuration.GetSection("AiModelSettings").Bind(options));;
    }

    public void Dispose()
    {
        _model?.Dispose();
        _tokenizer?.Dispose();
        _logger?.LogInformation("Exiting ...");
    }

}
