using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace UnrealTools.CodeGen
{
    internal sealed class GenericMethodRewriter : CSharpSyntaxRewriter
    {
        const string AttributeName = "SpecializeMethod";
        private readonly List<string> _genericTypeTokens = new List<string>();
        private readonly List<string> _genericReplacementTokens = new List<string>();

        private bool ReplaceIdentifier(string from, out string to)
        {
            var index = _genericTypeTokens.IndexOf(from);
            if (index != -1 && _genericReplacementTokens.Count > index)
            {
                to = _genericReplacementTokens[index];
                return true;
            }
            else
            {
                to = string.Empty;
                return false;
            }
        }
        public INamedTypeSymbol ResolveType(Compilation compilation, Type type)
        {
            var typeSymbol = compilation.GetTypeByMetadataName(type.FullName);
            if (typeSymbol is null)
                throw new Exception($"Type {type.AssemblyQualifiedName} not resolved to INamedTypeSymbol.");

            return typeSymbol;
        }
        public GenericMethodRewriter(params INamedTypeSymbol[] symbols)
        {
            foreach (var symbol in symbols)
            {
                _genericReplacementTokens.Add(symbol.ToDisplayString());
            }
        }
        public GenericMethodRewriter(bool visitIntoStructuredTrivia = false) : base(visitIntoStructuredTrivia) { }

        public override SyntaxNode VisitAttributeList(AttributeListSyntax node)
        {
            var list = base.VisitAttributeList(node) as AttributeListSyntax;
            if (list.Attributes.Count == 0)
                return default;

            return list;
        }
        public override SyntaxNode VisitAttribute(AttributeSyntax node)
        {
            if (node.Name is IdentifierNameSyntax name && name.Identifier.ValueText == AttributeName)
                return default;
            return base.VisitAttribute(node);
        }
        public override SyntaxNode VisitTypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node) => DefaultVisit(node);
        public override SyntaxNode VisitTypeParameterList(TypeParameterListSyntax node)
        {
            // Process the type parameters, adding them to array.
            base.VisitTypeParameterList(node);
            // Remove node
            return DefaultVisit(node);
        }
        public override SyntaxNode VisitTypeParameter(TypeParameterSyntax node)
        {
            // Add type identifier to array
            _genericTypeTokens.Add(node.Identifier.ValueText);
            // Process it further, the parameterlist will be removed by VisitTypeParameterList
            return base.VisitTypeParameter(node);
        }
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            => ReplaceIdentifier(node.Identifier.ValueText, out var value)
                ? SyntaxFactory.ParseTypeName(value)
                : base.VisitIdentifierName(node);
    }
}
