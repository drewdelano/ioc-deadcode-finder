using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        public List<MethodInfo> EntryPoints { get; set; } = new List<MethodInfo>();

        public void WalkMethod()
        {
            foreach (var entryPoint in EntryPoints)
            {
                _methodQueue.Enqueue(entryPoint);
            }

            while (_methodQueue.Count > 0)
            {
                var nextMethod = _methodQueue.Dequeue();
                WalkMethod(nextMethod);
            }
        }

        void WalkMethod(MethodBase methodBase)
        {
            if (_graph.HasMethodBeenSeen(methodBase))
            {
                return;
            }

            if (methodBase.IsVirtual)
            {
                var implementationsToWalk = _graph.GetInterfaceImplementations(methodBase);
                foreach (var implementationToWalk in implementationsToWalk)
                {
                    _methodQueue.Enqueue(implementationToWalk);
                }
            }

            if (methodBase.IsAbstract)
            {
                return;
            }

            var msilBits = methodBase.GetMethodBody().GetILAsByteArray();

            int bytesToAdvance = 0;
            for (var currentPosition = 0; currentPosition < msilBits.Length; currentPosition += bytesToAdvance)
            {
                var opCode = PositionToOpCode(msilBits, currentPosition);
                var operandSize = GetOperandSizeFromOpCode(opCode);

                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    var methodBeingCalled = GetMethodFromCurrentPosition(methodBase, opCode, msilBits, currentPosition);
                    _methodQueue.Enqueue(methodBeingCalled);
                }

                bytesToAdvance = opCode.Size + operandSize;
            }
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

            return methodBase.Module.ResolveMethod(methodToken, typeGenericTypes, methodGenericTypes);
        }

        static int GetOperandSizeFromOpCode(OpCode opCode)
        {
            switch (opCode.OperandType)
            {
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;

                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineSwitch:
                case OperandType.InlineTok:
                case OperandType.ShortInlineR:
                case OperandType.InlineType:
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

        static readonly SortedDictionary<ushort, OpCode> _opCodes = new SortedDictionary<ushort, OpCode>(typeof(OpCodes)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null))
            .OrderByDescending(f => f.Size)
            .ToDictionary(k => (ushort)k.Value, v => v));


        static OpCode PositionToOpCode(byte[] msilBytes, int currentPosition)
        {
            if (msilBytes[currentPosition] < 254)
            {
                return _opCodes[(ushort)msilBytes[currentPosition]];
            }
            else
            {
                return _opCodes[(ushort)(msilBytes[currentPosition] | (msilBytes[currentPosition + 1] >> 8))];
            }
        }
    }
}
