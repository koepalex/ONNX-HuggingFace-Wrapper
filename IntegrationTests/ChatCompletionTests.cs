namespace OnnxHuggingFaceWrapper.IntegrationTests;

using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.HuggingFace;
using Microsoft.SemanticKernel.ChatCompletion;

public class ChatCompletionTests: IDisposable
{
    private IntegrationTestWebApplicationFactory _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetup() => _factory = new IntegrationTestWebApplicationFactory();

    [SetUp]
    public void Setup() => _client = _factory.CreateClient();

    public void Dispose() => _factory?.Dispose();

    [Test]
    public async Task TestChatCompletionWithPhi3()
    {
        var kernel = Kernel
            .CreateBuilder()
            .AddHuggingFaceChatCompletion(
                "phi-3-mini",
                new Uri("https://localhost:5001"),
                apiKey: null,
                serviceId: null,
                httpClient: _client
            )
            .Build();

        kernel.Should().NotBeNull();


        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        chatCompletion.Should().NotBeNull();

        var cts =  new CancellationTokenSource(TimeSpan.FromMinutes(10));

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(@" 
            You extract intention from provided text. The two intentions you identify are: product information and order status. 
            You answer with a valid JSON string. 
            The JSON string must have the format {""Intention"": ""ProductInformation""} or {""Intention"": ""OrderInfo""}. 
            You don't provide additional information. 
            If you can't identify intention answer with {""Intention"": ""Unknown""}
        ");
        chatHistory.AddUserMessage("I've purchased three weeks ago new training shoes. When will they will be delivered?");
        chatHistory.AddUserMessage("Still waiting for the delivery. Any idea when it will arrive? I'm Robert and I'm calling on behalf of company MYCompany.");
        chatHistory.AddUserMessage("Do you have training shoes? If yes, I'm interested in running equipment specifically running shoes.");
        chatHistory.AddUserMessage("What is the average price for running shoes?");

        var chatMessageContent = await chatCompletion.GetChatMessageContentAsync(
            chatHistory, 
            new HuggingFacePromptExecutionSettings {
                MaxTokens = 500,
                Temperature = 0.0f,
            },
            cancellationToken: cts.Token);

        chatMessageContent.Should().NotBeNull();

        TestContext.Out.WriteLine(chatMessageContent.Content);
        TestContext.Out.WriteLine(chatMessageContent.Role);
        // chatMessageContent.Role.Should().Be("assistant");
        // chatMessageContent.Content.Should().Contain("ProductInformation");
    }
}