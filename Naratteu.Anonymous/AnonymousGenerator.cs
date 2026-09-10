using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Naratteu.Anonymous;

/// <summary>
/// <c>IFoo.New(new() { .. })</c> / <c>Anon.New&lt;IFoo&gt;(new() { .. })</c> 호출지점을 보고
/// 그 인터페이스를 대리자 프로퍼티로 구현한 클래스와 진입점을 만들어낸다.
/// </summary>
[Generator]
public class AnonymousGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("Naratteu.Anonymous.Runtime.g.cs", SourceText.From(RuntimeSource.Text, Encoding.UTF8)));

        var names = context.AnalyzerConfigOptionsProvider.Select((p, _) => new Names(
            Option(p, "AnonymousEntryName", "New"),
            Option(p, "AnonymousHolderName", "Anon")));

        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, Transform)
            .Where(c => c is not null)
            .Select((c, _) => c!);

        context.RegisterSourceOutput(candidates.Collect().Combine(names), (spc, t) => Emitter.Emit(spc, t.Left, t.Right));
    }

    static string Option(AnalyzerConfigOptionsProvider p, string key, string fallback) =>
        p.GlobalOptions.TryGetValue($"build_property.{key}", out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

    /// <summary>인자가 <c>new() { .. }</c> 하나뿐인 호출만 후보로 본다. 이름은 설정으로 바뀔 수 있어 여기선 안 본다.</summary>
    static bool IsCandidate(SyntaxNode node, CancellationToken _) =>
        node is InvocationExpressionSyntax inv
        && inv.ArgumentList.Arguments.Count is 1
        && inv.ArgumentList.Arguments[0].Expression is ImplicitObjectCreationExpressionSyntax;

    static Candidate? Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var inv = (InvocationExpressionSyntax)ctx.Node;

        // 이미 해석되는 호출이면 남의 메서드다
        if (ctx.SemanticModel.GetSymbolInfo(inv, ct).Symbol is not null) return null;

        SimpleNameSyntax? name = inv.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name,
            SimpleNameSyntax sn => sn,
            _ => null,
        };
        if (name is null) return null;

        var generic = name is GenericNameSyntax;
        ExpressionSyntax? typeSyntax = name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } g ? g.TypeArgumentList.Arguments[0]
            : !generic && inv.Expression is MemberAccessExpressionSyntax m ? m.Expression : null;
        if (typeSyntax is null) return null;

        // 수신자가 '타입'이어야 한다. 인스턴스면 우리 문법이 아니다.
        if (ctx.SemanticModel.GetSymbolInfo(typeSyntax, ct).Symbol is not INamedTypeSymbol type) return null;

        var at = LocationInfo.From(name.GetLocation());
        List<DiagInfo> diags = [];

        if (type.TypeKind is not TypeKind.Interface)
        {
            diags.Add(new(Diags.NotAnInterface.Id, type.Fq(), at));
            return new(name.Identifier.Text, generic, type.Fq(), "", null, at, diags.ToEquatable());
        }

        var target = TargetBuilder.Build(type, at, diags);
        return new(name.Identifier.Text, generic, type.Fq(), TargetBuilder.TypeArgs(type), target, at, diags.ToEquatable());
    }
}
