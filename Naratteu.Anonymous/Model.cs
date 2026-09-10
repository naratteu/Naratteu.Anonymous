using Microsoft.CodeAnalysis;

namespace Naratteu.Anonymous;

/// <summary>익명구현 클래스가 노출할 멤버 하나. (인터페이스 멤버 하나당 1~2개)</summary>
sealed record AnonMember(
    string DeclKey,          // 이 멤버를 선언한 인터페이스의 원형정의 키
    string Name,             // 초기화자에서 쓰게 될 프로퍼티 이름
    string? TypeRef,         // Func<..> / Action<..> / 전용 대리자 / Get<T> / Prop<T> / Ev<T>
    string? DelegateDecl,    // 전용 대리자가 필요하면 그 선언문
    string ExplicitImpl,     // 명시적 인터페이스 구현문
    bool Required,
    string? Initializer);    // 이벤트처럼 기본값이 있는 경우

/// <summary>인터페이스 원형정의 하나에 대응하는 익명구현 클래스 모델.</summary>
sealed record AnonTarget(
    string DefKey,                        // global::App.IRepo<T>  (원형정의)
    string ImplName,                      // __Anon_App_IRepo
    string TypeParams,                    // "<T>" 또는 ""
    string Constraints,                   // "where T : class" 또는 ""
    EquatableArray<BaseRef> Bases,        // 상속중인 인터페이스들 (체이닝 후보)
    EquatableArray<AnonMember> Members)
{
    public string ImplRef(string typeArgs) => $"global::Naratteu.Anonymous.Generated.{ImplName}{typeArgs}";
}

/// <summary>파생 인터페이스에서 바라본 상위 인터페이스 한 개.</summary>
sealed record BaseRef(string DefKey, string TypeArgs);

/// <summary>호출지점 하나에서 뽑아낸 정보.</summary>
sealed record Candidate(
    string InvokedName,
    bool GenericForm,
    string ConstructedRef,    // global::App.IRepo<global::System.String>
    string TypeArgs,          // "<global::System.String>" 또는 ""
    AnonTarget? Target,
    LocationInfo? At,
    EquatableArray<DiagInfo> Diagnostics);

sealed record DiagInfo(string Id, string Arg, LocationInfo? Where)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Diags.ById[Id], Where?.ToLocation(), Arg);
}

sealed record LocationInfo(string Path, TextSpanInfo Span, LinePositionInfo Start, LinePositionInfo End)
{
    public static LocationInfo? From(Location l) => l.SourceTree is null ? null : new(
        l.SourceTree.FilePath,
        new(l.SourceSpan.Start, l.SourceSpan.Length),
        new(l.GetLineSpan().StartLinePosition.Line, l.GetLineSpan().StartLinePosition.Character),
        new(l.GetLineSpan().EndLinePosition.Line, l.GetLineSpan().EndLinePosition.Character));

    public Location ToLocation() => Location.Create(Path,
        new(Span.Start, Span.Length),
        new(new(Start.Line, Start.Character), new(End.Line, End.Character)));
}

record struct TextSpanInfo(int Start, int Length);
record struct LinePositionInfo(int Line, int Character);
