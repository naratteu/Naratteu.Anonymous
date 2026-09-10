using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>남이 만든 인터페이스(BCL)도 그대로 익명구현된다.</summary>
static class Bcl
{
    public static void Run()
    {
        using (IDisposable d = IDisposable.New(new() { Dispose = () => Console.WriteLine("정리됨") }))
            Console.WriteLine("using 블록 안");

        IComparer<int> desc = IComparer<int>.New(new() { Compare = (a, b) => b.CompareTo(a) });
        var xs = new List<int> { 3, 1, 2 };
        xs.Sort(desc);
        Console.WriteLine($"내림차순: {string.Join(",", xs)}");

        // 같은 이름이 여러 인터페이스에서 올라오면 선언한 인터페이스 이름으로 갈라준다
        IEnumerable<int> seq = IEnumerable<int>.New(new()
        {
            GetEnumerator_IEnumerable_T = () => Count().GetEnumerator(),
            GetEnumerator_IEnumerable = () => Count().GetEnumerator(),
        });
        Console.WriteLine($"열거: {string.Join(",", seq)}");

        static IEnumerable<int> Count() { yield return 1; yield return 2; yield return 3; }
    }
}
