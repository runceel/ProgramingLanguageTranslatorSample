using System.ComponentModel;

namespace ProgramingLanguageTranslatorSample.Memories;
internal class CodeContext
{
    [Description("現在読んでいるファイルで読み込まれている名前空間の一覧")]
    public string[] UsingNamespaces { get; set; } = [];
    [Description("現在読んでいる名前空間")]
    public string CurrentNamespace { get; set; } = "";
    [Description("現在読んでいるクラス名")]
    public string CurrentClassName { get; set; } = "";
    [Description("現在読んでいるメソッド名")]
    public string CurrentMethodName { get; set; } = "";
    [Description("現在読んでいるメソッドの戻り値の型")]
    public string CurrentMethodReturnType { get; set; } = "";
    [Description("現在読んでいるメソッドの引数の型と名前の一覧")]
    public string[] CurrentMethodArguments { get; set; } = [];
    [Description("その他、ソースコードを変換するために覚えて置いた方が良い事項 (ローカル変数名などの先のコードを理解するために必要そうな情報)")]
    public string[] OtherMemos { get; set; } = [];
}
