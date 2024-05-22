using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProgramingLanguageTranslatorSample.JsonSchema;
using ProgramingLanguageTranslatorSample.Options;
using ProgramingLanguageTranslatorSample.SourceReaders;
using System.Text;
using System.Text.Json;

namespace ProgramingLanguageTranslatorSample;
internal interface IVBtoCSConverter
{
    Task ConvertAsync();
}

internal class DefaultVBtoCSConverter(
    IJsonSchemaGenerator jsonSchemaGenerator,
    Kernel kernel,
    ISourceReader sourceReader,
    IOptions<ConverterOption> options,
    ILogger<DefaultVBtoCSConverter> logger)
    : IVBtoCSConverter
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private const int ChunkSize = 50;
    private const int OverlapSize = 10;

    private static readonly string[] _attentions =
    [
        "VB は C# と異なりクラスやメソッドの開始や終了が `{` と `}` で括られていません。`Sub`, `Class`, `Function` などのキーワードで関数が始まり、`End Sub`, `End Class`, `End Function` などのキーワードで終わります。そのため括弧の開始忘れや閉じ括弧忘れが無いように細心の注意を払ってください。",
        "VB はクラスやメソッドの開始や終了が End Sub や End Function で終わる。そのため C# に変換する際には { と } で囲む必要がある。メソッド開始時点の { は忘れがちなので \"prevChunk\" や \"nextChunk\" や \"prevChunkConverted\" のソースコードを参考にして忘れないように変換してください。特に変換後の \"currentChunk\" と \"prevChunkConverted\" を結合したときに { の抜けが無いかどうかという観点で確認をしてください。",
        "\"nextChunk\" が空の配列の場合はファイルの終端になります。そのためクラスや名前空間の終了の閉じ括弧が必要になります。",
    ];

    private KernelFunction? _convertFunction;

    public async Task ConvertAsync()
    {
        foreach (var fileName in Directory.GetFiles(
            options.Value.SourceFolder,
            $"*.vb",
            new EnumerationOptions
            {
                RecurseSubdirectories = true
            }))
        {
            await ConvertFileAsync(
                options.Value.SourceFolder,
                fileName,
                options.Value.DestinationFolder);
        }
    }

    async Task ConvertFileAsync(
        string sourceFolder,
        string targetFileName,
        string outputFolderPath)
    {
        Directory.CreateDirectory(outputFolderPath);
        int chunkIndex = 1;
        var outputFilePath = targetFileName.Replace(sourceFolder, outputFolderPath).Replace(".vb", ".cs");
        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await using var sw = new StreamWriter(outputFilePath);
        string[] prevConverted = [];
        await foreach (var chunk in sourceReader.ReadAsync(targetFileName, ChunkSize))
        {
            Console.WriteLine($"Processing... {targetFileName}, chunk: {chunkIndex++}");
            prevConverted = await ProcessChunkAsync(chunk, kernel, sw, prevConverted, jsonSchemaGenerator);
            await Task.Delay(500);
        }

        await sw.FlushAsync();
    }

    async Task<string[]> ProcessChunkAsync(
        SourceCodeChunk chunk,
        Kernel kernel,
        StreamWriter sw,
        string[] prevConverted,
        IJsonSchemaGenerator jsonSchemaGenerator)
    {
        var aiInput = new AIInput(
            chunk.FileName,
            prevConverted.Length < OverlapSize ? prevConverted : prevConverted[^OverlapSize..],
            chunk.PrevChunk.Length < OverlapSize ? chunk.PrevChunk : chunk.PrevChunk[^OverlapSize..],
            chunk.CurrentChunk,
            chunk.NextChunk.Length < OverlapSize ? chunk.NextChunk : chunk.NextChunk[..OverlapSize]);

        var json = new StringBuilder();
        await foreach (var x in kernel.InvokeStreamingAsync(
            EnsureCreateConvertFunction(),
            new()
            {
                ["inputSchema"] = jsonSchemaGenerator.GenerateFromType<AIInput>(),
                ["outputSchema"] = jsonSchemaGenerator.GenerateFromType<AIOutput>(),
                ["attentions"] = string.Join('\n', _attentions.Select(x => $"- {x}")),
                ["input"] = JsonSerializer.Serialize(aiInput, _jsonSerializerOptions)
            }))
        {
            if (x is StreamingChatMessageContent chat)
            {
                logger.LogInformation(
                    "{fileName}: {content}",
                    chunk.FileName,
                    chat.Content);
                json.Append(chat.Content);
            }
        }

        try
        {
            var result = JsonSerializer.Deserialize<AIOutput>(json.ToString(), _jsonSerializerOptions);
            if (result != null)
            {
                foreach (var line in result.CurrentChunk)
                {
                    await sw.WriteLineAsync(line);
                }

                return result.CurrentChunk;
            }
            else
            {
                logger.LogWarning("Error: AI Result is {json}.", json);
                return [];
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Error: {json}", json);
            throw;
        }
    }

    private KernelFunction EnsureCreateConvertFunction()
    {
        if (_convertFunction != null) return _convertFunction;

        _convertFunction = kernel.CreateFunctionFromPrompt("""
            <message role="system">
            ### あなたへの指示
            プログラムのソースコードを別のプログラミング言語に変換するプロフェッショナルとして行動してください。
            変換元の言語は "Visual Basic (VB)" です。
            変換先の言語は "C#" です。
            変換元のプログラミング言語のソースコードは以下のような JSON 形式で提供されます。
            
            ```json:渡されるプログラミング言語のソースコードの JSON スキーマ
            {{$inputSchema}}
            ```
            
            ソースコードは "prevChunk" と "currentChunk" と "nextChunk" に分割されていますが、実際には連続したソースコードになります。
            "prevChunk" と "currentChunk" と "nextChunk" のソースコードを結合して変換してください。
            変換の際に "prevChunk" の部分の変換結果のソースコードの "prevChunkConverted" に続くように変換してください。
            変換結果は以下のような JSON 形式で提供してください。
            
            ```json:出力結果の JSON スキーマ
            {{$outputSchema}}
            ```
            
            ### 変換の際の注意点
            {{$attentions}}
            </message>
            <message role="user">
            {{$input}}
            </message>
            """,
            executionSettings: new OpenAIPromptExecutionSettings
            {
                Temperature = 0,
#pragma warning disable SKEXP0010
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
#pragma warning restore SKEXP0010
            });

        return _convertFunction;
    }
}
