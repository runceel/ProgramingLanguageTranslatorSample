using System.Runtime.CompilerServices;

namespace ProgramingLanguageTranslatorSample.SourceReaders;

public interface ISourceReader
{
    public IAsyncEnumerable<SourceCodeChunk> ReadAsync(
        string fileName,
        int chunkBlockSize,
        CancellationToken cancellationToken = default);
}

internal class DefaultSourceReader : ISourceReader
{
    public async IAsyncEnumerable<SourceCodeChunk> ReadAsync(
        string fileName, 
        int chunkBlockSize,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        using var sr = new StreamReader(File.OpenRead(fileName));

        string[]? prevChunk = null;
        string[]? currentChunk = null;
        await foreach (var nextChunk in ReadLinesAsync(sr, chunkBlockSize))
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

    private static async IAsyncEnumerable<string[]> ReadLinesAsync(StreamReader sr, int chunkSize)
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

        if (list.Count != 0)
        {
            yield return list.ToArray();
        }
    }
}

public record SourceCodeChunk(string FileName,
    string[] PrevChunk,
    string[] CurrentChunk,
    string[] NextChunk);
