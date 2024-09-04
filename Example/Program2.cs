using Naratteu.AnonymousClass;

var hello = new Example.Hello.Inner
{
    _World = (a, b) => $"hello world! {a} + {b} = {a + b}"
};
var hello2 = new Example.Hello.Inner
{
    _World = (a, b) => $"hello world twoo! {a} - {b} = {a - b}"
};

Console.WriteLine(hello.World(1, 2));
Console.WriteLine(hello2.World(4, 3));

class Hello
{
    public virtual string World(int a, int b) => throw new Exception("미구현!");
    public class Inner : Hello
    {
        public WorldD? _World { private get; init; }
        public delegate string WorldD(int a, int b);
        public override string World(int a, int b) => (_World ?? base.World)(a, b);
    };
}

namespace Example
{
    [AnonymousClass]
    partial class Hello
    {
        public virtual string World(int a, int b) => throw new Exception("미구현!");
        protected virtual string World2(int a, int b) => throw new Exception("미구현!");
        int a;
        public virtual ref int World3(ref int v) => throw new Exception("미구현!");
    }
}