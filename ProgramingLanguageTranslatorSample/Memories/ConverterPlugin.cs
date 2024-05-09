using Azure.AI.OpenAI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProgramingLanguageTranslatorSample.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProgramingLanguageTranslatorSample.Memories;
internal class ConverterPlugin(IOptions<ConverterOption> options,
    ILogger<ConverterPlugin> logger)
{
    private KernelFunction? _convertFunction;

    [KernelFunction]
    public async Task<string> ConvertAsync(
        string sourceCode,
        CodeContext codeContext,
        Kernel kernel,
        CancellationToken cancellationToken)
    {
        var convertFunction = EnsureCreateConvertFunction(kernel, options.Value);
        var result = await kernel.InvokeAsync<string>(convertFunction,
                       new()
                       {
                           ["sourceCode"] = sourceCode,
                           ["codeContext"] = JsonSerializer.Serialize(codeContext),
                       }).ConfigureAwait(false);
        if (result == null)
        {
            logger.LogWarning("変換結果が null です。変換元: {sourceCode}", sourceCode);
        }

        return result ?? "";
    }

    private KernelFunction EnsureCreateConvertFunction(Kernel kernel, ConverterOption option)
    {
        if (_convertFunction != null) return _convertFunction;

        _convertFunction = kernel.CreateFunctionFromPrompt(
            new PromptTemplateConfig($$$"""
                <message role="system">
                あなたはプログラムのソースコードを他のプログラミング言語に変換するプロフェッショナルとして振舞ってください。
                与えられたプログラムのコードを以下の手順で他のプログラミング言語に変換してください。
                出力は会話などは含めずに変換後のプログラミング言語のコードのみを出力してください。

                ### 変換手順
                1. 変換元のプログラミング言語を確認してください。
                2. 変換先のプログラミング言語を確認してください。
                3. ユーザーから与えられたプログラムのコードを確認してください。
                4. ユーザーから与えられたプログラムのコードを変換する際に参考になる情報が "変換元のプログラムのコードのコンテキスト" です。内容を確認してください。
                5. ユーザーから与えられたプログラムのコードだけを変換先のプログラミング言語に変換してください。その際に余分なコードを補完しないようにしてください。

                ### 変換元のプログラミング言語
                {{{option.SourceLanguage}}}

                ### 変換先のプログラミング言語
                {{{option.DestinationLanguage}}}

                ### 変換元のプログラムのコードのコンテキスト
                {{$codeContext}}
                </message>
                <message role="user">
                using System;
                </message>
                <message>
                Import System
                </message>
                <message role="user">
                Console.WriteLine("Hello, World!");
                </message>
                <message>
                Console.WriteLine("Hello, World!")
                </message>
                <message role="user">
                {{$sourceCode}}
                </message>
                """)
            {
                ExecutionSettings = new()
                {
                    [PromptExecutionSettings.DefaultServiceId] = new OpenAIPromptExecutionSettings()
                    {
                        ModelId = ModelIds.ConvertSourceCode,
                    }
                }
            });

        return _convertFunction;
    }
}
