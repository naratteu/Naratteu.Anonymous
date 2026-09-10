using Microsoft.CodeAnalysis;

namespace Naratteu.Anonymous;

/// <summary>인터페이스 심볼 하나를 익명구현 클래스 모델로 옮긴다.</summary>
static class TargetBuilder
{
    const string Rt = "global::Naratteu.Anonymous";

    public static AnonTarget? Build(INamedTypeSymbol constructed, LocationInfo? at, List<DiagInfo> diags)
    {
        var def = constructed.OriginalDefinition;
        var faces = new List<INamedTypeSymbol>(def.AllInterfaces.Length + 1) { def };
        faces.AddRange(def.AllInterfaces);

        var raw = faces.SelectMany(f => f.GetMembers().Where(IsInterfaceMember).Select(m => (face: f, sym: m))).ToList();

        if (raw.Any(r => IsFatal(r.sym)))
        {
            diags.Add(new(Diags.CannotImplement.Id, constructed.Fq(), at));
            return null;
        }

        var used = new HashSet<string>();
        var names = AssignNames(raw, used);
        var members = raw.SelectMany((r, i) => Members(r.face, r.sym, names[i], used, diags, at)).ToList();

        return new(
            DefKey: def.Fq(),
            ImplName: ImplName(def),
            TypeParams: def.TypeParameters.Length is 0 ? "" : $"<{def.TypeParameters.Select(t => t.Name).Join()}>",
            Constraints: Constraints(def),
            Bases: def.AllInterfaces.Select(b => new BaseRef(b.OriginalDefinition.Fq(), TypeArgs(b))).ToEquatable(),
            Members: members.ToEquatable());
    }

    public static string ImplName(INamedTypeSymbol def) =>
        $"__Anon_{def.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None)).Substring("global::".Length).Sanitize()}"
        + (def.Arity is 0 ? "" : $"_{def.Arity}");

    public static string TypeArgs(INamedTypeSymbol t) => t.TypeArguments.Length is 0 ? "" : $"<{t.TypeArguments.Select(a => a.Fq()).Join()}>";

    static bool IsInterfaceMember(ISymbol m) => m.IsAbstract && m is IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion } or IPropertySymbol or IEventSymbol;

    /// <summary>인스턴스 대리자로 옮길 수 없는 멤버. (static abstract, 연산자, 비공개 멤버)</summary>
    static bool IsFatal(ISymbol m) => m.IsStatic
        || m.DeclaredAccessibility is not Accessibility.Public
        || m is IMethodSymbol { MethodKind: not MethodKind.Ordinary };

    // ===== 이름 배정 =====

    static List<string> AssignNames(List<(INamedTypeSymbol face, ISymbol sym)> raw, HashSet<string> used)
    {
        var preferred = raw.Select(r => r.sym is IPropertySymbol { IsIndexer: true } p ? p.MetadataName : r.sym.Name).ToList();
        var dup = new HashSet<string>(preferred.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key));

        var result = new List<string>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var name = preferred[i];
            if (dup.Contains(name))
                name += "_" + (raw[i].sym is IMethodSymbol { Parameters.Length: > 0 } m
                    ? m.Parameters.Select(p => p.Type.ToDisplayString(Formats.Short).Sanitize()).Join("_")   // 오버로드는 파라미터로
                    : raw[i].face.ToDisplayString(Formats.Short).Sanitize());                                // 그 외는 선언한 인터페이스로
            result.Add(Unique(name, used));
        }
        return result;
    }

    static string Unique(string name, HashSet<string> used)
    {
        var n = name.Esc();
        for (var i = 2; !used.Add(n); i++) n = $"{name}_{i}".Esc();
        return n;
    }

    // ===== 멤버 =====

    static IEnumerable<AnonMember> Members(INamedTypeSymbol face, ISymbol sym, string name, HashSet<string> used, List<DiagInfo> diags, LocationInfo? at)
    {
        var key = face.OriginalDefinition.Fq();
        var iface = face.Fq();

        switch (sym)
        {
            case IMethodSymbol { IsGenericMethod: true } g:
                diags.Add(new(Diags.UnsupportedMember.Id, $"{face.ToDisplayString(Formats.Short)}.{g.Name}", at));
                yield return new(key, name, null, null,
                    $"{Ret(g)} {iface}.{g.Name}<{g.TypeParameters.Select(t => t.Name).Join()}>({Parms(g.Parameters)}) => throw new global::System.NotSupportedException(\"제네릭 메서드는 익명구현이 지원하지 않습니다\");",
                    false, null);
                break;

            case IMethodSymbol m:
            {
                var (type, decl) = Del(name, m.ReturnsVoid ? "void" : m.ReturnType.Fq(), m.ReturnsByRef, m.ReturnsByRefReadonly, Prms(m.Parameters));
                yield return new(key, name, type, decl,
                    $"{Ret(m)} {iface}.{m.Name}({Parms(m.Parameters)}) => {RefOut(m)}{name}({Args(m.Parameters)});",
                    true, null);
                break;
            }

            case IPropertySymbol { IsIndexer: true } x:
            {
                var idx = x.Parameters;
                if (x.GetMethod is { } _ || x.ReturnsByRef)
                {
                    var g = Unique($"{name}_get", used);
                    var (type, decl) = Del(g, x.Type.Fq(), x.ReturnsByRef, x.ReturnsByRefReadonly, Prms(idx));
                    var body = x.SetMethod is null || x.ReturnsByRef
                        ? $"=> {(x.ReturnsByRef ? "ref " : "")}{g}({Args(idx)});"
                        : null;
                    if (body is not null)
                        yield return new(key, g, type, decl, $"{RetRef(x)}{x.Type.Fq()} {iface}.this[{Parms(idx)}] {body}", true, null);
                    else
                    {
                        var s = Unique($"{name}_set", used);
                        var (stype, sdecl) = Del(s, "void", false, false, [.. Prms(idx), new(x.Type.Fq(), "value", RefKind.None)]);
                        yield return new(key, g, type, decl,
                            $"{x.Type.Fq()} {iface}.this[{Parms(idx)}] {{ get => {g}({Args(idx)}); {Setter(x)} => {s}({Args(idx)}, value); }}", true, null);
                        yield return new(key, s, stype, sdecl, "", true, null);
                    }
                }
                else
                {
                    var s = Unique($"{name}_set", used);
                    var (stype, sdecl) = Del(s, "void", false, false, [.. Prms(idx), new(x.Type.Fq(), "value", RefKind.None)]);
                    yield return new(key, s, stype, sdecl,
                        $"{x.Type.Fq()} {iface}.this[{Parms(idx)}] {{ {Setter(x)} => {s}({Args(idx)}, value); }}", true, null);
                }
                break;
            }

            case IPropertySymbol p when p.ReturnsByRef:
            {
                var (type, decl) = Del(name, p.Type.Fq(), true, p.ReturnsByRefReadonly, []);
                yield return new(key, name, type, decl, $"{RetRef(p)}{p.Type.Fq()} {iface}.{p.Name} => ref {name}();", true, null);
                break;
            }

            case IPropertySymbol { GetMethod: not null, SetMethod: not null } p:
                yield return new(key, name, $"{Rt}.Prop<{p.Type.Fq()}>", null,
                    $"{p.Type.Fq()} {iface}.{p.Name} {{ get => {name}.Value; {Setter(p)} => {name}.Value = value; }}", true, null);
                break;

            case IPropertySymbol { GetMethod: not null } p:
                yield return new(key, name, $"{Rt}.Get<{p.Type.Fq()}>", null,
                    $"{p.Type.Fq()} {iface}.{p.Name} => {name}.Value;", true, null);
                break;

            case IPropertySymbol p:
                yield return new(key, name, $"{Rt}.Set<{p.Type.Fq()}>", null,
                    $"{p.Type.Fq()} {iface}.{p.Name} {{ {Setter(p)} => {name}.Value = value; }}", true, null);
                break;

            case IEventSymbol e:
                yield return new(key, name, $"{Rt}.Ev<{e.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).Fq()}>", null,
                    $"event {e.Type.Fq()} {iface}.{e.Name} {{ add => {name}.Add(value); remove => {name}.Remove(value); }}", false, "new()");
                break;
        }
    }

    static string Setter(IPropertySymbol p) => p.SetMethod is { IsInitOnly: true } ? "init" : "set";
    static string Ret(IMethodSymbol m) => m.ReturnsVoid ? "void" : $"{RetRef(m)}{m.ReturnType.Fq()}";
    static string RetRef(ISymbol s) => s switch
    {
        IMethodSymbol { ReturnsByRefReadonly: true } or IPropertySymbol { ReturnsByRefReadonly: true } => "ref readonly ",
        IMethodSymbol { ReturnsByRef: true } or IPropertySymbol { ReturnsByRef: true } => "ref ",
        _ => "",
    };
    static string RefOut(IMethodSymbol m) => m.ReturnsByRef ? "ref " : "";

    // ===== 대리자 =====

    static List<Prm> Prms(IEnumerable<IParameterSymbol> ps) => [.. ps.Select(p => new Prm(p.Type.Fq(), p.Name, p.RefKind))];

    static string Parms(IEnumerable<IParameterSymbol> ps) => ps.Select(p => $"{Mod(p.RefKind)}{p.Type.Fq()} {p.Name.Esc()}").Join();
    static string Args(IEnumerable<IParameterSymbol> ps) => ps.Select(p => $"{ArgMod(p.RefKind)}{p.Name.Esc()}").Join();

    static string Mod(RefKind r) => r switch
    {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In => "in ",
        RefKind.RefReadOnlyParameter => "ref readonly ",
        _ => "",
    };

    static string ArgMod(RefKind r) => r switch
    {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In or RefKind.RefReadOnlyParameter => "in ",
        _ => "",
    };

    /// <summary>가능하면 Func/Action, 안되면 전용 대리자를 만든다.</summary>
    static (string TypeRef, string? Decl) Del(string owner, string ret, bool byRef, bool byRefReadonly, List<Prm> ps)
    {
        if (!byRef && ps.Count <= 16 && ps.All(p => p.Ref is RefKind.None))
            return (ret is "void"
                ? ps.Count is 0 ? "global::System.Action" : $"global::System.Action<{ps.Select(p => p.Type).Join()}>"
                : $"global::System.Func<{ps.Select(p => p.Type).Concat([ret]).Join()}>", null);

        var name = $"{owner}_D";
        var refret = byRefReadonly ? "ref readonly " : byRef ? "ref " : "";
        return (name, $"public delegate {refret}{ret} {name}({ps.Select(p => $"{Mod(p.Ref)}{p.Type} {p.Name.Esc()}").Join()});");
    }

    // ===== 제약절 =====

    static string Constraints(INamedTypeSymbol def) => def.TypeParameters.Select(Clause).Where(c => c is not null).Join(" ");

    static string? Clause(ITypeParameterSymbol t)
    {
        List<string> parts = [];
        if (t.HasReferenceTypeConstraint) parts.Add(t.ReferenceTypeConstraintNullableAnnotation is NullableAnnotation.Annotated ? "class?" : "class");
        if (t.HasValueTypeConstraint && !t.HasUnmanagedTypeConstraint) parts.Add("struct");
        if (t.HasUnmanagedTypeConstraint) parts.Add("unmanaged");
        if (t.HasNotNullConstraint) parts.Add("notnull");
        parts.AddRange(t.ConstraintTypes.Select(c => c.Fq()));
        if (t.HasConstructorConstraint) parts.Add("new()");
        return parts.Count is 0 ? null : $"where {t.Name} : {parts.Join()}";
    }
}

readonly record struct Prm(string Type, string Name, RefKind Ref);
