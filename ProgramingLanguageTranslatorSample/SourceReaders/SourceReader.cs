using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ProgramingLanguageTranslatorSample.SourceReaders;

public interface ISourceReader
{
    IAsyncEnumerable<SourceCodeChunk> ReadAsync(
        string fileName,
        int maxPrevSourceToken,
        int maxTargetSourceToken,
        CancellationToken cancellationToken = default);
}

internal class DefaultSourceReader : ISourceReader
{
    private static Tokenizer _tokenizer = Tokenizer.CreateTiktokenForModel("gpt-35-turbo");
    public async IAsyncEnumerable<SourceCodeChunk> ReadAsync(
        string fileName, 
        int maxPrevSourceToken, 
        int maxTargetSourceToken,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        using var sr = new StreamReader(File.OpenRead(fileName));
        var prevSourceLines = new List<string>();
        var targetSourceLines = new List<string>();

        int currentTargetSourceToken = 0;
        while(!sr.EndOfStream)
        {
            var line = await sr.ReadLineAsync(cancellationToken);
            var currentLineToken = _tokenizer.CountTokens(line!);
            if (currentTargetSourceToken + currentLineToken > maxTargetSourceToken)
            {
                yield return new SourceCodeChunk(fileName, prevSourceLines.ToArray(), targetSourceLines.ToArray());
                prevSourceLines.AddRange(targetSourceLines);

                currentTargetSourceToken = 0;
                targetSourceLines.Clear();

                var tokensForPrevSourceLines = 0;
                prevSourceLines = prevSourceLines
                    .Reverse<string>()
                    .TakeWhile(x => (tokensForPrevSourceLines += _tokenizer.CountTokens(x)) < maxPrevSourceToken)
                    .Reverse()
                    .ToList();
            }

            currentTargetSourceToken += currentLineToken;
            targetSourceLines.Add(line!);
        }

        if (targetSourceLines.Any())
        {
            yield return new SourceCodeChunk(fileName, prevSourceLines.ToArray(), targetSourceLines.ToArray());
        }
    }
}

public record SourceCodeChunk(string FileName,
    string[] PrevSourceLines,
    string[] TargetSourceLines);
