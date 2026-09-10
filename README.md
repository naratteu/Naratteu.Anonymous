# Naratteu.Anonymous

[![NuGet](https://img.shields.io/nuget/v/Naratteu.Anonymous)](https://www.nuget.org/packages/Naratteu.Anonymous)

인터페이스를 자바 익명클래스처럼 **그 자리에서** 구현하게 해주는 소스제너레이터.

C# 에는 자바의 익명클래스가 없다. ([왜 없는지](https://chatgpt.com/s/t_6aa204ce1c288191b982085d6e79819b))
대신 `new() { .. }` 의 대상타입추론과 소스제너레이터를 엮으면 거의 같은 자리에 같은 모양으로 쓸 수 있다.

## 바로 해보기

프로젝트 만들 것 없이 붙여넣고 돌리면 된다. (.NET 10 SDK)

### 메서드

```bash
dotnet run - <<'EOF'
#:package Naratteu.Anonymous@0.0.1

IGreeter greeter = IGreeter.New(new()
{
    Greet = name => $"안녕, {name}!",
    Shout = Console.WriteLine,
});

Console.WriteLine(greeter.Greet("성원"));
greeter.Shout("확장 형태 호출");

interface IGreeter
{
    string Greet(string name);
    void Shout(string message);
}
EOF
```

```
안녕, 성원!
확장 형태 호출
```

### 프로퍼티 — 값을 그냥 주거나, 접근자를 직접 주거나

```bash
dotnet run - <<'EOF'
#:package Naratteu.Anonymous@0.0.1

var log = new List<string>();

IConfig config = IConfig.New(new()
{
    Name = "beambeam",                     // 값 그대로 넣으면 그게 게터
    Level = 1,                             // 읽고쓰기: 자동 저장소가 붙는다
    Trace = new()                          // 읽고쓰기: 접근자를 직접
    {
        get = () => log.Count > 0,
        set = v => log.Add($"Trace={v}"),
    },
});

config.Level++;
config.Trace = true;
Console.WriteLine($"{config.Name} / Level={config.Level} / Trace={config.Trace} / [{string.Join(",", log)}]");

interface IConfig
{
    string Name { get; }
    int Level { get; set; }
    bool Trace { get; set; }
}
EOF
```

```
beambeam / Level=2 / Trace=True / [Trace=True]
```

### 남이 만든 인터페이스도, 제네릭 형태도

```bash
dotnet run - <<'EOF'
#:package Naratteu.Anonymous@0.0.1

using (IDisposable d = IDisposable.New(new() { Dispose = () => Console.WriteLine("정리됨") }))
    Console.WriteLine("using 블록 안");

IComparer<int> desc = IComparer<int>.New(new() { Compare = (a, b) => b.CompareTo(a) });
List<int> xs = [3, 1, 2];
xs.Sort(desc);
Console.WriteLine($"내림차순: {string.Join(",", xs)}");

IGreeter g = Anon.New<IGreeter>(new() { Greet = n => $"hi {n}" });
Console.WriteLine(g.Greet("world"));

interface IGreeter { string Greet(string name); }
EOF
```

```
using 블록 안
정리됨
내림차순: 3,2,1
hi world
```

## 프로젝트에서 쓰려면

```bash
dotnet add package Naratteu.Anonymous
```

빌드할 때만 필요한 소스제너레이터라 결과물에 런타임 의존성이 남지 않는다.

확장 형태(`IGreeter.New(..)`)는 C# 14 를 쓰므로 .NET 10 SDK 가 필요하다.
그 아래 버전에서는 확장 형태를 아예 만들지 않고 `ANON005` 로 알려주며, 제네릭 형태
(`Anon.New<IGreeter>(..)`)는 그대로 돌아간다. (.NET 9 SDK 에서 확인)

## 어떻게 도는가

`IGreeter.New(new() { .. })` 같은 호출을 생성기가 **호출지점에서** 발견하면, 그 인터페이스를
대리자 프로퍼티로 구현한 클래스와 진입점을 만들어낸다.

```csharp
// 생성물
internal class __Anon_IGreeter : global::IGreeter
{
    string global::IGreeter.Greet(string name) => Greet(name);
    void global::IGreeter.Shout(string message) => Shout(message);

    public required global::System.Func<string, string> Greet { get; init; }
    public required global::System.Action<string> Shout { get; init; }
}

internal static class AnonExtensions
{
    extension(global::IGreeter)
    {
        public static global::IGreeter New(__Anon_IGreeter impl) => impl;
    }
}
```

`new() { .. }` 는 대상타입추론이라 파라미터 타입이 곧 초기화자의 모양이 되고, `required` 가
"구현 안 한 멤버가 있으면 컴파일 에러" 를 그대로 만들어준다.

## 두 가지 호출 형태

둘 다 만들어진다. 취향껏 쓰면 된다.

```csharp
IGreeter a = IGreeter.New(new() { .. });        // 확장 형태  — C# 14 (net10) 필요
IGreeter b = Anon.New<IGreeter>(new() { .. });  // 제네릭 형태 — C# 11 이상
```

- **확장 형태**는 C# 14 의 확장 정적 멤버(`extension(IGreeter) { public static .. }`)를 쓴다.
  인터페이스 이름으로 바로 호출하니 제일 짧고, 상속관계가 있어도 모호해지지 않는다.
  컴파일의 언어버전이 C# 14 에 못 미치면 이쪽은 만들지 않는다.
- **제네릭 형태**는 인터페이스마다 오버로드를 깔아두고 `where A : IGreeter` 제약으로 후보를 걸러낸다.
  파생 인터페이스끼리 겹치는 문제는 생성 클래스를 인터페이스 상속구조에 맞춰
  (`__Anon_IGreeterEx : __Anon_IGreeter`) 상속시켜 해결한다.

## 멤버 종류별로 어떻게 적는가

| 인터페이스 멤버 | 초기화자에 적는 것 |
| --- | --- |
| `string Greet(string name)` | `Greet = name => ..` (`Func`/`Action`) |
| `bool TryParse(string s, out int v)` | `TryParse = (string s, out int v) => ..` (전용 대리자) |
| `void Do(int)` / `void Do(string)` | `Do_int = ..`, `Do_string = ..` |
| `string Name { get; }` | `Name = "값"` 또는 `Name = new() { get = () => .. }` |
| `int Level { get; set; }` | `Level = 1` 또는 `Level = new() { get = .., set = .. }` |
| `int this[int i] { get; set; }` | `Item_get = i => ..`, `Item_set = (i, v) => ..` |
| `event Action<string>? Changed` | 안 적으면 보통의 자동구현 이벤트. `Changed = ev` 로 `Ev<T>` 를 꽂아 직접 발사할 수도 있다 |

프로퍼티는 `Get<T>` / `Prop<T>` / `Set<T>` 로 감싼다. **값을 그냥 대입하면** 암시적 변환으로
자동 저장소가 붙고, **접근자를 직접 주고 싶으면** `new() { get = .., set = .. }` 로 적는다.
(유니온이 정식으로 들어오기 전까진 이 조합이 같은 일을 한다)

## 설정

```xml
<PropertyGroup>
  <AnonymousEntryName>New</AnonymousEntryName>    <!-- IGreeter.New(..)       -->
  <AnonymousHolderName>Anon</AnonymousHolderName> <!-- Anon.New<IGreeter>(..) -->
</PropertyGroup>
```

패키지로 참조하면 그대로 먹고, `ProjectReference` 로 물릴 땐 `CompilerVisibleProperty` 를 같이 적어야 한다.

## 되는 것 / 안 되는 것

되는 것: 메서드 · 프로퍼티 · 인덱서 · 이벤트, 오버로드, `out`/`ref`/`ref readonly`,
`ref` 반환, 17개 이상 파라미터, 제네릭 인터페이스(제약조건 포함), 상속받은 멤버, BCL 인터페이스.
기본구현(DIM)이 있는 멤버는 건드리지 않는다.

| 진단 | 뜻 |
| --- | --- |
| `ANON001` | 대상이 인터페이스가 아님 |
| `ANON002` | 제네릭 메서드는 대리자로 못 옮긴다. 호출하면 `NotSupportedException` |
| `ANON003` | 제네릭 형태 호출이 상속관계 때문에 모호하다. 확장 형태로 부르면 된다 |
| `ANON004` | `static abstract` · 연산자 · 비공개 멤버가 있어 익명구현 자체가 불가능 |
| `ANON005` | 확장 형태로 불렀는데 언어버전이 C# 14 에 못 미친다 |

## 배포

nuget.org [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) 을 쓴다.
긴수명 API 키를 두지 않고, GitHub 이 발급한 OIDC 토큰을 nuget.org 가 검증해서 1시간짜리
임시 키를 내주는 방식이다. `v0.0.1` 처럼 태그를 밀면
[`.github/workflows/publish.yml`](.github/workflows/publish.yml) 이 돈다.

## 라이센스

[VibeCoded AI-Slop License v1.0](LICENSE)

## 선행작업

같은 발상을 굴려본 흔적들. 이 저장소는 두 번째 것의 히스토리를 이어받았다.

- [Naratteu.StrongDuck](https://github.com/naratteu/Naratteu.StrongDuck)
- [Naratteu.MemberCollector @ AnonymousClass](https://github.com/naratteu/Naratteu.MemberCollector/tree/AnonymousClass)
