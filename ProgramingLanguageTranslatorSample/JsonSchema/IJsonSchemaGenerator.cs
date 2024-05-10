using NJsonSchema.Generation;
using System.Text.Json;

namespace ProgramingLanguageTranslatorSample.JsonSchema;
public interface IJsonSchemaGenerator
{
    string GenerateFromType<T>() => GenerateFromType(typeof(T));
    string GenerateFromType(Type type);
}

public class DefaultJsonSchemaGenerator : IJsonSchemaGenerator
{
    private readonly JsonSchemaGeneratorSettings _settings = new SystemTextJsonSchemaGeneratorSettings
    {
        SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        },
    };
    
    public string GenerateFromType(Type type)
    {
        var generator = new JsonSchemaGenerator(_settings);
        return generator.Generate(type).ToJson();
    }
}
