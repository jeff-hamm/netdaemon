using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.CSharp;
using NetDaemon.HassModel.CodeGenerator.CodeGeneration;
using NetDaemon.HassModel.Entities;

namespace NetDaemon.HassModel.CodeGenerator;

internal static class EntitiesGenerator
{
    public static IEnumerable<MemberDeclarationSyntax> Generate(IReadOnlyCollection<EntityDomainMetadata> metaData)
    {
        var entityDomains = metaData.Select(d => d.Domain).Distinct();

        yield return GenerateRootEntitiesInterface(entityDomains);

        yield return GenerateRootEntitiesClass(metaData);
        yield return InterfaceDeclaration("IDomain").ToPublic();
        yield return GenerateEntityDomainBaseInterface();
        foreach (var domainMetadata in metaData.GroupBy(m => m.EntitiesForDomainClassName))
        {
            IList<EntityDomainMetadata> e = [.. domainMetadata];
            var idsClass = GenerateEntitiesForDomainIdsClass(e[0].EntityIdsClassName, e);

            yield return idsClass;
            yield return GenerateEntitiesForDomainClass(domainMetadata.Key, idsClass.Identifier.Text, e);
        }

        foreach (var domainMetadata in metaData)
        {
            yield return GenerateEntityType(domainMetadata);
            yield return AttributeTypeGenerator.GenerateAttributeRecord(domainMetadata);
            foreach(var ev in AttributeTypeGenerator.GenerateAttributeEnums(domainMetadata))
            {
                yield return ev;
            }
        }
    }
    private static TypeDeclarationSyntax GenerateRootEntitiesInterface(IEnumerable<string> domains)
    {
        var autoProperties = domains.Select(domain =>
        {
            var typeName = GetEntitiesForDomainClassName(domain);
            var propertyName = domain.ToPascalCase();

            return (MemberDeclarationSyntax)AutoPropertyGet(typeName, propertyName);
        });

        return InterfaceDeclaration("IEntities").WithMembers(List(autoProperties)).ToPublic();
    }

    // The Entities class that provides properties to all Domains
    private static TypeDeclarationSyntax GenerateRootEntitiesClass(IEnumerable<EntityDomainMetadata> domains)
    {
        var properties = domains.DistinctBy(s => s.Domain).Select(set =>
        {
            var entitiesTypeName = GetEntitiesForDomainClassName(set.Domain);
            var entitiesPropertyName = set.Domain.ToPascalCase();

            return PropertyWithExpressionBodyNew(entitiesTypeName, entitiesPropertyName, "_haContext");
        }).ToArray();

        return ClassWithInjectedHaContext(EntitiesClassName)
            .WithBase("IEntities")
            .AddMembers(properties);
    }

    private static TypeDeclarationSyntax GenerateEntityDomainBaseInterface()
    {
        return InterfaceDeclaration("IEntityDomain")
            .WithTypeParameterList(TypeParameterList(SeparatedList([SyntaxFactory.TypeParameter("TEntity")])))
            .WithConstraintClauses(new SyntaxList<TypeParameterConstraintClauseSyntax>(
                TypeParameterConstraintClause(IdentifierName("TEntity"),
                    SeparatedList<TypeParameterConstraintSyntax>().Add(
                        TypeConstraint(
                            IdentifierName("Entity")
                        )
                        
                    ))))
            .WithBase("IDomain")
             .AddMembers(
                
                 MethodDeclaration(IdentifierName("TEntity"), "Entity")
                     .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                     .WithParameterList(
                         ParameterList(
                             SeparatedList<ParameterSyntax>().Add(
                                 SyntaxFactory.Parameter(Identifier("entityId")).WithType(IdentifierName("string")))
                         )
                         )
                     .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                 )
            ;
    }
    /// <summary>
    /// Generates the class with all the properties for the Entities of one domain
    /// </summary>
    private static TypeDeclarationSyntax GenerateEntitiesForDomainClass(string className,string idsClassName, IList<EntityDomainMetadata> entitySets)
    {
        var entityClassName = entitySets[0].EntityClassName;
        var entityClass = ClassWithInjectedHaContext(className)
            .WithBase($"IEntityDomain<{entityClassName}>");

        entityClass = entityClass.AddMembers(EnumerateAllGenerator.GenerateEnumerateMethods(entitySets[0].Domain, entityClassName));
        var entityProperty = entitySets.SelectMany(s=>s.Entities.Select(e => GenerateEntityProperty(e, s.EntityClassName))).ToArray();
        return entityClass
                .AddMembers(GenerateGetDomainEntityMethod(entityClassName))
                .AddMembers(entityProperty)
//            .AddMembers(idsClass)
                .AddMembers(PropertyWithExpressionBodyNew(idsClassName, "Ids")
            ///.AddModifiers(Token(SyntaxKind.StaticKeyword)));
            )
//            .AddMembers(idsClass);
            ;
        
    }
    private static TypeDeclarationSyntax GenerateEntitiesForDomainIdsClass(string className, IList<EntityDomainMetadata> entitySets)
    {
        var entityClass = ClassDeclaration(className).ToPublic();

///        entityClass = entityClass.AddMembers(EnumerateAllGenerator.GenerateEnumerateMethods(entitySets[0].Domain, entityClassName));

        var entityIdProperties = entitySets.SelectMany(s => s.Entities.Select(e => GenerateEntityIdMembers(e))).ToArray();

        return entityClass
            .AddMembers(entityIdProperties);
    }


    private static MethodDeclarationSyntax GenerateGetDomainEntityMethod(string entityClassName)
    {
        return MethodDeclaration(IdentifierName(entityClassName), "Entity")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                ParameterList(
                SeparatedList<ParameterSyntax>().Add(
                    SyntaxFactory.Parameter(Identifier("entityId"))
                        .WithType(IdentifierName("string")))
                )
            )
            .WithBody(
                Block(
                    ReturnStatement(
                        CastExpression(IdentifierName(entityClassName),
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(
                                    NamingHelper.GetVariableName<IHaContext>("_")),
                                SyntaxFactory.IdentifierName("Entity")

                                ), ArgumentList(SeparatedList<ArgumentSyntax>().Add(Argument(
                                IdentifierName("entityId")
                                ))))

                        ))));

    }
    private static MemberDeclarationSyntax GenerateEntityIdMembers(EntityMetaData entity, string suffix="")
    {
        return AutoPropertyGet("string", entity.cSharpName+suffix, entity.id).ToPublic();
    }

    private static MemberDeclarationSyntax GenerateEntityProperty(EntityMetaData entity, string className)
    {
        return PropertyWithExpressionBodyNew(className, entity.cSharpName, "_haContext", $"\"{entity.id}\"")
            .WithSummaryComment(entity.friendlyName);
    }

    /// <summary>
    /// Generates a record derived from Entity like ClimateEntity or SensorEntity for a specific set of entities
    /// </summary>
    private static MemberDeclarationSyntax GenerateEntityType(EntityDomainMetadata domainMetaData)
    {
        var attributesGeneric = domainMetaData.AttributesClassName;

        var baseType = domainMetaData.IsNumeric ? typeof(NumericEntity) : typeof(Entity);
        var entityStateType = domainMetaData.IsNumeric ? typeof(NumericEntityState) : typeof(EntityState);
        var baseClass = $"{SimplifyTypeName(baseType)}<{domainMetaData.EntityClassName}, {SimplifyTypeName(entityStateType)}<{attributesGeneric}>, {attributesGeneric}>";

        var coreinterface = domainMetaData.CoreInterfaceName;
        if (coreinterface != null)
        {
            baseClass += $", {coreinterface}";
        }

        var (className, variableName) = GetNames<IHaContext>();
        var classDeclaration = $$"""
            record {{domainMetaData.EntityClassName}} : {{baseClass}}
            {
                public {{domainMetaData.EntityClassName}}({{className}} {{variableName}}, string entityId) : base({{variableName}}, entityId)
                {}

                public {{domainMetaData.EntityClassName}}({{SimplifyTypeName(typeof(IEntityCore))}} entity) : base(entity)
                {}
            }
            """;

        return ParseMemberDeclaration(classDeclaration)!
            .ToPublic()
            .AddModifiers(Token(SyntaxKind.PartialKeyword));
    }
}
