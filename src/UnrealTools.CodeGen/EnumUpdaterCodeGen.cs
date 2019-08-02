﻿using CodeGeneration.Roslyn;
using HtmlAgilityPack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnrealTools.CodeGen
{
    /// <summary>
    /// Code generator parsing UE4 documentation webpage.
    /// </summary>
    public sealed class EnumUpdaterCodeGen : IRichCodeGenerator
    {
        string _url;
        const string Suffix = "_Stub";
        DiagnosticDescriptor suffixDescriptor = new DiagnosticDescriptor("UTCG001", "Invalid codegen suffix error.", "Enum {0} should be suffixed with '{1}'.", "UnrealTools.CodeGen", DiagnosticSeverity.Error, true);
        DiagnosticDescriptor notEmptyDescriptor = new DiagnosticDescriptor("UTCG002", "Codegen target is not empty declaration.", "Enum {0} should have no members, found {1}.", "UnrealTools.CodeGen", DiagnosticSeverity.Warning, true);
        DiagnosticDescriptor sourceFailureDescription = new DiagnosticDescriptor("UTCG003", "Codegen target is not enum, or encountered a problem.", "There was a problem with the {0} extraction from {1}. Expected enum, got: {2}", "UnrealTools.CodeGen", DiagnosticSeverity.Error, true);
        DiagnosticDescriptor webFailure = new DiagnosticDescriptor("UTCG004", "Codegen target is null or empty.", "{0} is null or empty.", "UnrealTools.CodeGen", DiagnosticSeverity.Error, true);

        /// <summary>
        /// Initializes code generator with attribute arguments.
        /// </summary>
        /// <param name="attributeData">Arguments specified at the generator attribute.</param>
        public EnumUpdaterCodeGen(AttributeData attributeData)
        {
            if(attributeData.ConstructorArguments[0].Value is string url)
                _url = url;
            foreach(var kv in attributeData.NamedArguments)
            {
            }
        }

        private XmlElementSyntax XmlSeeHrefElement(string url, string text)
        {
            var seeTag = XmlName("see");
            return XmlElement(
                XmlElementStartTag(
                    seeTag,
                    List<XmlAttributeSyntax>().Add(
                        XmlTextAttribute("href", url)
                    )
                ),
                List<XmlNodeSyntax>().Add(
                    XmlText(text)
                ),
                XmlElementEndTag(seeTag)
            );
        }
        private EnumMemberDeclarationSyntax AddEnumMemberDocs(EnumMemberDeclarationSyntax memberNode, string memberSummary)
        {
            var xmlPad = XmlText(" ");
            var xmlNewLine = XmlNewLine("\r\n");
            var xmlNewLineNoTrail = XmlText(XmlTextNewLine("\r\n", false));
            var docs = new XmlNodeSyntax[]
            {
                xmlPad,
                XmlSummaryElement(
                    xmlNewLine,
                    xmlPad,
                    XmlText(memberSummary),
                    xmlNewLine,
                    xmlPad
                    ),
                xmlNewLineNoTrail
            };
            return memberNode.WithLeadingTrivia(Trivia(DocumentationComment(docs)));
        }
        private EnumDeclarationSyntax AddEnumDocs(EnumDeclarationSyntax enumNode, string summaryText, Dictionary<string, string> memberDocs)
        {
            if (!summaryText.EndsWith('.'))
                summaryText += ".";
            var xmlPad = XmlText(" ");
            var xmlNewLine = XmlNewLine("\r\n");
            var xmlNewLineNoTrail = XmlText(XmlTextNewLine("\r\n", false));
            var seeTag = XmlName("see");
            var docs = new XmlNodeSyntax[]
            {
                xmlPad,
                XmlSummaryElement(
                    xmlNewLine,
                    xmlPad,
                    XmlText(summaryText),
                    xmlNewLine,
                    xmlPad
                    ),
                xmlNewLine,
                xmlPad,
                XmlRemarksElement(
                    XmlText($"Autogenerated from "),
                    XmlSeeHrefElement(_url, $"{enumNode.Identifier.ValueText} docs"),
                    XmlText($".")
                ),
                xmlNewLineNoTrail
            };
            XmlElementEndTag(seeTag);

            var nodes = enumNode.Members.Select(member => AddEnumMemberDocs(member, memberDocs[member.Identifier.ValueText]));
            enumNode = enumNode.WithMembers(SeparatedList(nodes));
            return enumNode.WithLeadingTrivia(Trivia(DocumentationComment(docs)));
        }

        /// <summary>
        /// Generate enum from the docs.
        /// </summary>
        /// <param name="context">All the inputs necessary to perform the code generation.</param>
        /// <param name="progress">A way to report diagnostic messages.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated member syntax to be added to the project.</returns>
        public async Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var def = List<MemberDeclarationSyntax>();
            if (context.ProcessingNode is EnumDeclarationSyntax enumDecl)
            {
                var location = enumDecl.Modifiers.Count > 0 ? enumDecl.Modifiers.First().GetLocation() : enumDecl.EnumKeyword.GetLocation();
                var enumName = enumDecl.Identifier.ValueText;
                if (!enumName.EndsWith(Suffix))
                    progress.Report(Diagnostic.Create(suffixDescriptor, location, enumName, Suffix));
                if (enumDecl.Members.Count > 0)
                    progress.Report(Diagnostic.Create(notEmptyDescriptor, location, enumName, enumDecl.Members.Count));

                var docsDocument = await new HtmlWeb().LoadFromWebAsync(_url, cancellationToken);
                var document = docsDocument.DocumentNode;
                if (document is null)
                    progress.Report(Diagnostic.Create(webFailure, location, nameof(document)));

                var summaryNode = document.SelectSingleNode("//div[@id='description']/p")?.InnerText;
                if (summaryNode is null)
                    progress.Report(Diagnostic.Create(webFailure, location, nameof(summaryNode)));
                var membersDocs = document.SelectNodes("//div[@id='values']//tr[@class='normal-row']");
                if (membersDocs is null || membersDocs.Count == 0)
                    progress.Report(Diagnostic.Create(webFailure, location, nameof(membersDocs)));
                var docs = membersDocs.Select(kv => 
                    new KeyValuePair<string, string>(
                        kv.SelectSingleNode("./td[@class='name-cell']/p").InnerText,
                        kv.SelectSingleNode("./td[@class='desc-cell']/p").InnerText
                    )).ToDictionary(kv => kv.Key, kv => kv.Value);
                var sourceCode = HtmlEntity.DeEntitize(document.SelectSingleNode("//div[@class='simplecode_api']/p").InnerText);

                if (ParseMemberDeclaration(sourceCode) is EnumDeclarationSyntax newEnum)
                {
                    newEnum = newEnum.WithBaseList(enumDecl.BaseList)
                        .WithModifiers(TokenList(
                            Token(SyntaxKind.PublicKeyword)
                            )
                        );
                    def = def.Add(AddEnumDocs(newEnum, summaryNode, docs));
                }
                else
                {
                    progress.Report(Diagnostic.Create(sourceFailureDescription, location, enumName, _url, sourceCode));
                }
            }

            return def;
        }

        /// <summary>
        /// Generates enum from the docs, putting it in the invocation namespace if it exists.
        /// </summary>
        /// <param name="context">All the inputs necessary to perform the code generation.</param>
        /// <param name="progress">A way to report diagnostic messages.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated syntax nodes to be added to the compilation unit added to the project.</returns>
        public async Task<RichGenerationResult> GenerateRichAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            SyntaxNode syntax = context.ProcessingNode;
            do
            {
                syntax = syntax.Parent;
            } while (!(syntax is NamespaceDeclarationSyntax));

            if (syntax is NamespaceDeclarationSyntax namespaceDecl)
            {
                return new RichGenerationResult
                {
                    Members = List<MemberDeclarationSyntax>()
                    .Add(NamespaceDeclaration(namespaceDecl.Name)
                        .WithMembers(
                            await GenerateAsync(context, progress, cancellationToken)
                        )
                    )
                };
            }
            else
            {
                return new RichGenerationResult
                {
                    Members = await GenerateAsync(context, progress, cancellationToken)
                };
            }
        }
    }
}