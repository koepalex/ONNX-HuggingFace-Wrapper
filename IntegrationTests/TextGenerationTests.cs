namespace OnnxHuggingFaceWrapper.IntegrationTests;

using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;
using Microsoft.SemanticKernel.Connectors.HuggingFace;

public class TextGenerationTests: IDisposable
{
    private IntegrationTestWebApplicationFactory _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetup() => _factory = new IntegrationTestWebApplicationFactory();

    [SetUp]
    public void Setup() => _client = _factory.CreateClient();

    public void Dispose() => _factory?.Dispose();

    [Test]
    public async Task TestTextGenerationWithPhi3()
    {
        var kernel = Kernel
            .CreateBuilder()
            .AddHuggingFaceTextGeneration(
                "phi-3-mini",
                new Uri("https://localhost:5001"),
                apiKey: null,
                serviceId: null,
                httpClient: _client
            )
            .Build();

        kernel.Should().NotBeNull();


        var textGeneration = kernel.GetRequiredService<ITextGenerationService>();
        textGeneration.Should().NotBeNull();

        var cts =  new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var textContent = await textGeneration.GetTextContentsAsync(
            "Who is the president of the United States?", 
            new HuggingFacePromptExecutionSettings {
                MaxTokens = 250,
                Temperature = 0.9f,
            },
            cancellationToken: cts.Token);
        textContent.Should().NotBeNullOrEmpty();
        textContent.Should().HaveCountGreaterThanOrEqualTo(1);

        textContent.First().Text.Should().Contain("Joe Biden");
        TestContext.Out.WriteLine(textContent.First().Text);

    }
}