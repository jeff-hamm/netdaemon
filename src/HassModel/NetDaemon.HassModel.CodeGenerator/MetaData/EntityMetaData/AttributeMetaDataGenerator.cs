using System.Text.RegularExpressions;
using NetDaemon.Client.HomeAssistant.Model;

namespace NetDaemon.HassModel.CodeGenerator;

internal static class AttributeMetaDataGenerator
{
    public static IEnumerable<EntityAttributeMetaData> GetMetaDataFromEntityStates(string domain, IEnumerable<HassState> entityStates, CodeGenerationSettings? options=null)
    {
        options ??= new();
        // Get all attributes of all entities in this set
        var jsonProperties = entityStates.SelectMany(s =>
            s.AttributesJson?.EnumerateObject().Select(json => (json,s,json.Value is {ValueKind:JsonValueKind.Array})) ?? Enumerable.Empty<(JsonProperty,HassState,bool)>());

        // Group the attributes from all entities in this set by JsonPropertyName
        var jsonPropetiesByName = jsonProperties.GroupBy(p => p.json.Name);

        // find the candidate CSharp name and the best ClrType for each unique json property
        var attributeGroups = jsonPropetiesByName
            .SelectMany(group =>
                group.Select(p =>
                new {
                    metadata= ToMetadata(domain.ToValidCSharpPascalCase(),group.Key, group.Select(p => p.json.Value),options),
                    p.s,
                    p.Item3
                    })
                    )
            .GroupBy(p => p.metadata.CSharpName)
                
            .ToArray();
        foreach (var ag in attributeGroups)
        {
            var g = ag.GroupBy(kvp => kvp.metadata, new EntityAttributeMetaDataComparer())
                .ToArray();
            if(g.Length == 0) continue;

            if (g.Length == 1 || g[0].Key.Values == null || g[0].Key?.Values?.Count == 0)
                yield return g[0].Key with
                {
                    IsList = g[0].Any(s => s.Item3)
                };
            else
            {
                var ix = 0;
                foreach (var sg in g)
                {
                    yield return sg.Key with
                    {
                        ClrType = sg.Key.ClrType + "_" + ix,
                        IsList = sg.First().Item3
                    };
                    ix++;
                }
            }

        }
    } 

    class EntityAttributeMetaDataComparer : IEqualityComparer<EntityAttributeMetaData>
    {
        public bool Equals(EntityAttributeMetaData? x, EntityAttributeMetaData? y) => (x?.CSharpName== y?.CSharpName) &&
                                                                                      (x?.TypeName == y?.TypeName) &&
                                                                                      ((x?.Values == null && y?.Values == null) ||
                x?.Values is {} xv && y?.Values is { } yv &&
                                                                                      xv.Order().SequenceEqual(yv.Order(), new InvariantStringComparer()));
        public int GetHashCode(EntityAttributeMetaData obj) =>
            HashCode.Combine(obj.CSharpName.GetHashCode(StringComparison.OrdinalIgnoreCase),
            obj.Values?.Aggregate(0,(h,s) => HashCode.Combine(h,s.GetHashCode(StringComparison.OrdinalIgnoreCase))));
    }

    class InvariantStringComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x?.Equals(y, StringComparison.OrdinalIgnoreCase) ?? false;
        public int GetHashCode(string obj) => obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    private static EntityAttributeMetaData ToMetadata(string domain, string JsonName, IEnumerable<JsonElement> valueKinds, CodeGenerationSettings options)
    {
        var ClrTypeName = GetBestClrTypeName(ToName(domain, JsonName),valueKinds,options);
        return new EntityAttributeMetaData(JsonName, JsonName.ToValidCSharpPascalCase(), ClrTypeName);
    }

    private static string ToName(string domain, string JsonName)
    {
        return domain.ToValidCSharpPascalCase() + JsonName.ToValidCSharpPascalCase();
    }

    private static CustomType? GetBestClrTypeName(string BaseName, IEnumerable<JsonElement> valueKinds, CodeGenerationSettings options)
    {
        var distinctCrlTypes = valueKinds
            .Where(e => e.ValueKind != JsonValueKind.Null) // null fits in any type so we can ignore it for now
            .GroupBy(e => MapJsonType(e,options))
            .ToHashSet();

        // If we have no items left that means all attributes had JsonValueKind.Null,
        // this can occur eg for light brightness if all lights are currently off
        // In that case we will not determine a clrType here because there might be a better type when this
        // metadata gets merged
        if (distinctCrlTypes.Count == 0) return null;

        // Multiple types were found so we need to resort to object
        if (distinctCrlTypes.Count > 1) return CustomType.ClrObject;

        // For arrays, we want to enumerate the sub-elements of the array. If there's a single
        // element type, then we'll construct an IROList<subtype>, otherwise we'll construct
        // IROList<object>
        var clrTypeGroup = distinctCrlTypes.Single();
        if (clrTypeGroup.Key.ClrType == typeof(IReadOnlyList<>))
        {
            var children = clrTypeGroup.SelectMany(el => el.EnumerateArray()).ToArray();
            var listSubTypeName = GetBestClrTypeName(BaseName,children,options) ?? CustomType.ClrObject;

            if (listSubTypeName.ClrType != typeof(string))
                return CustomType.ListOfType(listSubTypeName.TypeName);

            var values = children.Select(v => v.GetString()??"")
                .Where(s =>  s != string.Empty).DistinctBy(s => s.ToLowerInvariant()).ToArray();
            if(values.Length<2)
                return CustomType.ListOfType(listSubTypeName.TypeName);

            return CustomType.ListOfType(BaseName,values);

        }

        return clrTypeGroup.Key;
    }

    private static CustomType MapJsonType(JsonElement e, CodeGenerationSettings options) =>
        e switch
        {
            {ValueKind:JsonValueKind.False } => CustomType.FromClrType(typeof(bool)),
            {ValueKind:JsonValueKind.Undefined } => CustomType.ClrObject,
            {ValueKind:JsonValueKind.Object } => CustomType.ClrObject,
            {ValueKind:JsonValueKind.Array } => CustomType.FromClrType(typeof(IReadOnlyList<>)),
//            JsonValueKind.String } => typeof(string),
            {ValueKind:JsonValueKind.String } => ToStringType(e,options),
            {ValueKind:JsonValueKind.Number } => CustomType.FromClrType(typeof(double)),
            {ValueKind:JsonValueKind.True } => CustomType.FromClrType(typeof(bool)),
            {ValueKind:JsonValueKind.Null } => CustomType.ClrObject,
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.ValueKind, null)
        };

    private static CustomType ToStringType(JsonElement jsonElement, CodeGenerationSettings options)
    {
        if (jsonElement.GetString() is { } s && s.StartsWith(options.Namespace, StringComparison.InvariantCultureIgnoreCase))
            return new CustomType(s);
        return CustomType.FromClrType(typeof(string));
    }
}

public record CustomType(string TypeName, Type? ClrType = null, IReadOnlyList<string>? KnownValues = null) : IEquatable<CustomType>
{
    public bool ToGenerate => KnownValues?.Count > 0;
    public virtual bool Equals(CustomType? other) => TypeName.Equals(other?.TypeName, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode()
    {
        return TypeName.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    public static readonly CustomType ClrObject = new CustomType(typeof(object).GetFriendlyName(), typeof(object));
    public static CustomType FromClrType(Type type) => new CustomType(type.GetFriendlyName(), type);
    public static CustomType ListOfType(string type) => new CustomType($"IReadOnlyList<{type}>");
    public static CustomType ListOfType(string type, IReadOnlyList<string> values) => new CustomType(type, KnownValues: values);
}
