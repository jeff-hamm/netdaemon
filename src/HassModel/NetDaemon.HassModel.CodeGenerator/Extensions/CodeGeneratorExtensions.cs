﻿namespace NetDaemon.HassModel.CodeGenerator.Extensions
{
    internal static class CodeGeneratorExtensions
    {
        public static string GetClassName(this CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Any())
                return compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>().First().Identifier.ToString();

            if (compilationUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().Any())
                return compilationUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().First().Identifier.ToString();

            if (compilationUnit.DescendantNodes().OfType<RecordDeclarationSyntax>().Any())
                return compilationUnit.DescendantNodes().OfType<RecordDeclarationSyntax>().First().Identifier.ToString();
            if (compilationUnit.DescendantNodes().OfType<EnumDeclarationSyntax>().Any())
                return compilationUnit.DescendantNodes().OfType<EnumDeclarationSyntax>().First().Identifier.ToString();

            return string.Empty;
        }

    }
}
