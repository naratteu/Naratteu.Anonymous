using System.Collections;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Naratteu.Anonymous;

static class Exts
{
    public static string Join<T>(this IEnumerable<T> tt, string sep = ", ") => string.Join(sep, tt);

    /// <summary>global:: 을 포함한 완전한 이름. nullable 표기까지 살린다.</summary>
    public static string Fq(this ITypeSymbol t) => t.ToDisplayString(Formats.Fq);

    public static string Esc(this string name) => SyntaxFacts.IsValidIdentifier(name) && !SyntaxFacts.IsReservedKeyword(SyntaxFacts.GetKeywordKind(name)) ? name : "@" + name;

    /// <summary>식별자에 쓸 수 있는 글자만 남긴다. (오버로드 이름 구분용)</summary>
    public static string Sanitize(this string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) || c is '_' ? c : '_');
        return sb.ToString().Trim('_');
    }

    public static EquatableArray<T> ToEquatable<T>(this IEnumerable<T> tt) where T : IEquatable<T> => new([.. tt]);
}

static class Formats
{
    public static readonly SymbolDisplayFormat Fq = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static readonly SymbolDisplayFormat Short = SymbolDisplayFormat.MinimallyQualifiedFormat;
}

/// <summary>배열을 값처럼 비교시켜 증분 파이프라인의 캐시가 동작하게 만드는 래퍼.</summary>
readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
    readonly ImmutableArray<T> _items = items;

    public ImmutableArray<T> Items => _items.IsDefault ? [] : _items;
    public int Count => Items.Length;

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());
    public override bool Equals(object? obj) => obj is EquatableArray<T> o && Equals(o);
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var i in Items) hash = hash * 31 + (i?.GetHashCode() ?? 0);
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
