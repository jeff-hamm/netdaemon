using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace NetDaemon.HassModel;

/// <summary>
/// This should be a part of DI but is used in too many places without access, presently
/// </summary>
[JsonSerializable(typeof(IEnumerable<HassEntity>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.General,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public partial class HassJsonContext : JsonSerializerContext
{
    
    private static HassJsonContext? _defaultWithFallback;
    public static JsonSerializerContext DefaultWithFallback => _defaultWithFallback ??= new HassJsonContext(CombineOptions());

    //public static IJsonTypeInfoResolver DefaultTypeInfoResolverWithFallback =>
    //    _resolver ??= ;

    private static JsonSerializerOptions? _options;

    public static JsonSerializerOptions DefaultOptions
    {
        get
        {
            if (_options == null)
            {
                _options =CombineOptions();
                _options.MakeReadOnly();
            }

            return _options;
        }
    }

    private static JsonSerializerOptions CombineOptions()
    {
        return new JsonSerializerOptions(Default.Options)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(Default, new DefaultJsonTypeInfoResolver())
        };
    }
}

public static class HassJsonExtensions
{
    public static IServiceCollection AddHassJson(this IServiceCollection services)
    {
        services.AddSingleton(HassJsonContext.DefaultOptions);
        services.AddSingleton(HassJsonContext.DefaultWithFallback);
        services.AddSingleton<IJsonTypeInfoResolver>(HassJsonContext.DefaultWithFallback);
        
        return services;
    }
}
