using System.Globalization;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.Client.Extensions;
using NetDaemon.Client.Settings;
using NetDaemon.Extensions.Logging;
using NetDaemon.HassModel;
using NetDaemon.Runtime;
using NuGet.Protocol;
using Serilog;
using Serilog.Events;

#pragma warning disable CA1303
if (args.Any(arg => arg.ToLower(CultureInfo.InvariantCulture) == "-help"))
{
    ShowUsage();
    return 0;
}
VersionHelper.PrintVersion();
await VersionValidator.ValidateLatestVersion();
VersionValidator.ValidatePackageReferences();


var host = Host.CreateDefaultBuilder(args)

    .ConfigureAppConfiguration(builder =>
    {
        builder.Sources.Clear();
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            // default path is the folder of the currently executing root assembly
            builder.AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{env}.json", true, true)

            // Also look in the current directory which will typically be the project folder
            // of the users code and have a appsettings.development.json with the correct HA connection settings
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile("appsettings.development.json", true, true)
            .AddUserSecrets<Program>()

            // finally override with Environment vars or commandline
            .AddEnvironmentVariables()
            .AddCommandLine(args, new Dictionary<string, string>
            {
                ["-o"] = "CodeGeneration:OutputFile",
                ["-f"] = "CodeGeneration:OutputFolder",
                ["-ns"] = "CodeGeneration:Namespace",
                ["-host"] = "HomeAssistant:Host",
                ["-port"] = "HomeAssistant:Port",
                ["-ssl"] = "HomeAssistant:Ssl",
                ["-token"] = "HomeAssistant:Token",
                ["-bypass-cert"] = "HomeAssistant:InsecureBypassCertificateErrors",
                ["-override"] = "CodeGeneration:EntityOverridesFile"
            });
    })
    .UseNetDaemonDefaultLogging()
    .UseSerilog(((c, sp, cfg) =>
    {
        cfg.ReadFrom.Configuration(c.Configuration)
            .Destructure.ByTransforming<JsonElement>(el => el.ToString())
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning).MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning).Enrich.FromLogContext();
    }))
    .ConfigureServices((context,s) =>
    {
        s.Configure<HomeAssistantSettings>(context.Configuration.GetSection("HomeAssistant"));
        s.Configure<CodeGenerationSettings>(context.Configuration.GetSection("CodeGeneration"));
        s.PostConfigure<CodeGenerationSettings>(o =>
        {
            o.GenerateOneFilePerEntity = args.Any(arg => arg.ToLower(CultureInfo.InvariantCulture) == "-fpe");
        });
        s.AddSingleton<CodeGenerationSettings>(s => s.GetRequiredService<IOptions<CodeGenerationSettings>>().Value);
        s.AddSingleton<HomeAssistantSettings>(s => s.GetRequiredService<IOptions<HomeAssistantSettings>>().Value);
        s.AddHomeAssistantClient();
        s.AddSingleton<Controller>();
    })
    .Build();


var logger = host.Services.GetRequiredService<ILogger<Program>>();
try
{
    var controller = host.Services.GetRequiredService<Controller>();
    await controller.RunAsync();
    logger.LogInformation("Code Generated successfully!");
    return 0;
}
catch (Exception e)
{
    logger.LogError(e, "Failed to generate code");
    return 1;
}



void ShowUsage()
{
    Console.WriteLine(@"
    Usage: nd-codegen [options] -ns namespace -o outfile
    Options:
        -f           : Name of folder to output files (folder name only)
        -fpe         : Create separate file per entity (ignores -o option)
        -host        : Host of the netdaemon instance
        -port        : Port of the NetDaemon instance
        -ssl         : true if NetDaemon instance use ssl
        -token       : A long lived HomeAssistant token
        -bypass-cert : Ignore certificate errors (insecure)
        -override    : Oveeeeeeerrides file for entity metadata

    These settings is valid when installed codegen as global dotnet tool.
            ");
}
