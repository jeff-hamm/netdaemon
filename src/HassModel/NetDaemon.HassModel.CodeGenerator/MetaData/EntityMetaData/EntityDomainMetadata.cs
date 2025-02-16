using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetDaemon.HassModel.CodeGenerator;

record EntitiesMetaData
{
    public IReadOnlyCollection<EntityDomainMetadata> Domains { get; init; } = [];
}

record EntityDomainMetadata(
    string Domain,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool IsNumeric,

    IReadOnlyList<EntityMetaData> Entities,

    IReadOnlyList<EntityAttributeMetaData> Attributes
    )
{
    private static readonly HashSet<string> CoreInterfaces =
        typeof(IEntityCore).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.IsAssignableTo(typeof(IEntityCore)))
            .Select(t => t.Name)
            .ToHashSet();

    private readonly string prefixedDomain = (IsNumeric && EntityIdHelper.MixedDomains.Contains(Domain)  ? "numeric_" : "") + Domain;

    [JsonIgnore]
    public string EntityClassName => $"{prefixedDomain}Entity".ToValidCSharpPascalCase();

    [JsonIgnore]
    public string EntityIdsClassName => $"{prefixedDomain}Ids".ToValidCSharpPascalCase();

    /// <summary>
    /// Returns the name of the corresponding Core Interface if it exists, or null if it does not
    /// </summary>
    [JsonIgnore]
    public string? CoreInterfaceName
    {
        get
        {
            var name = $"I{Domain.ToValidCSharpPascalCase()}EntityCore";
            return CoreInterfaces.Contains(name) ? name : null;
        }
    }

    [JsonIgnore]
    public string AttributesClassName => $"{prefixedDomain}Attributes".ToValidCSharpPascalCase();

    [JsonIgnore]
    public string EntitiesForDomainClassName => $"{Domain}Entities".ToValidCSharpPascalCase();

    [JsonIgnore]
    public Type? AttributesBaseClass { get; set; }
};

record EntityMetaData(string id, string? friendlyName, string cSharpName);

[method:JsonConstructorAttribute]
partial record EntityAttributeMetaData(string JsonName, string CSharpName, string? ClrType, IReadOnlyList<string>? Values, bool IsList=false)
{
    [JsonIgnore]
    public string? PropertyType
    {
        get
        {
            var name = TypeName;
            if (IsList && !IsListType)
                return $"IReadOnlyList<{name}>";
            return name;

        }
    }
    [JsonIgnore]
    public IEnumerable<string>? CSharpValues => Values?.Select(s => s.ToValidCSharpCamelCase());

    [GeneratedRegex(@"(`\d)?\[(?<TypeName>[^\]]+)\]", RegexOptions.Compiled)]
    public static partial Regex ToClrGeneric();

    [JsonIgnore]
    public string? TypeName =>
        ClrType != null ? ToClrGeneric().Replace(ClrType,ev => $"<{ev.Groups["TypeName"].Value}>") : null;
    [MemberNotNullWhen(true,nameof(Values)),MemberNotNullWhen(true,nameof(ClrType)),MemberNotNullWhen(true,nameof(CSharpValues))]
    public bool ToGenerate => Values?.Count > 0 && ClrType!= null && !IsListType;
    public bool ToParse => ToGenerate || (Values?.Count > 0 && ClrType != null && IsListType);

    [JsonIgnore]
    private bool IsListType => ClrType?.Contains("IReadOnlyList", StringComparison.InvariantCultureIgnoreCase) == true;

    public EntityAttributeMetaData(string JsonName, string CSharpName, Type? ClrType) : this(JsonName, CSharpName, ClrType?.GetFriendlyName(), null){}
    public EntityAttributeMetaData(string JsonName, string CSharpName, CustomType? ClrType) : this(JsonName, CSharpName, ClrType?.TypeName,ClrType?.KnownValues?.Select(s => s.ToLowerInvariant())?.Order().Distinct().ToArray())
    {
    }
}
