using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using NetDaemon.HassModel.CodeGenerator.Model;
using System.Xml.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NetDaemon.HassModel.CodeGenerator.CodeGeneration;

internal static class AttributeTypeGenerator
{
    /// <summary>
    /// Generates a record with all the attributes found in a set of entities.
    /// </summary>
    /// <example>
    /// public record LightAttributes : LightAttributesBase
    /// {
    ///     [JsonPropertyName("brightness")]
    ///     public double? Brightness { get; init; }
    ///
    ///     [JsonPropertyName("color_mode")]
    ///     public string? ColorMode { get; init; }
    ///
    ///     [JsonPropertyName("color_temp")]
    ///     public double? ColorTemp { get; init; }
    /// }
    /// </example>

    public static RecordDeclarationSyntax GenerateAttributeRecord(EntityDomainMetadata domain)
    {
        var propertyDeclarations = domain.Attributes
            .SelectMany(ToAttributeProperty);

        var record = Record(domain.AttributesClassName, propertyDeclarations)
            .ToPublic()
            .AddModifiers(Token(SyntaxKind.PartialKeyword));

        return domain.AttributesBaseClass != null ? record.WithBase(SimplifyTypeName(domain.AttributesBaseClass)) : record;
    }

    private static IEnumerable<MemberDeclarationSyntax> ToAttributeProperty(EntityAttributeMetaData a)
    {
        if (a.ToGenerate || a.ToParse)
        {
            
            var type = (a.PropertyType ?? typeof(object).GetFriendlyName());
            
            yield return FieldDeclaration(
                        VariableDeclaration(IdentifierName(type + "?"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(Identifier("_"+ a.CSharpName))
                                )
                            )
                        )
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));
            yield return 
                PropertyWithExpression($"{type}?", a.CSharpName,
                        SyntaxFactory.ParseExpression($"_{a.CSharpName} ??= {a.CSharpName}Json.HasValue ? JsonSerializer.Deserialize<{type}>({a.CSharpName}Json.Value,HassJsonContext.DefaultOptions) : null"))
                .AddAttributeLists(
                    AttributeList(
                        SeparatedList([
                            Attribute(
                                ParseName("System.Text.Json.Serialization.JsonIgnore"))
                        ])));
 //               .WithJsonPropertyName(a.JsonName);
            yield return AutoPropertyGetInit($"JsonElement?", a.CSharpName + "Json")
                .ToPublic()
                .WithJsonPropertyName(a.JsonName);
        }
        else
        {
            yield return AutoPropertyGetInit($"{(a.PropertyType ?? typeof(object).GetFriendlyName())}?", a.CSharpName)
                .ToPublic()
                .WithJsonPropertyName(a.JsonName);

        }

    }

    public static IEnumerable<MemberDeclarationSyntax> GenerateAttributeEnums(EntityDomainMetadata domain)
    {
        foreach (var a in domain.Attributes)
        {
            var ev = GenerateAttributeEnum(a);
            if (ev != null)
                yield return ev;
        }
    }

    public static MemberDeclarationSyntax? GenerateAttributeEnum(EntityAttributeMetaData domain)
    {
        if (domain.ToGenerate)
        {
            return EnumDeclaration(domain.ClrType)
                .ToPublic()
                .WithAttributeLists(
                    SingletonList(
                        AttributeList(
                            SeparatedList([
                                Attribute(
                                    ParseName("System.Text.Json.Serialization.JsonConverter"),
                                    AttributeArgumentList(
                                        SingletonSeparatedList(
                                            AttributeArgument(
                                                TypeOfExpression(
                                                    ParseName(nameof(JsonStringEnumConverter))
)
)
)
))
                            ]))))
                .WithMembers(
                    SyntaxFactory.SeparatedList(
                        domain.Values.Select(s => EnumMemberDeclaration(s.ToValidCSharpPascalCase()!)
                            .WithAttributeLists(
                                SingletonList(
                                    AttributeList(
                                        SeparatedList([
                                            Attribute(ParseName("System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute"),
                                                AttributeArgumentList(
                                                    SingletonSeparatedList(AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(s)))))),
                                            //Attribute(
                                            //    ParseName("System.Text.Json.Serialization.JsonConverter"),
                                            //    AttributeArgumentList(
                                            //        SingletonSeparatedList(
                                            //            AttributeArgument(
                                            //                TypeOfExpression(
                                            //                    ParseName($"SingleObjectAsArrayConverter<{domain.ClrType}>")
//)
//)
//)
//))
                                        ]))))
                        ).ToArray()
                        )
                    );

        }

        return null;
    }

}
