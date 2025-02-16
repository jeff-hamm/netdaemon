using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetDaemon.Client;
using NetDaemon.Client.Settings;
using Serilog.Core;
[assembly: InternalsVisibleTo("NetDaemon.Extensions.SourceGen")]

namespace NetDaemon.HassModel.CodeGenerator;

#pragma warning disable CA1303
#pragma warning disable CA2000 // because of await using ... configureAwait()
internal class Controller(CodeGenerationSettings generationSettings, HomeAssistantSettings haSettings,
    IHostApplicationLifetime applicationLifetime,
    IHomeAssistantClient client, ILogger<Controller> logger) : IHostedService
{
    private const string ResourceName = "NetDaemon.HassModel.CodeGenerator.MetaData.DefaultMetadata.DefaultEntityMetaData.json";

    private string EntityMetaDataFileName => Path.Combine(OutputFolder, "EntityMetaData.json");
    private string ServicesMetaDataFileName => Path.Combine(OutputFolder, "ServicesMetaData.json");

    private string OutputFolder => string.IsNullOrEmpty(generationSettings.OutputFolder)
        ? Directory.GetParent(Path.GetFullPath(generationSettings.OutputFile))!.FullName
        : generationSettings.OutputFolder;

    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var (hassStates, servicesMetaData) = await HaRepositry.GetHaData(
            client, haSettings).ConfigureAwait(false);

        var previousEntityMetadata = await LoadEntitiesMetaDataAsync().ConfigureAwait(false);
        var currentEntityMetaData = EntityMetaDataGenerator.GetEntityDomainMetaData(hassStates,generationSettings);
        var mergedEntityMetaData = EntityMetaDataMerger.Merge(generationSettings, previousEntityMetadata, currentEntityMetaData);
        if (await TryReadMetadata(generationSettings.EntityOverridesFile     ) is {} overrides)
        {
            mergedEntityMetaData = EntityMetaDataMerger.Overwrite(generationSettings, mergedEntityMetaData, overrides);
        }
        await Save(mergedEntityMetaData, EntityMetaDataFileName).ConfigureAwait(false);
        await Save(servicesMetaData, ServicesMetaDataFileName).ConfigureAwait(false);

        var hassServiceDomains = ServiceMetaDataParser.Parse(servicesMetaData!.Value, out var deserializationErrors);

        var generatedTypes = Generator.GenerateTypes(mergedEntityMetaData.Domains, hassServiceDomains);

        SaveGeneratedCode(generatedTypes);
        CheckParseErrors(deserializationErrors,logger);
        applicationLifetime.StopApplication();
    }

    internal static void CheckParseErrors(List<DeserializationError> parseErrors, ILogger logger)
    {

        if (parseErrors.Count == 0) return;

        Console.ForegroundColor = ConsoleColor.Yellow;
        WriteLine("""
                          Errors occured while parsing metadata from Home Assistant for one or more services.
                          This is usually caused by metadata from HA that is not in the expected JSON format.
                          nd-codegen will try to continue to generate code for other services.
                          """,logger);
        Console.ResetColor();
        foreach (var deserializationError in parseErrors)
        {
            WriteLine("",logger);
            WriteLine(deserializationError.Exception,logger);
            WriteLine(deserializationError.Context + " = ",logger);
            Console.Out.Flush();
            WriteLine(JsonSerializer.Serialize(deserializationError.Element, new JsonSerializerOptions{WriteIndented = true}),logger);
        }
    }

    internal async Task<EntitiesMetaData> LoadEntitiesMetaDataAsync()
    {
        if(await TryReadMetadata(EntityMetaDataFileName) is {} metaData)
            return metaData;
        await using var fileStream = GetDefaultMetaDataFileFromResource();
        return await ReadMetadata(fileStream);
    }
    
    private static async Task<EntitiesMetaData?> TryReadMetadata(string? fileName, JsonSerializerOptions? options=null)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }

        await using var fileStream = File.OpenRead(fileName);
        var loaded = await JsonSerializer.DeserializeAsync<EntitiesMetaData>(fileStream, options?? JsonSerializerOptions).ConfigureAwait(false);
        return loaded ?? new EntitiesMetaData();

    }
    private static async Task<EntitiesMetaData> ReadMetadata(Stream fileStream)
    {
        var loaded = await JsonSerializer.DeserializeAsync<EntitiesMetaData>(fileStream, JsonSerializerOptions).ConfigureAwait(false);
        return loaded ?? new EntitiesMetaData();}

    private static Stream GetDefaultMetaDataFileFromResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream(ResourceName)!;
    }

    private async Task Save<T>(T merged, string fileName)
    {
        Directory.CreateDirectory(OutputFolder);

        var fileStream = File.Create(fileName);
        await using var _ = fileStream.ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(fileStream, merged, JsonSerializerOptions).ConfigureAwait(false);
    }

    private static JsonSerializerOptions JsonSerializerOptions =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new ClrTypeJsonConverter() }
        };

    private void SaveGeneratedCode(MemberDeclarationSyntax[] generatedTypes)
    {
        if (!generationSettings.GenerateOneFilePerEntity)
        {
            WriteLine("Generating single file for all entities.");
            var unit = Generator.BuildCompilationUnit(generationSettings.Namespace, [.. generatedTypes]);

            Directory.CreateDirectory(Directory.GetParent(generationSettings.OutputFile)!.FullName);

            using var writer = new StreamWriter(generationSettings.OutputFile);
            unit.WriteTo(writer);

            WriteLine(Path.GetFullPath(generationSettings.OutputFile));
        }
        else
        {
            WriteLine("Generating separate file per entity.");

            Directory.CreateDirectory(OutputFolder);

            foreach (var type in generatedTypes)
            {
                var unit = Generator.BuildCompilationUnit(generationSettings.Namespace, type);
                using var writer = new StreamWriter(Path.Combine(OutputFolder, $"{unit.GetClassName().ToValidCSharpIdentifier()}.cs"));
                unit.WriteTo(writer);
            }

            WriteLine($"Generated {generatedTypes.Length} files.");
            WriteLine(OutputFolder);
        }
    }

    private void WriteLine(Exception ex)
    {
        Console.WriteLine(ex);
        logger.LogError(ex, "Error");
    }

    private void WriteLine()
    {
        Console.WriteLine();
    }
    
    private static void WriteLine(Exception ex, ILogger logger)
    {
        Console.WriteLine(ex);
        logger.LogError(ex, "Error");
    }

    private static void WriteLine(string p0, ILogger logger)
    {
        Console.WriteLine(p0);
        logger.LogInformation(p0);
    }
    private void WriteLine(string p0)
    {
        Console.WriteLine(p0);
        logger.LogInformation(p0);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
