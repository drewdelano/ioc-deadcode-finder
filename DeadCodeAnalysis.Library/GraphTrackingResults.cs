using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DeadCodeAnalysis.Library
{
    public class GraphTrackingResults
    {
        Dictionary<Type, Dictionary<MethodBase, List<MethodBase>>> _interfaceToImplementationMap = new Dictionary<Type, Dictionary<MethodBase, List<MethodBase>>>();
        Dictionary<Type, Dictionary<MethodBase, bool>> _graph = new Dictionary<Type, Dictionary<MethodBase, bool>>();

        public List<Type> UnusedClasses { get; private set; }
        public Dictionary<Type, List<MethodBase>> UnusedMethods { get; private set; }

        internal void BuildReport()
        {
            UnusedClasses = new List<Type>();
            UnusedMethods = new Dictionary<Type, List<MethodBase>>();

            foreach (var graphEntry in _graph)
            {
                if (graphEntry.Key.Name.StartsWith("<"))
                {
                    continue;
                }

                var unusedMethods = graphEntry.Value.Where(ge => ge.Value == false).Select(ge => ge.Key).ToList();
                if (unusedMethods.Count == graphEntry.Value.Count)
                {
                    UnusedClasses.Add(graphEntry.Key);
                }
                else if (unusedMethods.Count > 0)
                {
                    UnusedMethods.Add(graphEntry.Key, unusedMethods);
                }
            }
        }

        internal bool HasMethodBeenSeen(MethodBase methodBase)
        {
            if (!_graph.ContainsKey(methodBase.DeclaringType))
            {
                return true;
            }
            if (!_graph[methodBase.DeclaringType].ContainsKey(methodBase))
            {
                return true;
            }
            if (_graph[methodBase.DeclaringType][methodBase])
            {
                return true;
            }
            _graph[methodBase.DeclaringType][methodBase] = true;
            return false;
        }

        internal List<MethodBase> GetInterfaceImplementations(MethodBase methodBase)
        {
            if (!_interfaceToImplementationMap.ContainsKey(methodBase.DeclaringType))
            {
                return new List<MethodBase>();
            }
            if (!_interfaceToImplementationMap[methodBase.DeclaringType].ContainsKey(methodBase))
            {
                return new List<MethodBase>();
            }
            return _interfaceToImplementationMap[methodBase.DeclaringType][methodBase];
        }

        internal void GenerateMethodGraph(List<Assembly> assembliesToAnalyze)
        {
            _graph = new Dictionary<Type, Dictionary<MethodBase, bool>>();
            foreach (var assembly in assembliesToAnalyze)
            {
                foreach (var type in assembly.GetTypes())
                {
                    TrackInterfaceImplementations(type);
                    TrackMethodUsage(type);
                }
            }
        }

        void TrackMethodUsage(Type type)
        {
            if (!_graph.ContainsKey(type))
            {
                _graph.Add(type, new Dictionary<MethodBase, bool>());
            }
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                _graph[type].Add(method, false);
            }
        }

        void TrackInterfaceImplementations(Type type)
        {
            foreach (var iface in type.GetInterfaces())
            {
                var map = type.GetInterfaceMap(iface);

                var genericInterfaceType = iface.IsGenericType ? iface.GetGenericTypeDefinition() : iface;
                if (!_interfaceToImplementationMap.ContainsKey(genericInterfaceType))
                {
                    _interfaceToImplementationMap.Add(genericInterfaceType, new Dictionary<MethodBase, List<MethodBase>>());
                }

                for (var i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    if (!_interfaceToImplementationMap[genericInterfaceType].ContainsKey(map.InterfaceMethods[i]))
                    {
                        _interfaceToImplementationMap[genericInterfaceType].Add(map.InterfaceMethods[i], new List<MethodBase>());
                    }

                    _interfaceToImplementationMap[genericInterfaceType][map.InterfaceMethods[i]].Add(map.TargetMethods[i]);
                }
            }
        }
    }
}
