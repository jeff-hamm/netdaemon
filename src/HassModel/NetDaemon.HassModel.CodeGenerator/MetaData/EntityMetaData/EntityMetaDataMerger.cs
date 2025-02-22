using System.Reflection;
using System.Text.Json.Serialization;
using NetDaemon.HassModel.Entities.Core;

namespace NetDaemon.HassModel.CodeGenerator;
internal static class EntityMetaDataMerger
{
    // We have to disable warnings. SuppressMessage attribute did not work.
#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly Type[] _possibleBaseTypes = typeof(LightAttributesBase).Assembly.GetTypes();
#pragma warning restore CS0618 // Type or member is obsolete

    // We need to merge the previously saved metadata with the current metadata from HA
    // We do this because sometimes entities do not provide all their attributes,
    // like a Light only has a brightness attribute when it is turned on
    //
    // data structure:
    // [ {
    //   "domain": "weather",
    //   "isNumeric": false,
    //   "entities": [ {
    //       "id": "",
    //       "friendlyName":""
    //   }]
    //   "attributes" : [ {
    //       "jsonName": "temperature",
    //       "cSharpName": "Temperature",
    //       "clrType": "double"
    //     }]
    // } ]
    //
    // How to merge:
    // Match previous and current EntityDomainMetadata on domain and isNumeric
    //    use all non-matches from both
    //    merge matching items
    //       Only keep Entities in current set
    //       Match Attributes on JsonName
    //           use all non-matches from both
    //           merge matching items
    //               Previous attributes keep their Existing CSharpName (assume they are unique)
    //               If types do not match use object
    //       check for duplicate CSharpNames, keep CSharpName fixed for previous

    public static EntitiesMetaData Merge(CodeGenerationSettings codeGenerationSettings, EntitiesMetaData previous, EntitiesMetaData current)
    {                                                                           
        if (codeGenerationSettings.UseAttributeBaseClasses)
        {
            WriteWarningMessageToConsole("Usage of attribute classes is deprecated and will be removed in future release. We now include default metadata that gives same behaviour.");
            previous = SetBaseTypes(previous);
            current = SetBaseTypes(current);
        }

        return previous with
        {
            Domains = FullOuterJoin(previous.Domains, current.Domains, p => (p.Domain.ToLowerInvariant(), p.IsNumeric), MergeDomains)
                .Select(HandleDuplicateCSharpNames)
                .ToList()
        };
    }
    public static EntitiesMetaData Overwrite(CodeGenerationSettings codeGenerationSettings, EntitiesMetaData current, EntitiesMetaData overwrite)
    {   
        return current with
        {
            Domains = FullOuterJoin(overwrite.Domains,current.Domains, p => (p.Domain.ToLowerInvariant(), p.IsNumeric), OverwriteDomains)
                .Select(HandleDuplicateCSharpNames)
                .ToList()
        };
    }

    private static void WriteWarningMessageToConsole(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static EntitiesMetaData SetBaseTypes(EntitiesMetaData entitiesMetaData)
    {
        return entitiesMetaData with
        {
            Domains = entitiesMetaData.Domains.Select(m => DeriveFromBasetype(m, _possibleBaseTypes)).ToList()
        };
    }

    private static EntityDomainMetadata DeriveFromBasetype(EntityDomainMetadata domainMetadata, IReadOnlyCollection<Type> possibleBaseTypes)
    {
        var baseType = possibleBaseTypes.FirstOrDefault(t => t.Name == domainMetadata.AttributesClassName + "Base");

        var basePropertyJsonNames = baseType?.GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name)
            .ToHashSet() ?? [];

        return domainMetadata with
        {
            AttributesBaseClass = baseType,
            Attributes = domainMetadata.Attributes.Where(a => !basePropertyJsonNames.Contains(a.JsonName)).ToList(),
        };
    }
    private static EntityDomainMetadata OverwriteDomains(EntityDomainMetadata overwrites, EntityDomainMetadata current)
    {
        // Only keep entities from Current but merge the attributes
        return current with
        {
            Attributes = FullOuterJoin(overwrites.Attributes, current.Attributes, a => a.JsonName, OverwriteAttributes).ToList()
        };
    }

    private static EntityDomainMetadata MergeDomains(EntityDomainMetadata previous, EntityDomainMetadata current)
    {
        // Only keep entities from Current but merge the attributes
        return current with
        {
            Attributes = FullOuterJoin(previous.Attributes, current.Attributes, a => a.JsonName, MergeAttributes).ToList()
        };
    }
    
    private static EntityAttributeMetaData OverwriteAttributes(EntityAttributeMetaData overwrites, EntityAttributeMetaData current)
    {
        // for Attributes matching by the JsonName keep the previous
        // this makes sure the preferred CSharpName stays the same, we only merge the types
        return current with
        {
            ClrType = MergeTypes(current?.TypeName ,
                overwrites?.TypeName),
//            _clr = MergeTypes(previous.ClrType, current.ClrType)
        };
    }
    private static EntityAttributeMetaData MergeAttributes(EntityAttributeMetaData previous, EntityAttributeMetaData current)
    {
        // for Attributes matching by the JsonName keep the previous
        // this makes sure the preferred CSharpName stays the same, we only merge the types
        return current with
        {
            ClrType = MergeTypes(previous?.TypeName ,
                current?.TypeName),
            Values = MergeValues(previous, current)
//            _clr = MergeTypes(previous.ClrType, current.ClrType)
        };
    }

    private static IReadOnlyList<string>? MergeValues(EntityAttributeMetaData? previous, EntityAttributeMetaData? current)
    {
        if (current?.Values?.Count ==null)
            return previous?.Values;
        if (previous?.Values?.Count == null)
            return current.Values;
        return previous.Values.Select(v => v.ToLowerInvariant()).Union(current.Values?.Select(s => s.ToLowerInvariant()) ?? []).Order().ToArray();
    }

    private static Type? MergeTypes(Type? previous, Type? current)
    {
        // null for previous or current type means we did not get any non-null values to determine a type from
        // so if previous or current is null we use the other.
        // if for some reason the type has changed we use object to support both.
        return
            previous == current ? previous :
            previous is null ? current :
            current is null ? previous :
            typeof(object);
    }
    
    private static string? MergeTypes(String? previous, String? current)
    {
        // null for previous or current type means we did not get any non-null values to determine a type from
        // so if previous or current is null we use the other.
        // if for some reason the type has changed we use object to support both.
        return
            previous == current ? previous :
            previous is null ? current :
            current is null ? previous :
            current ?? typeof(object).GetFriendlyName();
    }


    private static EntityDomainMetadata HandleDuplicateCSharpNames(EntityDomainMetadata entitiesMetaData)
    {
        // This hashset will initially have all Member names in the base class.
        // We will then also add all new names to this set so we are sure they will all be unique
        var reservedCSharpNames = entitiesMetaData.AttributesBaseClass?
            .GetMembers().Select(p => p.Name).ToHashSet() ?? [];

        var withDeDuplicatedCSharpNames = entitiesMetaData.Attributes
            .GroupBy(t => t.CSharpName)
            .SelectMany(s => DeDuplicateCSharpNames(s.Key, s, reservedCSharpNames)).ToList();

        return entitiesMetaData with
        {
            Attributes = withDeDuplicatedCSharpNames
        };
    }

    private static IEnumerable<EntityAttributeMetaData> DeDuplicateCSharpNames(
        string preferredCSharpName, IEnumerable<EntityAttributeMetaData> items,
        ISet<string> reservedCSharpNames)
    {
        var list = items.ToList();
        if (list.Count == 1 && reservedCSharpNames.Add(preferredCSharpName))
        {
            // Just one Attribute with this preferredCSharpName AND it was not taken yet
            return new[] { list[0]};
        }

        // We have duplicates so we apply a suffix to all
        var suffix = 0;
        return list.Select(p => p with { CSharpName = ReserveNextAvailableName() }).ToList();

        string ReserveNextAvailableName()
        {
            string tryName;
            do
            {
                tryName = $"{preferredCSharpName}_{suffix++}";
            } while (!reservedCSharpNames.Add(tryName));

            return tryName;
        }
    }

    /// <summary>
    /// Full outer join two sets based on a key and merge the matches
    /// </summary>
    /// <returns>
    /// All items from previous and current that dont match
    /// A merged item for all matches based in the Merger delegate
    /// </returns>
    private static IEnumerable<T> FullOuterJoin<T, TKey>(
        IEnumerable<T> previous,
        IEnumerable<T> current,
        Func<T, TKey> keySelector,
        Func<T,T,T> merger) where TKey : notnull
    {
        var previousLookup = previous
            .Select(kvp => new { key=keySelector(kvp),kvp})
            .GroupBy(kvp => kvp.key)
            .ToDictionary(k => k.Key, k => k.First().kvp);
        var currentLookup = current.ToLookup(keySelector);

        var inPrevious = previousLookup
            .Select(p => (previous: p.Value, current: currentLookup[p.Key].FirstOrDefault()))
            .Select(t => t.current == null
                ? t.previous // Item in previous doe snot exist in current, return previous
                : merger(t.previous, t.current)); // match, so call merge delegate

        var onlyInCurrent = currentLookup.Where(l => !previousLookup.ContainsKey(l.Key)).SelectMany(l => l);
        return inPrevious.Concat(onlyInCurrent);
    }
}
