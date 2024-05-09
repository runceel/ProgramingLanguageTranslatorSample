using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProgramingLanguageTranslatorSample;
using ProgramingLanguageTranslatorSample.Memories;
using Microsoft.SemanticKernel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProgramingLanguageTranslatorSample.Options;
using ProgramingLanguageTranslatorSample.SourceReaders;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProgramingLanguageTranslatorServices(builder.Configuration);
var app = builder.Build();

var kernel = app.Services.GetRequiredService<Kernel>();
var reader = app.Services.GetRequiredService<ISourceReader>();

var options = app.Services.GetRequiredService<IOptions<ConverterOption>>().Value;
const int TokensForPrevSource = 1000;
const int TokensForTargetSource = 500;

foreach (var fileName in Directory.GetFiles(
    options.SourceFolder,
    $"*{options.SourceLanguageExtension}",
    new EnumerationOptions { RecurseSubdirectories = true }))
{
    await ConvertAsync(fileName, options.DestinationFolder);
}

async Task ConvertAsync(string targetFileName, string outputFolderPath)
{
    Directory.CreateDirectory(outputFolderPath);
    int chunkIndex = 1;
    var outputFilePath = targetFileName.Replace(options.SourceFolder, outputFolderPath).Replace(options.SourceLanguageExtension, options.DestinationLanguageExtension);
    var outputDirectory = Path.GetDirectoryName(outputFilePath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await using var sw = new StreamWriter(outputFilePath);
    string[] prevConvertedSource = [];
    await foreach (var chunk in reader.ReadAsync(targetFileName, TokensForPrevSource, TokensForTargetSource))
    {
        Console.WriteLine($"Processing... {targetFileName}, chunk: {chunkIndex++}");
        var result = await kernel.InvokePromptAsync($$$"""
            ### Instructions to you
            あなたはプログラムのソースコードを他のプログラミング言語に変換するプロフェッショナルとして振舞ってください。
            与えられたプログラムのコードを以下の手順で他のプログラミング言語に変換してください。

            1. "Source programing language" を確認して変換元のプログラミング言語が何であるか理解してください。
            2. "Destination programing language" を確認して変換先のプログラミング言語が何であるか理解してください。
            3. "Code prior to the code to be converted" には変換するコードの前にあるコードが含まれています。このコードは参考情報としてのみ提供され、変換されるべきではありません。"Code to be converted" を変換する際の参考情報として参照してください。
            4. "Code to be converted" には変換するコードが含まれています。このコードは巨大なコードの一部分である可能性があります。このコードのみを変換先のプログラミング言語に変換してください。変換する際には、余分なコードを追加したり、元のコードを変更したりしないようにしてください。
            5. 1から4の内容を踏まえて "Code to be converted" のコードを変換先のプログラミング言語に変換してください。
            6. 変換後のコードを以下の観点で見直しておかしな点があれば修正してください。
                - "Code to be converted" は大きなコードの一部である可能性があるため、不適切な閉じカッコが追加されていないか、元のコードが変更されていないか確認してください。
                - 変換後のコードには会話などは含めず、変換後のプログラミング言語のコードのみを出力してください。
                - 変換後のコードからコンパイルエラーを消す目的のために余分に追加されたコードがあれば削除してください。
                - 最後に、変換後のコードが元のコードと1対1に対応するようにきちんと変換されているか確認してください。
            7. 熟考して、問題がないと判断したら変換後のソースコードを提出してください。

            ### Source programming language
            {{{options.SourceLanguage}}}

            ### Destination programming language
            {{{options.DestinationLanguage}}}

            ### Code prior to the code to be converted
            {{{string.Join('\n', chunk.PrevSourceLines)}}}

            ### Code to be converted
            {{{string.Join('\n', chunk.TargetSourceLines)}}}
            """);


        prevConvertedSource = (result.GetValue<string>()?.Split('\n') ?? [])
            .Select(x => x.TrimEnd())
            .Where(x => !x.StartsWith("```"))
            .ToArray();
        foreach (var line in prevConvertedSource)
        {
            await sw.WriteLineAsync(line);
        }

        await Task.Delay(500);
    }

    await sw.FlushAsync();
}
