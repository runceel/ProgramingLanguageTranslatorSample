using NJsonSchema.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramingLanguageTranslatorSample.JsonSchema;
public interface IJsonSchemaGenerator
{
    string GenerateFromType<T>() => GenerateFromType(typeof(T));
    string GenerateFromType(Type type);
}

public class DefaultJsonSchemaGenerator : IJsonSchemaGenerator
{
    private readonly JsonSchemaGeneratorSettings _settings = new SystemTextJsonSchemaGeneratorSettings();
    
    public string GenerateFromType(Type type)
    {
        var generator = new JsonSchemaGenerator(_settings);
        return generator.Generate(type).ToJson();
    }
}
