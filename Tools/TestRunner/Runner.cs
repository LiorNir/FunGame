using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute { }

    public static class Assert
    {
        public static void AreEqual(object expected, object actual)
        {
            if (!Equals(expected, actual)) throw new Exception($"Expected <{expected}> but was <{actual}>");
        }
        public static void AreEqual(float expected, float actual, float tolerance)
        {
            if (Math.Abs(expected - actual) > tolerance) throw new Exception($"Expected <{expected}> +/- {tolerance} but was <{actual}>");
        }
        public static void Greater(float a, float b) { if (!(a > b)) throw new Exception($"Expected {a} > {b}"); }
        public static void IsTrue(bool condition) { if (!condition) throw new Exception("Expected true"); }
        public static void IsFalse(bool condition) { if (condition) throw new Exception("Expected false"); }
        public static void IsEmpty(IEnumerable e)
        {
            var any = false;
            foreach (var x in e) { any = true; Console.WriteLine("   unexpected: " + x); }
            if (any) throw new Exception("Expected empty");
        }
        public static void Throws<T>(Action a) where T : Exception
        {
            try { a(); }
            catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
    }
}

namespace NUnit.Framework
{
    public static class CollectionAssert
    {
        public static void AreEqual(System.Collections.IEnumerable a, System.Collections.IEnumerable b)
        {
            var x = a.Cast<object>().ToList(); var y = b.Cast<object>().ToList();
            if (x.Count != y.Count) throw new Exception($"Collections differ in size {x.Count} vs {y.Count}");
            for (var i = 0; i < x.Count; i++) if (!Equals(x[i], y[i])) throw new Exception($"Collections differ at {i}");
        }
    }
}

public static class Runner
{
    public static int Main()
    {
        return Main1();
    }
    public static int Main1()
    {
        var failed = 0;
        var total = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                total++;
                var instance = Activator.CreateInstance(type);
                try
                {
                    method.Invoke(instance, null);
                    Console.WriteLine("PASS " + type.Name + "." + method.Name);
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine("FAIL " + type.Name + "." + method.Name + ": " + e.InnerException.Message);
                }
            }
        }
        Console.WriteLine($"{total - failed}/{total} passed");
        return failed;
    }
}
