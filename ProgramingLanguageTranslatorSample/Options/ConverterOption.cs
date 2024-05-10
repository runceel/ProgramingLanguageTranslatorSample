namespace ProgramingLanguageTranslatorSample.Options;
internal class ConverterOption
{
    public string SourceFolder { get; set; } = "C:\\Temp\\Source";
    public string DestinationFolder { get; set; } = "C:\\Temp\\Target";
    public string ModelDeploymentNameForConvertSourceCode { get; set; } = "gpt-4-32k";
}
