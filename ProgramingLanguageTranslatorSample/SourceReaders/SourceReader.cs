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
    public IAsyncEnumerable<SourceCodeChunk> ReadAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}

internal class DefaultSourceReader : ISourceReader
{
    private static Tokenizer _tokenizer = Tokenizer.CreateTiktokenForModel("gpt-35-turbo");
    public async IAsyncEnumerable<SourceCodeChunk> ReadAsync(
        string fileName, 
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        using var sr = new StreamReader(File.OpenRead(fileName));

        string[]? prevChunk = null;
        string[]? currentChunk = null;
        await foreach (var nextChunk in ReadLinesAsync(sr, 100))
        {
            if (currentChunk != null)
            {
                yield return new SourceCodeChunk(fileName, prevChunk ?? [], currentChunk, nextChunk);
            }

            prevChunk = currentChunk;
            currentChunk = nextChunk;
        }

        if (currentChunk != null)
        {
            yield return new SourceCodeChunk(fileName, prevChunk ?? [], currentChunk, []);
        }
    }

    private async IAsyncEnumerable<string[]> ReadLinesAsync(StreamReader sr, int chunkSize)
    {
        var list = new List<string>();
        while (!sr.EndOfStream)
        {
            var line = await sr.ReadLineAsync();
            list.Add(line!);
            if (list.Count >= chunkSize)
            {
                yield return list.ToArray();
                list.Clear();
            }
        }

        if (list.Any())
        {
            yield return list.ToArray();
        }
    }
}

public record SourceCodeChunk(string FileName,
    string[] PrevChunk,
    string[] CurrentChunk,
    string[] NextChunk);
