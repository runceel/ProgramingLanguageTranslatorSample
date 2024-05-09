namespace ProgramingLanguageTranslatorSample.Options;
internal class ConverterOption
{
    public string SourceLanguage { get; set; } = "Visual Basic";
    public string SourceLanguageExtension { get; set; } = ".vb";
    public string DestinationLanguage { get; set; } = "C#";
    public string DestinationLanguageExtension { get; set; } = ".cs";
    public string SourceFolder { get; set; } = "C:\\Temp\\Source";
    public string DestinationFolder { get; set; } = "C:\\Temp\\Target";
    public string ModelDeploymentNameForAnalyzeContext { get; set; } = "gpt-4";
    public string ModelDeploymentNameForConvertSourceCode { get; set; } = "gpt-4-32k";
}
