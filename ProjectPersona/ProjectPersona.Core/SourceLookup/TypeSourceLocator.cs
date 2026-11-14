using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using AssemblyIdentity = ProjectPersona.Core.Building.AssemblyIdentity;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Naming;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Обходит синтаксическое дерево и регистрирует объявления типов с их FQN и исходным текстом.
/// </summary>
internal static class TypeSourceLocator
{
    /// <summary>
    /// Индексирует все объявления типов в единице компиляции.
    /// </summary>
    /// <param name="compilationUnit">Корень синтаксического дерева документа.</param>
    /// <param name="sourceText">Исходный текст документа.</param>
    /// <param name="assembly">Идентичность компиляции сборки, которой принадлежит документ.</param>
    /// <param name="semanticModel">Семантическая модель документа или <see langword="null"/>.</param>
    /// <param name="builder">Построитель индекса, принимающий найденные совпадения.</param>
    public static void Locate(
        CompilationUnitSyntax compilationUnit,
        SourceText sourceText,
        AssemblyIdentity assembly,
        SemanticModel? semanticModel,
        TypeSourceIndexBuilder builder)
    {
        ProcessMembers(
            compilationUnit.Members,
            sourceText,
            assembly,
            semanticModel,
            string.Empty,
            builder);
    }

    private static void ProcessMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        SourceText sourceText,
        AssemblyIdentity assembly,
        SemanticModel? semanticModel,
        string namespaceName,
        TypeSourceIndexBuilder builder)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    ProcessMembers(
                        namespaceDeclaration.Members,
                        sourceText,
                        assembly,
                        semanticModel,
                        CombineNamespace(namespaceName, namespaceDeclaration.Name.ToString()),
                        builder);
                    break;

                case TypeDeclarationSyntax typeDeclaration:
                    ProcessType(
                        typeDeclaration,
                        sourceText,
                        assembly,
                        semanticModel,
                        namespaceName,
                        typeDeclaration.Identifier.Text,
                        builder);
                    break;

                case DelegateDeclarationSyntax delegateDeclaration:
                    AddType(
                        delegateDeclaration,
                        sourceText,
                        assembly,
                        semanticModel,
                        namespaceName,
                        delegateDeclaration.Identifier.Text,
                        builder);
                    break;

                case EnumDeclarationSyntax enumDeclaration:
                    AddType(
                        enumDeclaration,
                        sourceText,
                        assembly,
                        semanticModel,
                        namespaceName,
                        enumDeclaration.Identifier.Text,
                        builder);
                    break;
            }
        }
    }

    private static void ProcessType(
        TypeDeclarationSyntax typeDeclaration,
        SourceText sourceText,
        AssemblyIdentity assembly,
        SemanticModel? semanticModel,
        string namespaceName,
        string typePath,
        TypeSourceIndexBuilder builder)
    {
        AddType(
            typeDeclaration,
            sourceText,
            assembly,
            semanticModel,
            namespaceName,
            typePath,
            builder);

        foreach (var member in typeDeclaration.Members)
        {
            switch (member)
            {
                case TypeDeclarationSyntax nestedType:
                    ProcessType(
                        nestedType,
                        sourceText,
                        assembly,
                        semanticModel,
                        namespaceName,
                        $"{typePath}.{nestedType.Identifier.Text}",
                        builder);
                    break;

                case DelegateDeclarationSyntax nestedDelegate:
                    AddType(
                        nestedDelegate,
                        sourceText,
                        assembly,
                        semanticModel,
                        namespaceName,
                        $"{typePath}.{nestedDelegate.Identifier.Text}",
                        builder);
                    break;

                case EnumDeclarationSyntax nestedEnum:
                    AddType(
                        nestedEnum,
                        sourceText,
                        assembly,
                        semanticModel,
                        namespaceName,
                        $"{typePath}.{nestedEnum.Identifier.Text}",
                        builder);
                    break;
            }
        }
    }

    private static void AddType(
        MemberDeclarationSyntax declaration,
        SourceText sourceText,
        AssemblyIdentity assembly,
        SemanticModel? semanticModel,
        string namespaceName,
        string typePath,
        TypeSourceIndexBuilder builder)
    {
        var type = TypeFqn.Build(namespaceName, typePath);
        var declarationSpan = declaration.FullSpan;
        var sourceCode = sourceText.ToString(declarationSpan);
        var references = TypeSourceReferenceCollector.Collect(declaration, semanticModel, type);
        var members = IndexMembers(declaration, declarationSpan, semanticModel, type);

        builder.Add(new TypeSourceIndexedMatch(
            new TypeSourceMatch(
                assembly.Name,
                type,
                assembly.TargetFramework,
                sourceCode,
                references,
                Members: null),
            members));
    }

    private static IReadOnlyList<TypeSourceMemberSlice> IndexMembers(
        MemberDeclarationSyntax declaration,
        TextSpan declarationSpan,
        SemanticModel? semanticModel,
        string declaringFqn)
    {
        var members = new List<TypeSourceMemberSlice>();

        foreach (var memberDeclaration in EnumerateMemberDeclarations(declaration))
        {
            var memberSpan = memberDeclaration.FullSpan;
            var start = memberSpan.Start - declarationSpan.Start;
            var length = memberSpan.Length;
            var memberReferences = TypeSourceReferenceCollector.Collect(memberDeclaration, semanticModel, declaringFqn);
            var signature = TypeSourceReferenceCollector.FormatDeclaringSignature(memberDeclaration, semanticModel);

            foreach (var name in TypeSourceMemberNameResolver.GetNames(memberDeclaration, semanticModel))
            {
                members.Add(new TypeSourceMemberSlice(name, start, length, memberReferences, signature));
            }
        }

        return members;
    }

    private static IEnumerable<MemberDeclarationSyntax> EnumerateMemberDeclarations(MemberDeclarationSyntax declaration)
    {
        switch (declaration)
        {
            case TypeDeclarationSyntax typeDeclaration:
                foreach (var member in typeDeclaration.Members)
                {
                    if (member is TypeDeclarationSyntax or DelegateDeclarationSyntax or EnumDeclarationSyntax)
                        continue;

                    yield return member;
                }

                break;

            case EnumDeclarationSyntax enumDeclaration:
                foreach (var member in enumDeclaration.Members)
                    yield return member;

                break;
        }
    }

    private static string CombineNamespace(string parentNamespace, string segment) =>
        string.IsNullOrEmpty(parentNamespace) ? segment : $"{parentNamespace}.{segment}";
}
