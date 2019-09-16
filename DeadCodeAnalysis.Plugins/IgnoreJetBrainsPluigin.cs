using DeadCodeAnalysis.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeadCodeAnalysis.Plugins
{
	public class IgnoreJetBrainsPluigin : Plugin
	{
		public override IEnumerable<Type> FilterUnusedTypes(IEnumerable<Type> inputs)
		{
			return inputs.Where(i => i.Namespace?.Contains("JetBrains") == false);
		}
	}
}
