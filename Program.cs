using System.Diagnostics;
using System.Linq.Expressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using Microsoft.ML.OnnxRuntimeGenAI;

#region Services and Logging Configuration
var serviceCollection = new ServiceCollection();
ConfigureServices(serviceCollection);

var serviceProvider = serviceCollection.BuildServiceProvider();
var logger = serviceProvider.GetService<ILogger<Program>>()!;
#endregion

// relative paths seems not to work
// best clone huggingface repo and use the path to the model
// NOTE: install GIT LFS and activate it before cloning the repo
string modelPath = @"C:\Users\alkopke\src\HF\Phi-3-mini-4k-instruct-onnx\cpu_and_mobile\cpu-int4-rtn-block-32";
// start with complete output first
const OutputType outputType = OutputType.Complete;

// This will load `genai_config.json` get get details of the model
using var model = new Model(modelPath);
using var tokenizer = new Tokenizer(model);

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var sw = new Stopwatch();

while(!cts.IsCancellationRequested)
{
    try
    {
        logger.LogInformation("Please enter your question:");
        string prompt = await Console.In.ReadLineAsync() ?? string.Empty;

        // use <|system|> for grounding prompts
        var inputs = new string[] {
            $"<|user|>\n{prompt}<|end|>\n<|assistant|>"
        };

        var inputSequences = tokenizer.EncodeBatch(inputs);

        using var generatorParams =  new GeneratorParams(model);

        // TODO figure out which options are available
        generatorParams.SetSearchOption("max_length", 200);
        generatorParams.SetSearchOption("temperature", 0.6);
        generatorParams.SetInputSequences(inputSequences);


        if (outputType == OutputType.Complete) 
        {
            Sequences outputSequences;
            using (var measurement = serviceProvider.GetService<ExecutorWithMeasurement>())
            {
                outputSequences = model.Generate(generatorParams);
            }

            logger.LogInformation("Output:");
            var outputs = tokenizer.DecodeBatch(outputSequences);
            Array.ForEach(outputs, Console.WriteLine);
        }
        else if (outputType == OutputType.Streaming)
        {
            using var tokenizerStream = tokenizer.CreateStream();
            using (var measurement = serviceProvider.GetService<ExecutorWithMeasurement>())
            {
                using var generator = new Generator(model, generatorParams);
                while (!generator.IsDone())
                {
                    generator.ComputeLogits();
                    Console.Write(tokenizerStream.Decode(generator.GetSequence(0)[^1]));
                }
            }
        }
        else
        {
            logger.LogError("Invalid output type");
            cts.Cancel();
        }
    }
    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
    {
        logger.LogError(ex.ToString());
        cts.Cancel();
    }
}

await Console.Out.WriteLineAsync("Exiting...");

static void ConfigureServices(IServiceCollection services)
{
    services.AddLogging(config =>
    {
        config.AddConsole(options =>
        {
            //options..ColorBehavior = LoggerColorBehavior.Enabled;
        })
        .AddFilter(level => level >= LogLevel.Trace);
    })
    .AddTransient<ExecutorWithMeasurement>();
}

public enum OutputType
{
    Complete = 0,
    Streaming = 1,
}