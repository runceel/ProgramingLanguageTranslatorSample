using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProgramingLanguageTranslatorSample;
using ProgramingLanguageTranslatorSample.Memories;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProgramingLanguageTranslatorSample.Options;
using ProgramingLanguageTranslatorSample.SourceReaders;
using System.Diagnostics;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddProgramingLanguageTranslatorServices(builder.Configuration);
        var app = builder.Build();

        var kernel = app.Services.GetRequiredService<Kernel>();
        var reader = app.Services.GetRequiredService<ISourceReader>();

        var options = app.Services.GetRequiredService<IOptions<ConverterOption>>().Value;

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

            await using (var sw = new StreamWriter(outputFilePath))
            {
                await foreach (var chunk in reader.ReadAsync(targetFileName))
                {
                    Console.WriteLine($"Processing... {targetFileName}, chunk: {chunkIndex++}");
                    await ProcessChunkAsync(chunk, kernel, options, sw);
                    //var result = await kernel.InvokePromptAsync($$$"""
                    //    ### Instructions to you
                    //    あなたはプログラムのソースコードを他のプログラミング言語に変換するプロフェッショナルとして振舞ってください。
                    //    与えられたプログラムのコードを以下の手順で他のプログラミング言語に変換してください。

                    //    1. "変換元のプログラミング言語" を確認して変換元のプログラミング言語が何であるか理解してください。
                    //    2. "変換先のプログラミング言語" を確認して変換先のプログラミング言語が何であるか理解してください。
                    //    3. "変換対象のコード" には変換するコードが含まれています。このコードは巨大なコードの一部分である可能性があります。このコードのみを変換先のプログラミング言語に変換してください。変換する際には、余分なコードを追加したり、元のコードを変更したりしないようにしてください。
                    //    4. "変換対象のコードの後に続くコード" には変換するコードの後ろに続くコードが含まれています。このコードは参考情報としてのみ提供され、変換されるべきではありません。"変換対象のコード" を変換する際の参考情報として参照してください。
                    //    5. 1から4の内容を踏まえて"変換対象のコード" のコードを"変換先のプログラミング言語"に変換してください。
                    //    6. 変換後のコードを以下の観点で見直しておかしな点があれば修正してください。
                    //        - "変換元のプログラミング言語" は大きなコードの一部である可能性があるため、不適切な閉じカッコが追加されていないか、元のコードが変更されていないか確認してください。
                    //        - 変換後のコードは "変換対象のコードの後に続くコード" を "変換先のプログラミング言語" に変換したものを追加してもエラーが起きることがないように余分なコードを追加していないか確認してください。
                    //        - 変換後のコードには会話などは含めず、変換後のプログラミング言語のコードのみを出力してください。
                    //        - 変換後のコードからコンパイルエラーを消す目的のために余分に追加されたコードがあれば削除してください。
                    //        - 最後に、変換後のコードが元のコードと1対1に対応するようにきちんと変換されているか確認してください。
                    //    7. 熟考して、問題がないと判断したら変換後のソースコードを提出してください。

                    //    ### 変換元のプログラミング言語
                    //    {{{options.SourceLanguage}}}

                    //    ### 変換先のプログラミング言語
                    //    {{{options.DestinationLanguage}}}

                    //    ### 変換対象のコード
                    //    ```
                    //    {{{string.Join('\n', chunk.CurrentChunk)}}}
                    //    ```

                    //    ### 変換対象のコードの後に続くコード
                    //    ```
                    //    {{{string.Join('\n', chunk.NextChunk)}}}
                    //    ```
                    //    """);
                    //var result = await kernel.InvokeStreamingAsync(function);

                    //var convertedSource = (result.GetValue<string>()?.Split('\n') ?? [])
                    //    .Select(x => x.TrimEnd())
                    //    .Where(x => !x.StartsWith("```"))
                    //    .ToArray();
                    //foreach (var line in convertedSource)
                    //{
                    //    await sw.WriteLineAsync(line);
                    //}

                    await Task.Delay(500);
                }

                await sw.FlushAsync();
            }

            var lines = new List<string>();
            using (var sr = new StreamReader(outputFilePath))
            {
                while(!sr.EndOfStream)
                {
                    var line = await sr.ReadLineAsync();
                    if (line?.StartsWith("```") ?? true)
                    {
                        continue;
                    }

                    lines.Add(line);
                }
            }

            File.Delete(outputFilePath);
            await File.WriteAllLinesAsync(outputFilePath, lines);
        }

    }

    private static async Task ProcessChunkAsync(SourceCodeChunk chunk, Kernel kernel, ConverterOption options, StreamWriter sw)
    {
        var function = kernel.CreateFunctionFromPrompt($$$"""
                ### Instructions to you
                You are to act as a professional who converts program source code from one programming language to another.
                Please convert the given program code to another programming language following the steps below.

                1. Check the "Source programming language" to understand what the original programming language is.
                2. Check the "Destination programming language" to understand what the target programming language is.
                3. "Code to be converted" contains the code to be converted. This code may be a part of a larger code. Convert only this code to the target programming language. When converting, do not add extra code or modify the original code.
                4. "Code following the code to be converted" contains code that follows the code to be converted. This code is provided only as reference information and should not be converted. Refer to it when converting the "Code to be converted".
                5. Based on the contents from 1 to 4, convert the "Code to be converted" to the target programming language.
                6. Review the converted code from the following perspectives and correct any issues.
                    - Since "Source programming language" may be a part of a larger code, check if any inappropriate closing brackets have been added or if the original code has been modified.
                    - Ensure that the converted code does not add any extra code that would cause errors when the "Code following the code to be converted" is converted to the target programming language.
                    - Do not include conversations in the converted code, only output the code in the target programming language.
                    - If there is any extra code added to eliminate compile errors from the converted code, remove it.
                    - Finally, check if the converted code has been properly converted to correspond one-to-one with the original code.
                7. After careful consideration, if you determine there are no issues, submit the converted source code.

                ### Source programming language
                {{{options.SourceLanguage}}}

                ### Destination programming language
                {{{options.DestinationLanguage}}}

                ### Code to be converted
                ```
                {{{string.Join('\n', chunk.CurrentChunk)}}}
                ```

                ### Code following the code to be converted
                ```
                {{{string.Join('\n', chunk.NextChunk)}}}
                ```
                """,
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    Temperature = 0,
                });

        await foreach (var x in kernel.InvokeStreamingAsync(function))
        {
            if (x is StreamingChatMessageContent chat)
            {
                Console.WriteLine(chat.Content);
                await sw.WriteAsync(chat.Content);
            }
        }
    }
}