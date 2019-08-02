using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealTools.CodeGen
{
    /// <summary>
    /// Code generator creating specialized implementation of the generic method.
    /// </summary>
    public sealed class SpecializeMethodCodeGen : ICodeGenerator
    {
        private List<INamedTypeSymbol> SpecializedTypes { get; set; }

        /// <summary>
        /// Initializes instance with types passed into the attribute.
        /// </summary>
        /// <param name="attributeData">Arguments specified at the generator attribute.</param>
        public SpecializeMethodCodeGen(AttributeData attributeData)
        {
            var data = attributeData.ConstructorArguments[0].Values;
            if (data.Length > 0)
            {
                SpecializedTypes = new List<INamedTypeSymbol>(data.Length);
                foreach (var it in data)
                {
                    SpecializedTypes.Add((INamedTypeSymbol)it.Value);
                }
            }
        }

        private List<INamedTypeSymbol[]> GetTypeCombinations(int typeArgCount)
        {
            IEnumerable<INamedTypeSymbol> RecurseTypeCombinations(int argCount)
            {
                if (argCount-- == 1)
                {
                    for (var i = 0; i < SpecializedTypes.Count; i++)
                    {
                        yield return SpecializedTypes[i];
                    }
                    yield break;
                }

                var child = RecurseTypeCombinations(argCount).Partition(argCount).ToArray();
                for (var i = 0; i < SpecializedTypes.Count; i++)
                {
                    foreach (var x in child)
                    {
                        yield return SpecializedTypes[i];
                        foreach (var y in x)
                            yield return y;
                    }

                }
            }

            return RecurseTypeCombinations(typeArgCount).Partition(typeArgCount).ToList();
        }

        /// <summary>
        /// Generates specialized methods for types.
        /// </summary>
        /// <param name="context">All the inputs necessary to perform the code generation.</param>
        /// <param name="progress">A way to report diagnostic messages.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated member syntax to be added to the project.</returns>
        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            if (SpecializedTypes == null)
            {
                SpecializedTypes = new Type[]
                {
                    typeof(bool),
                    typeof(sbyte),
                    typeof(byte),
                    typeof(short),
                    typeof(ushort),
                    typeof(int),
                    typeof(uint),
                    typeof(long),
                    typeof(ulong),
                    typeof(float),
                    typeof(double),
                    typeof(decimal),
                    typeof(Guid)
                }
                .Select(t => context.Compilation.GetTypeByMetadataName(t.FullName))
                .ToList();
            }

            var def = SyntaxFactory.List<MemberDeclarationSyntax>();
            if (context.ProcessingNode is MethodDeclarationSyntax method)
            {
                var typeArgCount = method.TypeParameterList.Parameters.Count;
                var items = GetTypeCombinations(typeArgCount);

                foreach (var it in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var token = method.GetFirstToken();
                    var trivia = SyntaxTriviaList.Empty;
                    if (token.HasStructuredTrivia)
                    {
                        trivia = token.LeadingTrivia;
                    }
                    var rewritten = method.Accept(new GenericMethodRewriter(it)) as MethodDeclarationSyntax;
                    def = def.Add(rewritten.WithLeadingTrivia(trivia));
                }
            }

            return Task.FromResult(def);
        }
    }
}
