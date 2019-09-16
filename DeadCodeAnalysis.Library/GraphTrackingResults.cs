using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DeadCodeAnalysis.Library
{
	public class GraphTrackingResults
	{
		TypeFriendlyDictionary<Type, TypeFriendlyDictionary<MethodBase, List<MethodBase>>> _interfaceToImplementationMap = new TypeFriendlyDictionary<Type, TypeFriendlyDictionary<MethodBase, List<MethodBase>>>();
		TypeFriendlyDictionary<Type, TypeFriendlyDictionary<MethodBase, bool>> _graph = new TypeFriendlyDictionary<Type, TypeFriendlyDictionary<MethodBase, bool>>();
		TypeFriendlyDictionary<MethodBase, bool> _methodsWeShouldntTrack = new TypeFriendlyDictionary<MethodBase, bool>();
		TypeFriendlyDictionary<Type, bool> _unusedClasses = new TypeFriendlyDictionary<Type, bool>();
		List<Plugin> _plugins;

		public GraphTrackingResults(List<Plugin> plugins)
		{
			_plugins = plugins;
		}

		public List<Type> UnusedClasses { get; private set; } = new List<Type>();

		public Dictionary<Type, List<MethodBase>> UnusedMethods { get; private set; } = new TypeFriendlyDictionary<Type, List<MethodBase>>();

		internal void FinalizeReport()
		{
			UnusedClasses = _plugins
				.Select(p => (Func<IEnumerable<Type>, IEnumerable<Type>>)p.FilterUnusedTypes)
				.Aggregate(_unusedClasses.Keys.AsEnumerable(), (s, f) => f(s), f => f)
				.ToList();


			var newUnusedMethods = new TypeFriendlyDictionary<Type, List<MethodBase>>();
			var plugins = _plugins.Select(p => (Func<IEnumerable<MethodBase>, IEnumerable<MethodBase>>)p.FilterUnusedMethods).ToList();
			foreach (var entry in UnusedMethods) {

				newUnusedMethods.Add(entry.Key, plugins
					.Aggregate(entry.Value.AsEnumerable(), (s, f) => f(s), f => f)
					.ToList());
			}
		}

		internal bool HasMethodBeenSeen(MethodBase methodBase, Action<Type> callback)
		{
			var method = DegenerifyMethod(methodBase);
			var declaringType = DegenerifyType(methodBase.DeclaringType);

			if (!IsMethodWeShouldTrack(method)) {
				if (!_methodsWeShouldntTrack.ContainsKey(method)) {
					_methodsWeShouldntTrack.Add(method, true);
					return false;
				} else {
					return true;
				}
			}

			// have we already looked at this method before
			if (_graph[declaringType].ContainsKey(method)) {
				if (_graph[declaringType][method]) {
					return true;
				}
				_graph[declaringType][method] = true;
			}
			UnusedMethods[declaringType].Remove(method);
			return false;
		}

		internal bool IsMethodWeShouldTrack(MethodBase methodBase)
		{
			if (methodBase.DeclaringType == null) {
				return false;
			}

			if (!_graph.ContainsKey(DegenerifyType(methodBase.DeclaringType))) {
				return false;
			}
			return true;
		}

		internal List<MethodBase> GetInterfaceImplementations(MethodBase methodBase)
		{
			var declaringType = DegenerifyType(methodBase.DeclaringType);
			var method = DegenerifyMethod(methodBase);

			if (!_interfaceToImplementationMap.ContainsKey(declaringType)) {
				return new List<MethodBase>();
			}
			if (!_interfaceToImplementationMap[declaringType].ContainsKey(method)) {
				return new List<MethodBase>();
			}
			return _interfaceToImplementationMap[declaringType][method];
		}

		static MethodBase DegenerifyMethod(MethodBase method)
		{
			if ((method?.IsGenericMethod ?? false) && method is MethodInfo) {
				method = (method as MethodInfo).GetGenericMethodDefinition();
			}

			return method;
		}

		static Type DegenerifyType(Type type)
		{
			if (type?.IsGenericType ?? false) {
				type = type.GetGenericTypeDefinition();
			}

			return type;
		}

		internal void GenerateMethodGraph(List<Assembly> assembliesToAnalyze)
		{
			assembliesToAnalyze.AddRange(_plugins.SelectMany(p => p.ListOtherFoundAssemblies()));

			_graph = new TypeFriendlyDictionary<Type, TypeFriendlyDictionary<MethodBase, bool>>();
			foreach (var assembly in assembliesToAnalyze) {
				foreach (var type in assembly.GetTypes()) {
					if (type.FullName.Contains("<")) {
						continue; // skip anonymous types
					}
					var degenericedType = DegenerifyType(type);
					_graph.Add(degenericedType, new TypeFriendlyDictionary<MethodBase, bool>());
					UnusedMethods.Add(type, new List<MethodBase>());
					if (!degenericedType.IsInterface) {
						TrackInterfaceImplementations(degenericedType);
					}
					TrackMethodUsage(degenericedType);
					_unusedClasses.Add(degenericedType, false);
				}
			}
		}

		internal bool MarkTypeSeen(Type type, Action<Type> callback)
		{
			var degenericedType = DegenerifyType(type);
			if (_unusedClasses.ContainsKey(degenericedType)) {
				_unusedClasses.Remove(degenericedType);
				callback?.Invoke(degenericedType);
				return true;
			}
			return false;
		}

		void TrackMethodUsage(Type type)
		{
			foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
				var degenericMethod = DegenerifyMethod(method);
				_graph[type].Add(degenericMethod, false);
				UnusedMethods[type].Add(degenericMethod);

				if (method.IsVirtual) {
					var baseMethodCalled = DegenerifyMethod(method.GetBaseDefinition());

					if (!_interfaceToImplementationMap.ContainsKey(baseMethodCalled.DeclaringType)) {
						_interfaceToImplementationMap.Add(baseMethodCalled.DeclaringType, new TypeFriendlyDictionary<MethodBase, List<MethodBase>>());
					}

					if (!_interfaceToImplementationMap[baseMethodCalled.DeclaringType].ContainsKey(baseMethodCalled)) {
						_interfaceToImplementationMap[baseMethodCalled.DeclaringType].Add(baseMethodCalled, new List<MethodBase>());
					}

					if (!_interfaceToImplementationMap[baseMethodCalled.DeclaringType][baseMethodCalled].Contains(degenericMethod)) {
						_interfaceToImplementationMap[baseMethodCalled.DeclaringType][baseMethodCalled].Add(degenericMethod);
					}
				}
			}
		}

		void TrackInterfaceImplementations(Type type)
		{
			foreach (var iface in type.GetInterfaces()) {
				var map = type.GetInterfaceMap(iface);

				var genericInterfaceType = DegenerifyType(iface);
				var interfaceCode = Type.GetTypeCode(genericInterfaceType);

				if (!_interfaceToImplementationMap.ContainsKey(genericInterfaceType)) {
					_interfaceToImplementationMap.Add(genericInterfaceType, new TypeFriendlyDictionary<MethodBase, List<MethodBase>>());
				}

				for (var i = 0; i < map.InterfaceMethods.Length; i++) {
					var degenericSourceMethod = DegenerifyMethod(map.InterfaceMethods[i]);
					var degenericTargetMethod = DegenerifyMethod(map.TargetMethods[i]);

					if (!_interfaceToImplementationMap[genericInterfaceType].ContainsKey(degenericSourceMethod)) {
						_interfaceToImplementationMap[genericInterfaceType].Add(degenericSourceMethod, new List<MethodBase>());
					}
					_interfaceToImplementationMap[genericInterfaceType][degenericSourceMethod].Add(degenericTargetMethod);
				}
			}
		}
	}
}
