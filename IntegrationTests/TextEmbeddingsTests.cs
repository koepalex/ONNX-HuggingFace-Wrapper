namespace OnnxHuggingFaceWrapper.IntegrationTests;

using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

public class TextEmbeddingsTests: IDisposable
{
    private IntegrationTestWebApplicationFactory _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetup() => _factory = new IntegrationTestWebApplicationFactory();

    [SetUp]
    public void Setup() => _client = _factory.CreateClient();

    public void Dispose() => _factory?.Dispose();

    [Test]
    public async Task TestTextEmbeddings()
    {
        var kernel = Kernel
            .CreateBuilder()
            .AddHuggingFaceTextEmbeddingGeneration(
                "clip",
                new Uri("https://localhost:5001"),
                apiKey: null,
                serviceId: null,
                httpClient: _client
            )
            .Build();

        kernel.Should().NotBeNull();


        var embeddingsGeneration = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        embeddingsGeneration.Should().NotBeNull();

        var cts =  new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var embeddings = await embeddingsGeneration.GenerateEmbeddingsAsync(
            new List<string> {
                "Who is the president of the United States?", 
            },
            cancellationToken: cts.Token);
        embeddings.Should().NotBeNullOrEmpty();
        embeddings.Should().HaveCountGreaterThanOrEqualTo(1);

        embeddings.First().Length.Should().BeGreaterThan(0);
    }
}