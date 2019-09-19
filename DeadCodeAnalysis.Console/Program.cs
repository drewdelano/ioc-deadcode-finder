using DeadCodeAnalysis.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mvc = System.Web.Mvc;
using Http = System.Web.Http;
using DeadCodeAnalysis.Plugins;

namespace DeadCodeAnalysis.ConsoleExample
{
	class Program
	{
        // see usage later if this doesn't fit your model cleanly
		const string BuildDir = @"C:\your_project_path\bin\Debug";

		static void Main(string[] args)
        {
            // you can use the standard AssemblyResolve on AppDomain to customize where to load assemblies from
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            var analyzer = new DeadCodeAnalyzer();
            analyzer.Plugins.Add(new IgnoreJetBrainsPluigin());
            analyzer.Plugins.Add(new AutofacConstructorPlugin());

            // define which assemblies are yours (you probably don't care about dependencies)
            analyzer.AssembliesToAnalyze =
                Directory.GetFiles(BuildDir, "*.dll", SearchOption.AllDirectories)
                .Where(f => !f.Contains("Tests")) // remember to exclude test assemblies
                .Select(f => Assembly.LoadFile(f))
                .ToList();

            // define which code is called implicitly
            analyzer.EntryPoints =
                analyzer.AssembliesToAnalyze
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => FindEntryPoints(t))
                .Where(mi => mi != null)
                .ToList();

            // dynamically mark types are used implictily
            analyzer.MarkMethodsAsImplicitlyCalled = typeSeen =>
            {
                if (typeSeen.Name == "MySpecialType")
                {
                    // if you want to implicitly mark some methods on a type called, because of reflection code
                    // but you only want to mark them if the class is seen then you can do that here
                    return new List<MethodBase> { /* return implicitly called methods here */ };
                }
                return null;
            };

            // this is where all of the magic happens
            var report = analyzer.Analyze();

            // after analysis you can further filter the results before outputting anything
            var namespacesToIgnore = new[] { "" }; // for example, there are often namespaces that you just want to ignore completely
            var unusedClassesFiltered = report.UnusedClasses
                .OrderBy(uc => uc.Namespace)
                .Where(uc =>
                       uc.IgnoreProbableConstClasses()
                    && namespacesToIgnore.Any(badns => uc.Namespace?.Contains(badns) ?? false) == false)
                .Select(uc => uc.FullName)
                .ToList();

            // since the unused methods and classes are returned as Type and MethodBase objects you can decide how to format them too
            FormatOutputForFile(report, unusedClassesFiltered);

            Console.WriteLine("Done!");
            Console.ReadKey();
        }

        static void FormatOutputForFile(GraphTrackingResults report, List<string> unusedClassesFiltered)
        {
            File.WriteAllText(@"report.txt", $@"The following classes are unused:
{
                string.Join(Environment.NewLine, unusedClassesFiltered)
            }

Count: {unusedClassesFiltered.Count}

The following methods are unused:
{
                string.Join(Environment.NewLine,
                    report.UnusedMethods
                    .SelectMany(mi => mi.Value)
                    .Select(mi => mi.ToString()))
            }");
        }

        // how do you want to define things as being called?
        // if you're reflection light you might not need this
        // or you may be able to use the containers you have to define this
        static List<MethodInfo> FindEntryPoints(Type type)
        {
            if (type.IsAbstract || type.IsInterface)
            {
                return new List<MethodInfo> { };
            }
            var typeName = type.Name;
            var ns = type.Namespace;
            var typeAsm = type.Assembly.FullName;

            if (typeof(Mvc.ControllerBase).IsAssignableFrom(type))
            {
                // all routed Controller methods
                return type.GetMethods().ToList();
            }
            else if (typeof(Http.ApiController).IsAssignableFrom(type))
            {
                // all routed ApiController methods
                return type.GetMethods().ToList();
            }

            return new List<MethodInfo> { };
        }
        
		static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			// probably worth having a breakpoint in here just to be sure everything is loading properly

			return null;
		}
	}
}
