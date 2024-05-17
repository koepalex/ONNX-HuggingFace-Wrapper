namespace OnnxHuggingFaceWrapper.IntegrationTests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class IntegrationTestWebApplicationFactory : WebApplicationFactory<OnnxHuggingFaceWrapper.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
          builder.ConfigureAppConfiguration((host, configurationBuilder) => {     
            // configurationBuilder.AddJsonFile("src/appsettings.json", optional: true, reloadOnChange: true);
            // configurationBuilder.AddJsonFile($"src/appsettings.{host.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            
          });

        builder.ConfigureTestServices(services =>
        {
            //Add stub classes
            //services.AddScoped<ISocialLinkParser, StubSocialLinkParser>();

            //Configure logging
            services.AddLogging(builder => builder.ClearProviders().AddConsole().AddDebug());
        });
    }
}