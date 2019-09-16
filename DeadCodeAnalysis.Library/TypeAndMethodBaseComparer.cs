using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DeadCodeAnalysis.Library
{
	internal class TypeAndMethodBaseComparer<TKey> : IEqualityComparer<TKey>
	{
		public bool Equals(TKey x, TKey y)
		{
			return GetHashCode(x) == GetHashCode(y);
		}

		public int GetHashCode(TKey obj)
		{
			var t = obj as Type;
			if (t != null) {
				return t.ToString().GetHashCode();
			}

			var mb = obj as MethodBase;
			if (mb != null) {
				return $"{mb.DeclaringType?.AssemblyQualifiedName ?? "WeirdGlobalSpace"}.{mb}({string.Join(", ", mb.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName ?? "WeirdGlobalSpace"))})".GetHashCode();
			}
			return obj.GetHashCode();
		}
	}
}
