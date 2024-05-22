# OpenAI を使った VB から C# への変換

詳細は以下の記事を参照してください。

https://zenn.dev/microsoft/articles/convert-vb-to-cs

## 使用方法

1. JSON モードをサポートしているバージョンの gpt 4 系のモデルのデプロイ (gpt-3.5-turbo でも動作しますが精度は悪くなります)
2. Azure OpenAI Service に対してローカルの Azure CLI でサインインしているユーザーに `Cognitive Services OpenAI ユーザー` のロールを付与
    - 参考: https://learn.microsoft.com/ja-jp/azure/ai-services/openai/how-to/role-based-access-control
3. `appsettings.json` の内容を自分の環境にあわせて書き換えてください
    ```json:appsettings.json
    {
      "OpenAIClient": {
        // Azure OpenAI Service の エンドポイント
        "Endpoint": "https://リソース名.openai.azure.com/"
      },
      "ConverterOption": {
        // 変換元の VB のソースコードが格納されているフォルダ
        "SourceFolder": "C:\\Temp\\Source",
        // 変換結果の格納先フォルダ
        "DestinationFolder": "C:\\Temp\\Target",
        // 変換に使用するモデルのデプロイ名
        "ModelDeploymentNameForConvertSourceCode": "gpt-4"
      }
    }
    ```
4. 実行して変換が行われるか確認してください

## Tips

### マネージド ID 認証が使えない場合

`ServiceCollectionExtensions.cs` の `clientBuilder.AddOpenAIClient(configuration.GetSection(nameof(OpenAIClient)));` の行で OpenAI Service のクライアントを登録しています。
`AddOpenAIClient` メソッドにはエンドポイントと API キーを受け取るオーバーロードがあるので、そちらを使って登録するように変更してください。

動作確認のためだけでハードコードをする場合は以下のようになります。

```csharp
clientBuilder.AddOpenAIClient(new Uri("https://...."), new AzureKeyCredential("API Key"));
```

