using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DeadCodeAnalysis.Library
{
	public abstract class Plugin
	{
		public virtual IEnumerable<Type> FilterUnusedTypes(IEnumerable<Type> inputs) => inputs;

		public virtual IEnumerable<MethodBase> FilterUnusedMethods(IEnumerable<MethodBase> inputs) => inputs;

		public virtual List<MethodBase> NewTypeSeen(Type typeSeen) => new List<MethodBase>();

		public virtual List<MethodBase> NewMethodSeen(MethodBase methodBase) => new List<MethodBase>();

		public virtual List<Assembly> ListOtherFoundAssemblies() => new List<Assembly>();

	}
}
