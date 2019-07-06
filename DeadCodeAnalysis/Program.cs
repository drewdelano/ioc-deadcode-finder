using DeadCodeAnalysis.Library;
using System;
using System.Reflection;
using Tests.ProjectWithDeadCode;

namespace DeadCodeAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            var analyzer = new DeadCodeAnalyzer();
            analyzer.AssembliesToAnalyze.Add(typeof(Class1).Assembly);
            analyzer.EntryPoints.Add(typeof(Class1).GetMethod(nameof(Class1.Called)));
            var report = analyzer.Analyze();
        }
    }
}
