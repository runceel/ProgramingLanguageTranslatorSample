using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProgramingLanguageTranslatorSample;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProgramingLanguageTranslatorServices(builder.Configuration);
var app = builder.Build();

await app.Services
    .GetRequiredService<IVBtoCSConverter>()
    .ConvertAsync();
