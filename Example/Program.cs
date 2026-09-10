using System;

// ============================================================
//  1) 자바 익명클래스처럼: 인터페이스를 그 자리에서 구현
// ============================================================
IGreeter greeter = IGreeter.New(new()
{
    Greet = name => $"안녕, {name}!",
    Shout = Console.WriteLine,
});
Console.WriteLine(greeter.Greet("성원"));
greeter.Shout("확장 형태 호출 성공");

// 제네릭 형태도 같이 만들어진다
IGreeter another = Anon.New<IGreeter>(new()
{
    Greet = n => $"hi {n}",
    Shout = s => Console.WriteLine($"[{s}]"),
});
Console.WriteLine(another.Greet("world"));

// ============================================================
//  2) 프로퍼티: 값으로 주거나, 접근자를 직접 주거나
// ============================================================
var log = new System.Collections.Generic.List<string>();
IConfig config = IConfig.New(new()
{
    Name = "beambeam",                       // 읽기전용 → 값 그대로
    Retry = new() { get = () => 3 },         // 읽기전용 → 게터 직접
    Level = 1,                               // 읽고쓰기 → 자동 저장소가 붙는다
    Trace = new()                            // 읽고쓰기 → 접근자 직접
    {
        get = () => log.Count > 0,
        set = v => log.Add($"Trace={v}"),
    },
});
config.Level++;
config.Trace = true;
Console.WriteLine($"{config.Name} / Retry={config.Retry} / Level={config.Level} / Trace={config.Trace} / log=[{string.Join(",", log)}]");

// ============================================================
//  3) 제네릭 인터페이스 (제약조건까지 따라온다)
// ============================================================
IRepository<string> repo = IRepository<string>.New(new()
{
    Load = id => $"row#{id}",
    Save = (id, row) => Console.WriteLine($"저장: {id} <- {row}"),
});
Console.WriteLine(repo.Load(7));
repo.Save(7, "값");

// ============================================================
//  4) 파생 인터페이스 — 제네릭 형태로도 안 겹친다
// ============================================================
IGreeterEx ex = Anon.New<IGreeterEx>(new()
{
    Greet = n => $"안녕하세요, {n}",
    Shout = Console.WriteLine,
    Bye = () => "안녕히",
});
Console.WriteLine($"{ex.Greet("여러분")} / {ex.Bye()}");

// ============================================================
//  5) 까다로운 시그니처: out, 오버로드, 인덱서, 이벤트
// ============================================================
var bus = new Naratteu.Anonymous.Ev<Action<string>>();
ITricky tricky = ITricky.New(new()
{
    TryParse = (string s, out int v) => int.TryParse(s, out v),
    Do_int = i => Console.WriteLine($"Do(int) {i}"),
    Do_string = s => Console.WriteLine($"Do(string) {s}"),
    Item_get = i => i * i,
    Item_set = (i, v) => Console.WriteLine($"this[{i}] = {v}"),
    Changed = bus,
});
Console.WriteLine($"TryParse(\"42\") = {tricky.TryParse("42", out var parsed)}, {parsed}");
tricky.Do(1);
tricky.Do("하나");
Console.WriteLine($"tricky[9] = {tricky[9]}");
tricky[2] = 100;
tricky.Changed += s => Console.WriteLine($"이벤트: {s}");
bus.Handler?.Invoke("발사");

// ============================================================
//  6) 원래 있는 New 메서드는 건드리지 않는다
// ============================================================
Console.WriteLine(Legacy.New(new() { Value = 99 }).Value);

// ============================================================
//  7) BCL 인터페이스
// ============================================================
Bcl.Run();


interface IGreeter
{
    string Greet(string name);
    void Shout(string message);
}

interface IGreeterEx : IGreeter
{
    string Bye();
}

interface IConfig
{
    string Name { get; }
    int Retry { get; }
    int Level { get; set; }
    bool Trace { get; set; }
}

interface IRepository<T> where T : class
{
    T Load(int id);
    void Save(int id, T row);
}

interface ITricky
{
    bool TryParse(string text, out int value);
    void Do(int value);
    void Do(string value);
    int this[int index] { get; set; }
    event Action<string>? Changed;
}

class Legacy
{
    public int Value { get; init; }
    public static Legacy New(Legacy self) => self;
}
