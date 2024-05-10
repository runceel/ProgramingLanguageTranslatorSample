using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProgramingLanguageTranslatorSample.JsonSchema;
using ProgramingLanguageTranslatorSample.Options;
using ProgramingLanguageTranslatorSample.SourceReaders;

namespace ProgramingLanguageTranslatorSample;
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProgramingLanguageTranslatorServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddOpenAIClient(configuration.GetSection(nameof(OpenAIClient)));
            clientBuilder.UseCredential(new AzureCliCredential());
        });

        services.AddSingleton<IChatCompletionService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ConverterOption>>().Value;
            var openAiClient = sp.GetRequiredService<OpenAIClient>();
            return new AzureOpenAIChatCompletionService(
                options.ModelDeploymentNameForConvertSourceCode,
                openAiClient);
        });

        services.AddKernel();

        services.AddOptions<ConverterOption>()
           .BindConfiguration(nameof(ConverterOption))
           .ValidateDataAnnotations();

        // application services
        services.AddSingleton<ISourceReader, DefaultSourceReader>();
        services.AddSingleton<IJsonSchemaGenerator, DefaultJsonSchemaGenerator>();
        services.AddSingleton<IVBtoCSConverter, DefaultVBtoCSConverter>();

        return services;
    }
}
