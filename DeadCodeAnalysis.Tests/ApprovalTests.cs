using ApprovalTests;
using DeadCodeAnalysis.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tests.DeadCodeProject.Controllers;
using Tests.ProjectWithDeadCode;
using Xunit;

namespace DeadCodeAnalysis.Tests
{
	// Attributes
	// Lambdas
	// Generics
    public class ApprovalTests
    {
        [Fact]
        public void HappyPath()
        {
            //+Arrange
            var allMethods = GetMethodsFromAssembly(typeof(Class1).Assembly);

            var analyzer = new DeadCodeAnalyzer();
            analyzer.AssembliesToAnalyze.Add(typeof(Class1).Assembly);
            analyzer.EntryPoints.Add(typeof(Class1).GetMethod(nameof(Class1.Called)));

            //+Act
            var report = analyzer.Analyze();

            //+Assert
            string output = FormatVerify(allMethods, report);
            Approvals.Verify(output);
        }

        [Fact]
        public void HappyPathMvcSite()
        {
            //+Arrange
            var allMethods = GetMethodsFromAssembly(typeof(HomeController).Assembly);

            var analyzer = new DeadCodeAnalyzer();
            analyzer.AssembliesToAnalyze.Add(typeof(HomeController).Assembly);
            analyzer.EntryPoints.Add(typeof(HomeController).GetMethod(nameof(HomeController.Index)));

            //+Act
            var report = analyzer.Analyze();

            //+Assert
            var output = FormatVerify(allMethods, report);
            Approvals.Verify(output);
        }


        string FormatVerify(List<string> allMethods, GraphTrackingResults report)
        {
            return $@"All classes and methods:
{FormatLines(allMethods)}

Uncalled Classes:
{FormatLines(report.UnusedClasses.Select(uc => uc.FullName))}

Uncalled Methods:
{FormatLines(report.UnusedMethods.SelectMany(c => c.Value).Select(um => FormatMethod(um)))}
";
        }

        List<string> GetMethodsFromAssembly(Assembly assembly)
        {
            var bindingFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var allMethods = assembly
                .GetTypes()
                .Where(t => t.Name.StartsWith("<") == false)
                .SelectMany(t => t.GetMethods(bindingFlags))
                .Select(mi => FormatMethod(mi))
                .ToList();
            return allMethods;
        }

        string FormatMethod(MethodBase methodBase)
        {
            Func<MethodBase, string, string> fmt = (mi, returnType) => $"{mi.DeclaringType.FullName}: {returnType} {mi.Name.Split('<')[0].TrimEnd('+')}({string.Join(", ", mi.GetParameters().Select(p => p.ToString()))})";
            if (methodBase is MethodInfo mb)
            {
                return fmt(methodBase, mb.ReturnType.Name);
            }
            else
            {
                return fmt(methodBase, ".ctor");
            }
        }

        string FormatLines(IEnumerable<string> lines)
        {
            return lines.Any() ? string.Join(Environment.NewLine, lines) : "<None>";
        }
    }
}
