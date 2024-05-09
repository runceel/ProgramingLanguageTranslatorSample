using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProgramingLanguageTranslatorSample.JsonSchema;
using ProgramingLanguageTranslatorSample.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProgramingLanguageTranslatorSample.Memories;
internal class MemoryPlugin(
    IOptions<ConverterOption> options,
    IJsonSchemaGenerator jsonSchemaGenerator,
    ILogger<MemoryPlugin> logger)
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private CodeContext _codeContext = new();

    private KernelFunction? _updateCodeContextFunction;

    [KernelFunction]
    [Description("現在のソースコードを理解するために必要なコンテキストを取得します。")]
    public Task<CodeContext> GetCurrentCodeContextAsync(CancellationToken cancellationToken) => Task.FromResult(_codeContext);

    [KernelFunction]
    [Description("現在のソースコードを理解するために必要なコンテキストを更新します。")]
    public async Task UpdateCodeContextAsync(
        string lineOfCurrentSourceCode,
        string[] previousCodeLines,
        string[] nextCodeLines,
        Kernel kernel,
        CancellationToken cancellationToken)
    {
        var function = EnsureCreateUpdateCodeContextFunction(kernel);
        var context = await GetCurrentCodeContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await kernel.InvokeAsync<string>(function,
            new()
            {
                ["programingLanguage"] = options.Value.SourceLanguage,
                ["schema"] = jsonSchemaGenerator.GenerateFromType<CodeContext>(),
                ["currentContext"] = JsonSerializer.Serialize(context, _serializerOptions),
                ["previousCodeLines"] = string.Join('\n', previousCodeLines),
                ["lineOfCurrentSourceCode"] = lineOfCurrentSourceCode,
                ["nextCodeLines"] = string.Join('\n', nextCodeLines),
            }).ConfigureAwait(false);
        var resultContext = JsonSerializer.Deserialize<CodeContext>(result ?? "");
        if (resultContext != null)
        {
            _codeContext = resultContext;
        }
        else
        {
            logger.LogWarning("コンテキストの更新に失敗しました。");
        }
    }

    private KernelFunction EnsureCreateUpdateCodeContextFunction(Kernel kernel)
    {
        if (_updateCodeContextFunction != null) return _updateCodeContextFunction;

        _updateCodeContextFunction = kernel.CreateFunctionFromPrompt(
            new PromptTemplateConfig("""
                あなたはプログラミング言語を読み解くプロフェッショナルを補助するためのコンテキストを生成します。
                コンテキストは "生成するコンテキストの JSON スキーマ" にある JSON スキーマに従った JSON 文字列として生成してください。
                コンテキストを生成するために、現在わかっているコンテキストと、現在読んでいる行の前にある数行のコードと、現在読んでいる行と、現在読んでいる先の数行先のコードが提供されます。
                この先のコードを読むために必要な覚えておくべき情報をコンテキストに追加してください。
                また、既に不要になったコンテキストは削除してください。
                不要になったコンテキストの例は、既にブロックを抜けて考慮が不要になったローカル変数などが該当します。その際に現在読んでいるプログラミング言語名は削除せずに残してください。

                ### 読んでいるプログラミング言語名
                {{$programingLanguage}}

                ### 生成するコンテキストの JSON スキーマ
                {{$schema}}
                
                ### 現在のコンテキスト
                {{$currentContext}}
                
                ### 手前のソースコード
                {{$previousCodeLines}}
                
                ### 今見ているソースコードの行
                {{$lineOfCurrentSourceCode}}
                
                ### 続きのソースコード
                {{$nextCodeLines}}
                """)
            {
                ExecutionSettings = new()
                {
                    [PromptExecutionSettings.DefaultServiceId] = new OpenAIPromptExecutionSettings()
                    {
                        ModelId = ModelIds.AnalyzeSourceCode,
#pragma warning disable SKEXP0010
                        ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
#pragma warning restore SKEXP0010
                    }
                }
            });
        return _updateCodeContextFunction;
    }
}
