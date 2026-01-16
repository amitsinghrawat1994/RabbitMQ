using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        string[] possible = new[] { "MassTransit.EntityFrameworkCore", "MassTransit.EntityFrameworkCoreIntegration", "MassTransit.EntityFrameworkCore.Integration" };
        Assembly asm = null;
        foreach (var name in possible)
        {
            try
            {
                asm = Assembly.Load(name);
                if (asm != null) break;
            }
            catch { }
        }

        if (asm == null)
        {
            Console.WriteLine("MassTransit.EntityFrameworkCore assembly not found. Listing loaded assemblies:");
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies().OrderBy(x => x.GetName().Name))
                Console.WriteLine(" - " + a.GetName().Name);
            return;
        }

        Console.WriteLine("Found assembly: " + asm.GetName());

        var types = asm.GetTypes().Where(t => t.Name.Contains("EntityFramework")).OrderBy(t => t.Name).ToArray();
        Console.WriteLine("Types matching 'EntityFramework*':");
        foreach (var t in types)
        {
            Console.WriteLine(" - " + t.FullName);
        }

        // List methods on IEntityFrameworkSagaRepositoryConfigurator`1
        var repoTypeGen = asm.GetType("MassTransit.IEntityFrameworkSagaRepositoryConfigurator`1");
        var repoType = asm.GetType("MassTransit.IEntityFrameworkSagaRepositoryConfigurator");

        if (repoType == null || repoTypeGen == null)
        {
            Console.WriteLine("Repository configurator type(s) not found in assembly.");
            return;
        }

        Console.WriteLine("\nMethods on MassTransit.IEntityFrameworkSagaRepositoryConfigurator (non-generic):");
        foreach (var m in repoType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            Console.WriteLine($" - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name))}) : {m.ReturnType.FullName}");
        }

        Console.WriteLine("\nMethods on MassTransit.IEntityFrameworkSagaRepositoryConfigurator<T>:");
        foreach (var m in repoTypeGen.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            Console.WriteLine($" - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name))}) : {m.ReturnType.FullName}");
            foreach (var p in m.GetParameters())
            {
                var pt = p.ParameterType;
                Console.WriteLine($"    Param: {pt.FullName} (IsDelegate={typeof(Delegate).IsAssignableFrom(pt)})");
                if (pt.IsGenericType)
                {
                    var args = pt.GetGenericArguments();
                    Console.WriteLine($"    Generic Args: {string.Join(", ", args.Select(ga => ga.FullName))}");
                    foreach (var ga in args)
                    {
                        if (ga.IsGenericType)
                        {
                            Console.WriteLine($"      Arg inner: {ga.FullName}");
                        }
                    }
                }
            }
        }

        // List ILockStatementProvider implementations
        var lockProviderType = asm.GetType("MassTransit.EntityFrameworkCoreIntegration.ILockStatementProvider");
        if (lockProviderType != null)
        {
            Console.WriteLine("\nILockStatementProvider implementations:");
            var impls = asm.GetTypes().Where(t => lockProviderType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            foreach (var impl in impls)
                Console.WriteLine($" - {impl.FullName}");
        }
    }
}
