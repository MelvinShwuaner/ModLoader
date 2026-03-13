using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NeoModLoader.utils.instpredictors;

namespace NeoModLoader.utils;

/// <summary>
///     Utility class for Harmony Transpiler
/// </summary>
public static class HarmonyUtils
{
    /// <summary>
    ///     Find a code snippet in a list of instructions
    /// </summary>
    /// <param name="pCodes"></param>
    /// <param name="pResult">The code snippet found</param>
    /// <param name="pSnippetPredictors"></param>
    /// <returns>Index of the start of the code snippet in <paramref name="pCodes" /></returns>
    public static int FindCodeSnippet(List<CodeInstruction>      pCodes, out List<CodeInstruction> pResult,
                                      params BaseInstPredictor[] pSnippetPredictors)
    {
        for (var i = 0; i < pCodes.Count - pSnippetPredictors.Length; i++)
            if (!pSnippetPredictors.Where((t, j) => !t.Predict(pCodes[i + j])).Any())
            {
                pResult = pCodes.GetRange(i, pSnippetPredictors.Length);
                return i;
            }

        pResult = null;
        return -1;
    }

    /// <summary>
    /// </summary>
    /// <param name="pCodes"></param>
    /// <param name="pSnippetPredictors"></param>
    /// <returns>Index of the start of the code snippet in <paramref name="pCodes" /></returns>
    public static int FindCodeSnippetIdx(List<CodeInstruction> pCodes, params BaseInstPredictor[] pSnippetPredictors)
    {
        for (var i = 0; i < pCodes.Count - pSnippetPredictors.Length; i++)
            if (!pSnippetPredictors.Where((t, j) => !t.Predict(pCodes[i + j])).Any())
                return i;
        return -1;
    }

    /// <summary>
    /// </summary>
    /// <param name="pCodes"></param>
    /// <param name="pPredictor"></param>
    /// <returns>First of expected code instruction</returns>
    public static CodeInstruction FindInst(List<CodeInstruction> pCodes, BaseInstPredictor pPredictor)
    {
        return pCodes.FirstOrDefault(pPredictor.Predict);
    }

    /// <summary>
    /// </summary>
    /// <param name="pCodes"></param>
    /// <param name="pPredictor"></param>
    /// <typeparam name="TOperand"></typeparam>
    /// <returns></returns>
    public static TOperand FindInstOperand<TOperand>(List<CodeInstruction> pCodes, BaseInstPredictor pPredictor)
    {
        CodeInstruction inst = FindInst(pCodes, pPredictor);
        if (inst == null) return default;
        return inst.operand is TOperand operand ? operand : default;
    }

    /// <summary>
    /// </summary>
    /// <param name="pCodes"></param>
    /// <param name="pPredictor"></param>
    /// <typeparam name="TOperand"></typeparam>
    /// <returns>Index of the first of expected code instruction</returns>
    public static int FindInstIdx<TOperand>(List<CodeInstruction> pCodes, BaseInstPredictor pPredictor)
    {
        for (var i = 0; i < pCodes.Count; i++)
            if (pPredictor.Predict(pCodes[i]))
                return i;

        return -1;
    }

    internal static void _init()
    {
        BaseInstPredictor._init();
    }

    public static void Emit(this List<CodeInstruction> list, OpCode opcode, object operand = null)
    {
        list.Add(new CodeInstruction(opcode, operand));
    }
    /// <summary>
    /// Emits an instruction depending on the type of the operand
    /// </summary>
    public static void Emit(this ILGenerator il, OpCode opcode, object operand)
    {
        switch (operand)
        {
            case null:
                il.Emit(opcode);
                break;
            case int i:
                il.Emit(opcode, i);
                break;
            case long l:
                il.Emit(opcode, l);
                break;
            case float f:
                il.Emit(opcode, f);
                break;
            case double d:
                il.Emit(opcode, d);
                break;
            case string s:
                il.Emit(opcode, s);
                break;
            case byte b:
                il.Emit(opcode, b);
                break;
            case sbyte sb:
                il.Emit(opcode, sb);
                break;
            case MethodInfo m:
                il.Emit(opcode, m);
                break;
            case ConstructorInfo c:
                il.Emit(opcode, c);
                break;
            case FieldInfo fi:
                il.Emit(opcode, fi);
                break;
            case Type t:
                il.Emit(opcode, t);
                break;
            case Label lbl:
                il.Emit(opcode, lbl);
                break;
            case Label[] lbls:
                il.Emit(opcode, lbls);
                break;
            case LocalBuilder local:
                il.Emit(opcode, local);
                break;
            case SignatureHelper sig:
                il.Emit(opcode, sig);
                break;
            default:
                throw new NotSupportedException($"Unsupported operand type: {operand.GetType()}");
        }
    }
    /// <summary>
    /// Invokes a Prefix method. returns a object if the prefix replaces, returns null if not
    /// </summary>
    /// <param name="Prefix">the prefix</param>
    /// <param name="args">the arguments, including __instance. __result is SEPERATE</param>
    /// <param name="Replaced">returns if the prefix returns false or not</param>
    /// <returns></returns>
    public static object InvokePrefix(MethodInfo Prefix, ref object[] args, out bool Replaced)
    {
        Replaced = false;
        object __result = null;

        var parameters = Prefix.GetParameters();
        var invokeArgs = new object[parameters.Length];
        int argsIndex = 0; 

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.Name == "__result")
            {
                invokeArgs[i] = null;
            }
            else
            {
                invokeArgs[i] = argsIndex < args.Length ? args[argsIndex] : null;
                argsIndex++;
            }
        }

        object invokeResult = Prefix.Invoke(null, invokeArgs);
        
        argsIndex = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.Name == "__result")
            {
                if (param.ParameterType.IsByRef)
                    __result = invokeArgs[i];
            }
            else
            {
                if (param.ParameterType.IsByRef && argsIndex < args.Length)
                    args[argsIndex] = invokeArgs[i];
                argsIndex++;
            }
        }
        if (Prefix.ReturnType == typeof(bool) && invokeResult is bool continueExecution)
        {
            Replaced = !continueExecution;
        }
        return __result;
    }
    /// <summary>
    /// Invokes a Transpiler method. returns the outputed instructions
    /// </summary>
    /// <param name="Transpiler">the transpiler</param>
    /// <param name="instructions">the original instructions</param>
    /// <param name="generator">the IL generator</param>
    /// <param name="original">the original method, that this transpiler is patching</param>
    /// <returns></returns>
    public static IEnumerable<CodeInstruction> InvokeTranspiler(MethodInfo Transpiler, IEnumerable<CodeInstruction> instructions, ILGenerator generator = null, MethodBase original = null)
    {
        var transpilerParams = Transpiler.GetParameters();
        var args = transpilerParams.Select(object (p) =>
        {
            if (p.ParameterType == typeof(IEnumerable<CodeInstruction>))
                return instructions;
            if (p.ParameterType == typeof(ILGenerator))
                return generator;
            return p.ParameterType == typeof(MethodBase) ? original : null;
        }).ToArray();
        return (IEnumerable<CodeInstruction>)Transpiler.Invoke(null, args);
    }
    public static readonly Comparison<MethodInfo> SortByPriority = (method1, method2) => method1.GetPriority().CompareTo(method2.GetPriority());
    public static int GetPriority(this MethodInfo Method)
    {
        var priority = Method.GetCustomAttribute<HarmonyPriority>() ?? Method.DeclaringType?.GetCustomAttribute<HarmonyPriority>();
        return priority == null ? Priority.Normal : priority.info.priority;
    }

    
}