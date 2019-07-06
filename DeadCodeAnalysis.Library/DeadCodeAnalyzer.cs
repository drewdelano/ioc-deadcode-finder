using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DeadCodeAnalysis.Library
{
    public class DeadCodeAnalyzer
    {
        public List<Assembly> AssembliesToAnalyze { get; set; } = new List<Assembly>();

        public List<MethodInfo> EntryPoints { get; set; } = new List<MethodInfo>();

        public GraphTrackingResults Analyze()
        {
            var graph = new GraphTrackingResults();
            graph.GenerateMethodGraph(AssembliesToAnalyze);

            var ilWalker = new ILCallWalker(graph);
            ilWalker.EntryPoints = EntryPoints;
            ilWalker.WalkMethod();

            graph.BuildReport();
            return graph;
        }

    }
}
