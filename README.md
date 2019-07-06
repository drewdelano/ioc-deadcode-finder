# ioc-deadcode-finder

**still very rough!**

I keep wanting someway to find dead code to remove from large projects, but not having a way to tell what is referenced.  The aim of this project is to find unreferenced code.  

The plan is to do that by:
- configuring which projects should be analyzed
- configuring known called entry points (these are things called by reflection or configured by an IoC e.g. MyCustomerController when using Autofacs MVC configuration)
- analyzing the project by walking the call graphs of the known entry points (since we can't determine which implementations of interfaces will be called we simply treat all of them as being called)
- generate a report object that let's us follow up for manual analysis (it's still important to think about removing things before removing them) and remove them, if necessary

Sample code to use this would look something like this:
```
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
```

I haven't tested this on any production databases yet and the unit tests are still lacking. Use at your own risk for now.
