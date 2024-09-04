using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Naratteu.MemberCollector;

[Generator]
public class AnonymousClassGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource("AnonymousClassAttribute.g.cs", SourceText.From("""
                namespace Naratteu.AnonymousClass
                {
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
                    public class AnonymousClassAttribute : System.Attribute { }
                }
                """, Encoding.UTF8));
        });
        context.RegisterSourceOutput(
            context.SyntaxProvider.ForAttributeWithMetadataName("Naratteu.AnonymousClass.AnonymousClassAttribute",
                (_, _) => true, (c, _) => c),
            (spc, c) =>
            {
                if (c is { TargetSymbol: INamedTypeSymbol s })
                    spc.AddSource($"{s}.g.cs", SourceText.From($$"""
                            #nullable enable
                            namespace {{s.ContainingNamespace}}
                            {
                                partial class {{s.Name}}
                                {
                                    public class Inner : {{s.Name}}
                                    {
                            {{c.TargetNode.ChildNodes()
                                    .SelectNotNull(n => n as MethodDeclarationSyntax)
                                    .Where(n => c.SemanticModel.GetDeclaredSymbol(n) is { IsVirtual: true } && n.Modifiers.Any(m => m.Kind() is SyntaxKind.PublicKeyword))
                                    .Select(n => $$"""
                                        public {{n.Identifier}}D? _{{n.Identifier}} { private get; init; }
                                        public delegate {{n.ReturnType}} {{n.Identifier}}D{{n.TypeParameterList}}{{n.ParameterList}};
                                        public override {{n.ReturnType}} {{n.Identifier}}{{n.TypeParameterList}}{{n.ParameterList}} => (_{{n.Identifier}} ?? base.{{n.Identifier}})({{n.ParameterList.Parameters.Select(p => p.Identifier).Join() }});
                            """).Join("\n")}}
                                    }
                                }
                            }
                            """, Encoding.UTF8));
            });
    }
}

static class Exts
{
    public static string Join<T>(this IEnumerable<T> tt, string sep = ", ") => string.Join(sep, tt);
    public static string Concat<T>(this IEnumerable<T> tt) => string.Concat(tt);

    public static IEnumerable<TT> SelectNotNull<T, TT>(this IEnumerable<T> tt, Func<T, TT?> f)
    {
        foreach (var t in tt)
            if (f(t) is { } notnull)
                yield return notnull;
    }
}