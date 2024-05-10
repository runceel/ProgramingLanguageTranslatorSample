using System.ComponentModel;

record AIInput(
    [property: Description("変換処理中のファイル名")]
    string FileName,
    [property: Description("変換対象の手前の VB のソースコードを C# に変換したもの")]
    string[] PrevChunkConverted,
    [property: Description("変換対象の手前の VB のソースコード")]
    string[] PrevChunk,
    [property: Description("変換対象の VB のソースコード")]
    string[] CurrentChunk,
    [property: Description("変換対象の後続の VB のソースコード")]
    string[] NextChunk);
