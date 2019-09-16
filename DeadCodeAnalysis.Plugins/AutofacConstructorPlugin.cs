using DeadCodeAnalysis.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DeadCodeAnalysis.Plugins
{
	public class AutofacConstructorPlugin : Plugin
	{
		public override List<MethodBase> NewTypeSeen(Type typeSeen)
		{
			base.NewTypeSeen(typeSeen);

			var methods = new List<MethodBase>();

			// treat the load of any seen Module as called
			if (typeof(global::Autofac.Module).IsAssignableFrom(typeSeen)) {
				methods.Add(typeSeen.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance));
			}

			// autofac's default contructor picker behavior:
			// pick the constructor with the most parameters as long as it isn't tied with another
			var ctors = typeSeen.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
			var iocCtors = ctors
				.Select(c => new { c, len = c.GetParameters().Length })
				.GroupBy(g => g.len)
				.OrderByDescending(g => g.Key)
				.Take(1)
				.SelectMany(g => g)
				.ToList();
			if (iocCtors.Count == 1) {
				methods.Add(iocCtors[0].c);
			}

			return methods;
		}
	}
}
