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

		public Func<Type, List<MethodBase>> MarkMethodsAsImplicitlyCalled { get; set; }

		public List<Plugin> Plugins { get; } = new List<Plugin>();

		public GraphTrackingResults Analyze()
		{
			var graph = new GraphTrackingResults(Plugins);
			graph.GenerateMethodGraph(AssembliesToAnalyze);

			var ilWalker = new ILCallWalker(graph);
			ilWalker.EntryPoints = EntryPoints;
			ilWalker.Plugins.AddRange(Plugins);
			ilWalker.MarkMethodsAsImplicitlyCalled = MarkMethodsAsImplicitlyCalled;
			ilWalker.WalkMethod();

			graph.FinalizeReport();
			return graph;
		}

	}
}
