using Microsoft.CodeAnalysis;

namespace Naratteu.Anonymous;

static class Diags
{
    const string Category = "Naratteu.Anonymous";

    public static readonly DiagnosticDescriptor NotAnInterface = new("ANON001",
        "익명구현 대상이 인터페이스가 아님",
        "'{0}' 은(는) 인터페이스가 아니라서 익명구현을 만들 수 없습니다",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor UnsupportedMember = new("ANON002",
        "대리자로 표현할 수 없는 멤버",
        "'{0}' 은(는) 대리자로 표현할 수 없어 호출시 NotSupportedException 을 던지도록 구현됩니다",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor AmbiguousGenericForm = new("ANON003",
        "제네릭 형태 호출이 모호함",
        "'{0}' 은(는) 상속관계인 다른 대상과 겹쳐 제네릭 형태로는 모호합니다. 확장 형태로 호출하세요.",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor CannotImplement = new("ANON004",
        "익명구현을 만들 수 없는 인터페이스",
        "'{0}' 은(는) 인스턴스 대리자로 옮길 수 없는 멤버(static abstract / 연산자 / 비공개 멤버)를 가지고 있어 익명구현을 만들 수 없습니다",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ExtensionNeedsCSharp14 = new("ANON005",
        "확장 형태는 C# 14 부터",
        "'{0}' 을(를) 확장 형태로 부르려면 C# 14 가 필요합니다. LangVersion 을 올리거나 {1}.{2}<{0}>(..) 로 부르세요.",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly Dictionary<string, DiagnosticDescriptor> ById = new()
    {
        [NotAnInterface.Id] = NotAnInterface,
        [UnsupportedMember.Id] = UnsupportedMember,
        [AmbiguousGenericForm.Id] = AmbiguousGenericForm,
        [CannotImplement.Id] = CannotImplement,
        [ExtensionNeedsCSharp14.Id] = ExtensionNeedsCSharp14,
    };
}
