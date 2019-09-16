using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DeadCodeAnalysis.Library
{
    internal class TypeFriendlyDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
		public TypeFriendlyDictionary()
			: base(new TypeAndMethodBaseComparer<TKey>())
		{
		}

	}
}
