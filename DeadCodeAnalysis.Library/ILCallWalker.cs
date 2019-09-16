using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace DeadCodeAnalysis.Library
{
	internal class ILCallWalker
	{
		GraphTrackingResults _graph;
		Queue<MethodBase> _methodQueue = new Queue<MethodBase>();
		public ILCallWalker(GraphTrackingResults graph)
		{
			_graph = graph;
		}

		public Func<Type, List<MethodBase>> MarkMethodsAsImplicitlyCalled { get; set; }

		public List<MethodInfo> EntryPoints { get; set; } = new List<MethodInfo>();

		public List<Plugin> Plugins { get; } = new List<Plugin>();

		public void WalkMethod()
		{
			foreach (var entryPoint in EntryPoints) {
				_methodQueue.Enqueue(entryPoint);
			}

			while (_methodQueue.Count > 0) {
				var nextMethod = _methodQueue.Dequeue();
				WalkMethod(nextMethod);
			}
		}

		void WalkMethod(MethodBase methodBase)
		{
			var methodBaseName = methodBase.Name;
			var methodDeclaringClass = methodBase.DeclaringType?.Name;
			if (_graph.HasMethodBeenSeen(methodBase, EnqueueMethodsImplicitlyCalled)) {
				return;
			}

			if (methodBase.IsVirtual) {
				var implementationsToWalk = _graph.GetInterfaceImplementations(methodBase);
				foreach (var implementationToWalk in implementationsToWalk) {
					_methodQueue.Enqueue(implementationToWalk);
				}
			}

			if (methodBase.DeclaringType != null) {
				// all base types are implicitly seen (class BaseController : AbstractBaseController)
				var baseTypeChain = methodBase.DeclaringType;
				while (baseTypeChain != null) {
					MarkTypeSeen(baseTypeChain);
					baseTypeChain = baseTypeChain.BaseType;
				}

				// all containing types are implicitly seen ( class Nester { class Nestee { })
				var nestedTypeChain = methodBase.DeclaringType.DeclaringType;
				while (nestedTypeChain != null) {
					MarkTypeSeen(nestedTypeChain);
					nestedTypeChain = nestedTypeChain.DeclaringType;
				}
			}

			// register any return types as seen as well
			if (methodBase is MethodInfo mi) {
				foreach (var returnAttribute in mi.ReturnParameter.CustomAttributes) {
					MarkTypeSeen(returnAttribute.AttributeType);
				}
				MarkTypeSeen(mi.ReturnType);
			}

			// register all parameter types as seen as well
			foreach (var parameter in methodBase.GetParameters()) {
				foreach (var paramAttribute in parameter.CustomAttributes) {
					MarkTypeSeen(paramAttribute.AttributeType);
				}
				MarkTypeSeen(parameter.ParameterType);
			}

			// attributes on the method directly
			foreach (var methodAttribute in methodBase.CustomAttributes) {
				MarkTypeSeen(methodAttribute.AttributeType);
			}

			// call plugins
			foreach (var methodToEnqueue in Plugins.SelectMany(p => p.NewMethodSeen(methodBase))) {
				_methodQueue.Enqueue(methodToEnqueue);
			}

			ProcessIL(methodBase);
		}

		void ProcessIL(MethodBase methodBase)
		{
			var methodBaseName = methodBase.Name;
			var methodDeclaringClass = methodBase.DeclaringType?.Name;

			var methodBody = methodBase.GetMethodBody();
			if (methodBody == null) {
				return;
			}

			// do the fun bit with IL
			var msilBits = methodBody.GetILAsByteArray();

			int bytesToAdvance = 0;
			for (var currentPosition = 0; currentPosition < msilBits.Length; currentPosition += bytesToAdvance) {
				var opCode = PositionToOpCode(msilBits, currentPosition);
				var operandSize = GetOperandSizeFromOpCode(opCode, msilBits, currentPosition);

				if (opCode.OperandType == OperandType.InlineMethod) {
					var methodBeingCalled = GetMethodFromCurrentPosition(methodBase, opCode, msilBits, currentPosition);
					if (methodBeingCalled != null) {
						// can be null if we can't resolve the assembly
						_methodQueue.Enqueue(methodBeingCalled);
						HandleAsyncMethods(methodBeingCalled);
						HandleEnumerableMethods(methodBeingCalled);
						HandleGenericMethodParameters(methodBeingCalled);
					}
				} else if (opCode.OperandType == OperandType.InlineTok) {
					var token = GetTokenFromCurrentPosition(methodBase, opCode, msilBits, currentPosition);
					if (token is MethodBase) {
						_methodQueue.Enqueue(token as MethodBase);
					} else if (token is Type) {
						MarkTypeSeen(token as Type);
					} else if (token is FieldInfo fieldInfo) {
						MarkTypeSeen(fieldInfo.DeclaringType);
						foreach (var fieldAttrib in fieldInfo.CustomAttributes) {
							MarkTypeSeen(fieldAttrib.AttributeType);
						}
					} else if (token is PropertyInfo propInfo) {
						MarkTypeSeen(propInfo.DeclaringType);
						foreach (var propAttrib in propInfo.CustomAttributes) {
							MarkTypeSeen(propAttrib.AttributeType);
						}
					}
				} else if (opCode.OperandType == OperandType.InlineType) {
					var type = GetTypeFromCurrentPosition(methodBase, opCode, msilBits, currentPosition);
					MarkTypeSeen(type);
				} else if (opCode.OperandType == OperandType.InlineField) {
					var field = GetFieldFromCurrentPosition(methodBase, opCode, msilBits, currentPosition);
					if (field.DeclaringType != null) {
						MarkTypeSeen(field.DeclaringType);
					}
				}

				bytesToAdvance = opCode.Size + operandSize;
			}
		}

		void HandleGenericMethodParameters(MethodBase methodBeingCalled)
		{
			if (methodBeingCalled.IsGenericMethod) {
				foreach (var arg in methodBeingCalled.GetGenericArguments()) {
					MarkTypeSeen(arg);
				}
			}
		}
		

		void MarkTypeSeen(Type typeSeen)
		{
			if (typeSeen.IsArray) {
				typeSeen = typeSeen.GetElementType();
			}

			if (typeSeen.IsGenericType) {
				foreach (var genericParameterType in typeSeen.GetGenericArguments()) {
					MarkTypeSeen(genericParameterType);
				}
			}

			var hasNotBeenSeenBefore = _graph.MarkTypeSeen(typeSeen, EnqueueMethodsImplicitlyCalled);
			if (hasNotBeenSeenBefore) {
				// make sure to run any static ctors we find
				var staticCtor = typeSeen.GetConstructor(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				if (staticCtor != null) {
					_methodQueue.Enqueue(staticCtor);
				}

				// call plugins
				foreach (var methodToEnqueue in Plugins.SelectMany(p => p.NewTypeSeen(typeSeen))) {
					_methodQueue.Enqueue(methodToEnqueue);
				}

				// attributes on the method directly
				foreach (var typeAttribute in typeSeen.CustomAttributes) {
					MarkTypeSeen(typeAttribute.AttributeType);
				}

				foreach (var iface in typeSeen.GetInterfaces()) {
					MarkTypeSeen(iface);

					if (iface.IsGenericType) {
						foreach (var arg in iface.GetGenericArguments()) {
							MarkTypeSeen(arg);
						}
					}
				}
			}
		}

		void EnqueueMethodsImplicitlyCalled(Type typeSeen)
		{
			foreach (var methodToEnqueue in MarkMethodsAsImplicitlyCalled?.Invoke(typeSeen) ?? Enumerable.Empty<MethodBase>()) {
				_methodQueue.Enqueue(methodToEnqueue);
			}
		}

		void HandleAsyncMethods(MethodBase methodBeingCalled)
		{
			if (methodBeingCalled.Name == nameof(AsyncTaskMethodBuilder.Start) &&
										methodBeingCalled.IsGenericMethod &&
										((methodBeingCalled.DeclaringType.IsGenericType && typeof(AsyncTaskMethodBuilder<>).IsAssignableFrom(methodBeingCalled.DeclaringType.GetGenericTypeDefinition())
										|| methodBeingCalled.DeclaringType.IsAssignableFrom(typeof(AsyncTaskMethodBuilder))))) {

				var asyncStart = methodBeingCalled.GetGenericArguments()[0].GetMethod(nameof(IAsyncStateMachine.MoveNext), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_methodQueue.Enqueue(asyncStart);
			}
		}

		void HandleEnumerableMethods(MethodBase methodBeingCalled)
		{
			if (methodBeingCalled.IsConstructor
				&& methodBeingCalled.DeclaringType.Name.StartsWith("<")
				&& typeof(System.Collections.IEnumerator).IsAssignableFrom(methodBeingCalled.DeclaringType)) {

				var enumerableStart = methodBeingCalled.DeclaringType.GetMethod(nameof(System.Collections.IEnumerator.MoveNext), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				_methodQueue.Enqueue(enumerableStart);
			}
		}

		MemberInfo GetTokenFromCurrentPosition(MethodBase methodBase, OpCode opCode, byte[] msilBits, int currentPosition)
		{
			var operandStart = currentPosition + opCode.Size;

			var memberToken = msilBits[operandStart]
				| msilBits[operandStart + 1] << 8
				| msilBits[operandStart + 2] << 16
				| msilBits[operandStart + 3] << 24;

			var methodGenericTypes = methodBase.IsGenericMethod ? methodBase.GetGenericArguments() : null;
			var typeGenericTypes = methodBase.DeclaringType.IsGenericType ? methodBase.DeclaringType.GetGenericArguments() : null;

			var resolvedMember = methodBase.Module.ResolveMember(memberToken, typeGenericTypes, methodGenericTypes);

			return resolvedMember;
		}

		static MethodBase GetMethodFromCurrentPosition(MethodBase methodBase, OpCode opCode, byte[] msilBits, int currentPosition)
		{
			var operandStart = currentPosition + opCode.Size;

			var methodToken = msilBits[operandStart]
				| msilBits[operandStart + 1] << 8
				| msilBits[operandStart + 2] << 16
				| msilBits[operandStart + 3] << 24;

			var methodGenericTypes = methodBase.IsGenericMethod ? methodBase.GetGenericArguments() : null;
			var typeGenericTypes = methodBase.DeclaringType.IsGenericType ? methodBase.DeclaringType.GetGenericArguments() : null;

			try {
				var resolvedMethod = methodBase.Module.ResolveMethod(methodToken, typeGenericTypes, methodGenericTypes);

				return resolvedMethod;
			} catch {
				return null;
			}
		}

		static FieldInfo GetFieldFromCurrentPosition(MethodBase methodBase, OpCode opCode, byte[] msilBits, int currentPosition)
		{
			var operandStart = currentPosition + opCode.Size;

			var fieldToken = msilBits[operandStart]
				| msilBits[operandStart + 1] << 8
				| msilBits[operandStart + 2] << 16
				| msilBits[operandStart + 3] << 24;

			var methodGenericTypes = methodBase.IsGenericMethod ? methodBase.GetGenericArguments() : null;
			var typeGenericTypes = methodBase.DeclaringType.IsGenericType ? methodBase.DeclaringType.GetGenericArguments() : null;

			try {
				var resolvedField = methodBase.Module.ResolveField(fieldToken, typeGenericTypes, methodGenericTypes);

				return resolvedField;
			} catch {
				return null;
			}
		}

		static Type GetTypeFromCurrentPosition(MethodBase methodBase, OpCode opCode, byte[] msilBits, int currentPosition)
		{
			var operandStart = currentPosition + opCode.Size;

			var typeToken = msilBits[operandStart]
				| msilBits[operandStart + 1] << 8
				| msilBits[operandStart + 2] << 16
				| msilBits[operandStart + 3] << 24;

			var methodGenericTypes = methodBase.IsGenericMethod ? methodBase.GetGenericArguments() : null;
			var typeGenericTypes = methodBase.DeclaringType.IsGenericType ? methodBase.DeclaringType.GetGenericArguments() : null;

			try {
				var resolvedType = methodBase.Module.ResolveType(typeToken, typeGenericTypes, methodGenericTypes);

				return resolvedType;
			} catch {
				return null;
			}
		}

		static int GetOperandSizeFromOpCode(OpCode opCode, byte[] msilBits, int currentPosition)
		{
			if (OpCodes.TakesSingleByteArgument(opCode)) {
				return 1;
			}

			switch (opCode.OperandType) {
				case OperandType.InlineSwitch:
					var jumpAddresses = BitConverter.ToInt32(msilBits, currentPosition + opCode.Size);
					return 4 + jumpAddresses * 4;

				case OperandType.InlineI8:
				case OperandType.InlineR:
					return 8;

				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
				case OperandType.InlineString:
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.ShortInlineR:
				case OperandType.InlineSig:
					return 4;

				case OperandType.InlineVar:
					return 2;

				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					return 1;

				case OperandType.InlineNone:
					return 0;

				default:
					throw new NotImplementedException();
			}
		}

		static readonly IEnumerable<IGrouping<int, OpCode>> _opCodesMaster = typeof(OpCodes)
			.GetFields(BindingFlags.Static | BindingFlags.Public)
			.Where(f => f.FieldType == typeof(OpCode))
			.Select(f => (OpCode)f.GetValue(null))
			.GroupBy(f => f.Size);

		static readonly SortedDictionary<ushort, OpCode> _opCodesSingle = new SortedDictionary<ushort, OpCode>(
			_opCodesMaster
			.Where(g => g.Key == 1)
			.SelectMany(g => g)
			.ToDictionary(k => (ushort)k.Value, v => v));

		static readonly SortedDictionary<ushort, OpCode> _opCodesDouble = new SortedDictionary<ushort, OpCode>(
			_opCodesMaster
			.Where(g => g.Key == 2)
			.SelectMany(g => g)
			.ToDictionary(k => (ushort)k.Value, v => v));


		static OpCode PositionToOpCode(byte[] msilBytes, int currentPosition)
		{
			if (msilBytes[currentPosition] <= 253) {
				return _opCodesSingle[(ushort)msilBytes[currentPosition]];
			} else {
				return _opCodesDouble[(ushort)(msilBytes[currentPosition] << 8 | (msilBytes[currentPosition + 1]))];
			}
		}
	}
}
