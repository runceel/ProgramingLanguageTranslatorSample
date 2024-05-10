using System.ComponentModel;

record AIOutput(
    [property: Description("変換後の手前の C# のソースコード")]
    string[] PrevChunk,
    [property: Description("変換後の C# のソースコード")]
    string[] CurrentChunk,
    [property: Description("変換後の後続の C# のソースコード")]
    string[] NextChunk);
