using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using HarmonyLib.Tools;
using MelonLoader;
using MonoMod.Cil;
using NCMS.Extensions;
using NeoModLoader.constants;
using NeoModLoader.services;
using NeoModLoader.utils;

namespace NeoModLoader.AndroidCompatibilityModule.TranspilerSupport;

/// <summary>
/// tells NML to not replace the IL2CPP function you are transpiling with a managed one
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class IgnoreTranspilerSupport : Attribute
{
}
/// <summary>
/// Stores the Managed Replacement of an IL2CPP method and its transpilers
/// </summary>
public class MirrorData
{
    internal MirrorData(Delegate method, List<MethodInfo> transpilers)
    {
        Method = method;
        Transpilers = new SortedList<MethodInfo>(HarmonyUtils.SortByPriority, transpilers);
    }
    private MirrorData() { }
    public readonly Delegate Method;
    internal readonly SortedList<MethodInfo> Transpilers;
    /// <summary>
    /// gets all transpilers applied to this Mirror
    /// </summary>
    public List<MethodInfo> GetTranspilers()
    {
        return Transpilers.GetList();
    }
}
/// <summary>
/// A utility which replaces IL2CPP methods with managed methods (from the PC version) for transpilers. generic methods (or methods apart of generic classes) are NOT SUPPORTED
/// </summary>
public static class TranspilerSupport
{
    private static MethodInfo il2cpp2managed = AccessTools.Method(typeof(TranspilerSupport), nameof(TranspilerSupport.IL2CPP2Managed));
     /// <summary>
     /// Invokes a Mirror by its IL2CPP function. throws an exception if doesnt exist
     /// </summary>
    public static object InvokeMirror(MethodBase original, object[] args)
    {
        MirrorData Data = MirroredAssemblies.GetMirror(original);
        if (Data == null)
        {
            throw new ArgumentException("The Method doesnt have a Mirror Method!");
        }
        return Data.Method.DynamicInvoke(args);
    }
    internal static void Initialize(Harmony harmony)
    {
        MirroredAssemblies.Init();
        MirroredAssemblies.Generator.Init();
        MirroredAssemblies.ManagedAssembly = MirroredAssemblies.LoadMirrorAssembly(Paths.PublicizedAssemblyPath);
        MirroredAssemblies.NativeAssembly = typeof(Actor).Assembly;
        harmony.Patch(
            AccessTools.Method(typeof(HarmonyManipulator), nameof(HarmonyManipulator.Manipulate), new []{typeof(MethodBase), typeof(PatchInfo), typeof(ILContext)}),
            new HarmonyMethod(typeof(TranspilerSupport), nameof(PatchPrefix))
        );
        var GetField = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Field),
            new[] { typeof(Type), typeof(string) });
        harmony.Patch(GetField, new HarmonyMethod(ManagedField));
    }
    static void ManagedField(ref Type type)
    {
        if (type.Assembly.FileName() == "Assembly-CSharp.dll")
        {
            type = MirroredAssemblies.RemapType(type, MirroredAssemblies.ManagedAssembly);
        }
    }
    /// <summary>
    ///  Generates a mirror method for your transpiler and replaces <see cref="transpiler"/> with the IL2CPP2Managed transpiler or null if already patched
    /// </summary>
    /// <param name="original">the method to transpile</param>
    /// <param name="transpiler">your transpiler</param>
    public static void CheckTranspiler(MethodBase original, ref MethodInfo transpiler)
    {
        if (transpiler?.DeclaringType == null) return;
        if (transpiler.GetCustomAttribute<IgnoreTranspilerSupport>() != null ||
            transpiler.DeclaringType.GetCustomAttribute<IgnoreTranspilerSupport>() != null)
        {
            return;
        }
        if (!MirroredAssemblies.Mirrors.TryGetValue(original, out MirrorData data))
        {
            data = MirroredAssemblies.Generator.GenerateMirror(original, [transpiler]);
            MirroredAssemblies.Mirrors.Add(original, data);
            transpiler = il2cpp2managed;
        }
        else
        {
            var transpilers = data.Transpilers;
            transpilers.Add(transpiler);
            MirroredAssemblies.Mirrors[original] = MirroredAssemblies.Generator.GenerateMirror(original, transpilers.GetList());
            transpiler = null;
        }
    }
    private static void PatchPrefix(MethodBase original, PatchInfo patchInfo)
    {
        if (original?.DeclaringType == null || !ShouldRedirect(original))
        {
            return;
        }
        if (patchInfo.transpilers == null || patchInfo.transpilers.Length == 0)
        {
            return;
        }
        for (int i = 0; i < patchInfo.transpilers.Length; i++)
        {
            MethodInfo transpiler = patchInfo.transpilers[i].PatchMethod;
            CheckTranspiler(original, ref transpiler);
            if (transpiler == null)
            {
                patchInfo.transpilers = patchInfo.transpilers.Remove(patchInfo.transpilers[i]);
            }
            else
                patchInfo.transpilers[i].PatchMethod = transpiler;
        }
    }
    private static bool ShouldRedirect(MethodBase method)
    {
        return method.DeclaringType.Assembly.FileName() == "Assembly-CSharp.dll";
    }
    [IgnoreTranspilerSupport]
    private static IEnumerable<CodeInstruction> IL2CPP2Managed(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
    {
        var code = new List<CodeInstruction>();

        var parameters = original.GetParameters();
        var paramTypes = parameters.Select(p => p.ParameterType).ToList();
        var returnType = original is MethodInfo mi ? mi.ReturnType : typeof(void);

// instance methods: arg0 = __instance
        if (!original.IsStatic)
            paramTypes.Insert(0, original.DeclaringType);

        var methodLocal = generator.DeclareLocal(typeof(MethodBase));
        var argsLocal = generator.DeclareLocal(typeof(object[]));

// MethodBase original
        code.Emit(OpCodes.Ldtoken, (MethodInfo)original);
        code.Emit(OpCodes.Call,
            typeof(MethodBase).GetMethod(
                nameof(MethodBase.GetMethodFromHandle),
                new[] { typeof(RuntimeMethodHandle) }));
        code.Emit(OpCodes.Stloc, methodLocal);

// create object[] args
        int argCount = paramTypes.Count;
        code.Emit(OpCodes.Ldc_I4, argCount);
        code.Emit(OpCodes.Newarr, typeof(object));

        for (int i = 0; i < argCount; i++)
        {
            code.Emit(OpCodes.Dup);
            code.Emit(OpCodes.Ldc_I4, i);
            code.Emit(OpCodes.Ldarg, i);

            if (paramTypes[i].IsValueType)
                code.Emit(OpCodes.Box, paramTypes[i]);

            code.Emit(OpCodes.Stelem_Ref);
        }

        code.Emit(OpCodes.Stloc, argsLocal);

// InvokeMirror(original, args)
        code.Emit(OpCodes.Ldloc, methodLocal);
        code.Emit(OpCodes.Ldloc, argsLocal);
        code.Emit(OpCodes.Call,
            AccessTools.Method(typeof(TranspilerSupport), nameof(TranspilerSupport.InvokeMirror)));

// handle return
        if (returnType != typeof(void))
        {
            if (returnType.IsValueType)
                code.Emit(OpCodes.Unbox_Any, returnType);
            else
                code.Emit(OpCodes.Castclass, returnType);
        }
        else
        {
            code.Emit(OpCodes.Pop);
        }

        code.Emit(OpCodes.Ret);

        return code;
    }
}
public class MirroredAssemblies : AssemblyLoadContext
{
    internal static Dictionary<MethodBase, MirrorData> Mirrors = new();
    /// <summary>
    /// gets the mirror of a method from assembly-csharp, null if not found
    /// </summary>
    public static MirrorData GetMirror(MethodBase Method)
    {
        return Mirrors.GetValueOrDefault(Method);
    }
    /// <summary>
    /// the assembly from the PC version
    /// </summary>
    public static Assembly ManagedAssembly { get; internal set; }
    /// <summary>
    /// the IL2CPP Assembly
    /// </summary>
    public static Assembly NativeAssembly { get; internal set; }
    public MirroredAssemblies() : base("MirrorContext", isCollectible: true) { }

    protected override Assembly Load(AssemblyName assemblyName) => null;
    private static MirroredAssemblies Instance;
    internal static void Init(){
        Instance = new  MirroredAssemblies();
    }
    public static Assembly LoadMirrorAssembly(string path)
    {
        return Instance.LoadFromAssemblyPath(Path.GetFullPath(path));
    }
    public static void Destroy()
    {
        Instance.Unload();
        Instance = null;
    }
    public static Type RemapType(Type type, Assembly targetAssembly, Type nativeDeclaringType = null)
    {
        if (type == null)
            return null;

        if (type.Assembly == targetAssembly)
            return type;

        if (type.IsByRef)
            return RemapType(type.GetElementType(), targetAssembly, nativeDeclaringType).MakeByRefType();

        if (type.IsPointer)
            return RemapType(type.GetElementType(), targetAssembly, nativeDeclaringType).MakePointerType();

        if (type.IsArray)
        {
            var element = RemapType(type.GetElementType(), targetAssembly, nativeDeclaringType);
            return type.GetArrayRank() == 1 ? element.MakeArrayType() : element.MakeArrayType(type.GetArrayRank());
        }

        if (type.IsGenericParameter)
        {
            Type result = type;
            // Generic parameter of the declaring type
            if (type.DeclaringType != null && nativeDeclaringType != null && nativeDeclaringType.IsGenericType)
            {
                var index = Array.IndexOf(type.DeclaringType.GetGenericArguments(), type);
                if (index >= 0)
                    result = nativeDeclaringType.GetGenericArguments()[index];
            }
            return result;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var remappedDef = RemapType(genericDef, targetAssembly, nativeDeclaringType);
            var args = type.GetGenericArguments()
                .Select(t => RemapType(t, targetAssembly, nativeDeclaringType))
                .ToArray();

            return remappedDef.MakeGenericType(args);
        }

        var remapped = targetAssembly.GetType(type.FullName);
        return remapped ?? type;
    }
    /// <summary>
    /// the Mirrored method generator which generates managed method substitutions
    /// </summary>
    public static class Generator
    {
        /// <summary>
        /// Temporary data used for generating mirror methods
        /// </summary>
        public struct GeneratorData
        {
            public List<CodeInstruction> Instructions;
            public ILGenerator Generator;
            public List<MethodInfo> Transpilers;
            public MethodInfo MirrorMethod;
            public MethodInfo OriginalMethod;
            public DynamicMethod Method;
            public Delegate Output;
        }
        internal static void Init()
        {
            Generators.Stages.Add(Generators.RemapOperands);
            Generators.Stages.Add(Generators.DeclareLocals);
            Generators.Stages.Add(Generators.InvokeTranspilers);
            Generators.Stages.Add(Generators.TransformFields);
            Generators.Stages.Add(Generators.EmitInstructions);
            Generators.Stages.Add(Generators.LogInfo);
            Generators.Stages.Add(Generators.ValidateMirror);
            Generators.Stages.Add(Generators.GenerateDelegate);
        }
        /// <summary>
        /// Generates a Managed mirror function to an IL2CPP function. this mirror contains the original IL from the PC version
        /// </summary>
        /// <param name="original">the original il2cpp function</param>
        /// <param name="transpilers">any transpilers to be applied</param>
        /// <returns>the new mirror method. can be invoked with <see cref="TranspilerSupport.InvokeMirror"/></returns>
        /// <exception cref="NotSupportedException">if you try to generate a mirror from a generic method or a method in a generic type</exception>
        /// <exception cref="MissingMethodException">if the class the method is in or the method does not exist on the PC version</exception>
        /// <exception cref="InvalidOperationException">if the generator fails to generate the mirror</exception>
        public static MirrorData GenerateMirror(MethodBase original, List<MethodInfo> transpilers = null)
        {
            if (original.IsGenericMethod || original.DeclaringType.IsGenericType || !(original is MethodInfo info))
            {
                throw new NotSupportedException("Constructors, Generic Methods or methods in generic types are not supported");
            }

            var mirrorType = ManagedAssembly.GetType(original.DeclaringType.FullName);
            if (mirrorType == null)
            {
                throw new MissingMethodException("The Managed type does not exist!");
            }

            var paramList = original.GetParameters()
                .Select(p => p.ParameterType)
                .ToList();
            var paramTypes = paramList.Select(t => RemapType(t, ManagedAssembly)).ToArray();
            if (!original.IsStatic)
                paramList.Insert(0, original.DeclaringType);
            var paramms = paramList.ToArray();

            MethodInfo mirrorMethod = mirrorType.GetMethod(
                original.Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                paramTypes,
                null
            );
            if (mirrorMethod == null)
            {
                throw new MissingMethodException("The Managed method does not exist!");
            }
            DynamicMethod mirror = new DynamicMethod(
                original.Name + "_managed",
                RemapType(mirrorMethod.ReturnType, NativeAssembly),
                paramms,
                typeof(TranspilerSupport),
                true
            );
            var generator = mirror.GetILGenerator();
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(mirrorMethod);
            GeneratorData Data = new GeneratorData();
            Data.OriginalMethod = info;
            Data.MirrorMethod = mirrorMethod;
            Data.Generator = generator;
            Data.Instructions = instructions;
            Data.Transpilers = transpilers;
            Data.Method = mirror;
            foreach (var Stage in Generators.Stages)
            {
                try
                {
                    Stage(Data);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Mirror Method Generator encountered an error at stage {Stage.Method.Name}, with Exception {e.Message} at {e.StackTrace}");
                }
            }
            return new MirrorData(Data.Output, transpilers);
        }
        /// <summary>
        /// class containing all default stages for generating mirror methods
        /// </summary>
        public static class Generators
        {
            /// <summary>
            /// a stage of mirror function generation
            /// </summary>
            public delegate void GeneratorStage(GeneratorData Data);
            public static List<GeneratorStage> Stages = new();
            public static void RemapOperands(GeneratorData Data)
            {
                foreach (var t in Data.Instructions)
                {
                    t.operand = RemapOperand(t.operand);
                }
            }
            public static void InvokeTranspilers(GeneratorData Data)
            {
                if (Data.Transpilers == null) return;
                foreach (var transpiler in Data.Transpilers)
                {
                    Data.Instructions = HarmonyUtils.InvokeTranspiler(transpiler, Data.Instructions, Data.Generator, Data.OriginalMethod)
                        .ToList();
                }
            }
            public static void DeclareLocals(GeneratorData Data)
            {
                var locals = Data.MirrorMethod.GetMethodBody()?.LocalVariables ?? [];
                var localMap = new Dictionary<int, LocalBuilder>();
                foreach (var local in locals)
                {
                    var newLocal = Data.Generator.DeclareLocal(RemapType(local.LocalType, NativeAssembly));
                    localMap[local.LocalIndex] = newLocal;
                }
                var labelMap = new Dictionary<Label, Label>();
                Label GetOrCreateLabel(Label old)
                {
                    if (!labelMap.TryGetValue(old, out var newLabel))
                    {
                        newLabel = Data.Generator.DefineLabel();
                        labelMap[old] = newLabel;
                    }
                    return newLabel;
                }
                foreach (var instr in Data.Instructions)
                {
                    foreach (var label in instr.labels)
                        Data.Generator.MarkLabel(GetOrCreateLabel(label));
                    instr.operand = instr.operand switch
                    {
                        LocalBuilder lb when localMap.TryGetValue(lb.LocalIndex, out var mappedLocal) => mappedLocal,
                        Label lbl => GetOrCreateLabel(lbl),
                        Label[] lbls => lbls.Select(GetOrCreateLabel).ToArray(),
                        _ => instr.operand
                    };
                }
            }
            public static void TransformFields(GeneratorData Data)
            {
                foreach (var instr in Data.Instructions)
                {
                    TransformField(ref instr.opcode, ref instr.operand);
                }
            }
            public static void EmitInstructions(GeneratorData Data)
            {
                foreach (var instr in Data.Instructions)
                {
                    Data.Generator.Emit(instr.opcode, instr.operand);
                }
            }
            public static void LogInfo(GeneratorData Data)
            {
                void L(string msg)
                {
                    MelonHelper.Log(msg);
                }
                if (!HarmonyFileLog.Enabled)
                {
                    LogService.LogInfo("Not logging debug mirror info");
                    return;
                }
                LogService.LogInfo("|--------Mirror Debug Data Dump--------|");
                L($"Original Method: {Data.OriginalMethod.GetInfo()}");
                foreach (var param in Data.OriginalMethod.GetParameters())
                {
                    L("param:" + param.GetInfo());
                }
                L("return type: " + Data.OriginalMethod.ReturnType.GetInfo());
                L($"Mirror Method: {Data.MirrorMethod.GetInfo()}");
                foreach (var param in Data.MirrorMethod.GetParameters())
                {
                    L("param:" + param.GetInfo());
                }
                L("return type: " + Data.MirrorMethod.ReturnType.GetInfo());
                L("|------Instructions-----|");
                int i = 0;
                foreach (var instr in PatchProcessor.GetOriginalInstructions(Data.MirrorMethod))
                {
                    L($"{i} : {instr.GetInfo()}");
                    i++;
                }
                L($"Dynamic Method: {Data.Method.GetInfo()}");
                foreach (var param in Data.Method.GetParameters())
                {
                    L("param:" + param.GetInfo());
                }
                L("|------Instructions-----|");
                i = 0;
                foreach (var instr in Data.Instructions)
                {
                    L($"{i} : {instr.GetInfo()}");
                    i++;
                }
                L("return type: " + Data.Method.ReturnType.GetInfo());
                LogService.LogInfo("|--------Mirror Debug Data Dump--------|");
            }
            /// <summary>
            /// Validates the Generated Mirror. throws an exception if invalid. also 
            /// </summary>
            /// <exception cref="InvalidDataException">if the mirror is invalid</exception>
            public static void ValidateMirror(GeneratorData Data)
            {
                bool CheckTypes(IEnumerable<Type> types)
                {
                    return types.All(type => CheckType(type));
                }
                bool CheckType(Type type)
                {
                    return type.Assembly != ManagedAssembly && CheckTypes(type.GetGenericArguments());
                }
                if (!CheckType(Data.Method.ReturnType) ||
                    !CheckTypes(Data.Method.GetParameters().Select(p => p.ParameterType)))
                {
                    throw new InvalidDataException(
                        $"Method {Data.Method.GetInfo()}  has invalid return type or parameters!");
                }
                int i = 0;
                foreach (var instruct in Data.Instructions)
                {
                    if (instruct.opcode == OpCodes.Call || instruct.opcode == OpCodes.Callvirt)
                    {
                        if (instruct.operand is MethodBase method)
                        {
                            if (!(CheckType(method.DeclaringType) && CheckTypes(method.GetParameters().Select(p => p.ParameterType))))
                            {
                                throw new InvalidDataException(
                                    $"Method {method.GetInfo()} at {i} has invalid declared type or parameters!");
                            }
                            if (method is MethodInfo info)
                            {
                                if (!CheckType(info.ReturnType))
                                {
                                    throw new InvalidDataException(
                                        $"Method {method.GetInfo()} at {i} has invalid return type!");
                                }
                            }
                        }
                        else
                        {
                            throw new InvalidDataException($"Method at {i} {instruct.GetInfo()} is invalid!");
                        }
                    }
                    else switch (instruct.operand)
                    {
                        case FieldInfo field when field.DeclaringType.Assembly == NativeAssembly ||
                                                  field.DeclaringType.Assembly == ManagedAssembly:
                            throw new InvalidDataException($"Invalid Field {field.GetInfo()} at {i}");
                        case MemberInfo info when CheckType(info.DeclaringType):
                            throw new InvalidDataException($"Invalid Member {info.GetInfo()} at {i}");
                    }
                    i++;
                }
            }
            /// <summary>
            /// Generates the outputed delegate.
            /// </summary>
            public static void GenerateDelegate(GeneratorData Data)
            {
                var dm = Data.Method;
                try
                {
                    var paramTypes = dm.GetParameters().Select(p => p.ParameterType).ToArray();
                    var returnType = dm.ReturnType;

                    var delegateType = returnType == typeof(void)
                        ? Expression.GetActionType(paramTypes)
                        : Expression.GetFuncType(paramTypes.Append(returnType).ToArray());

                    Data.Output = dm.CreateDelegate(delegateType);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Mirror is Invalid! {ex}");
                }
            }
        }
        /// <summary>
        /// Transforms a Field into a getter / setter method
        /// </summary>
        public static bool TransformField(ref OpCode opcode, ref object operand)
        {
            if (operand is not FieldInfo field)
                return false;
            var nativeDeclType = RemapType(field.DeclaringType, NativeAssembly);

            Type RemapOperandType(Type t) => RemapType(t, NativeAssembly, nativeDeclType);

            var fieldType = RemapOperandType(field.FieldType);

            MethodInfo method = null;

            if (opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld)
                method = AccessTools.Method(nativeDeclType, "get_" + field.Name);
            else if (opcode == OpCodes.Stfld || opcode == OpCodes.Stsfld)
                method = AccessTools.Method(nativeDeclType, "set_" + field.Name, new[] { fieldType });

            if (method == null)
                return false;

            operand = method;

            opcode = (opcode == OpCodes.Ldsfld || opcode == OpCodes.Stsfld) ? OpCodes.Call : OpCodes.Callvirt;

            return true;
        }
        private static object RemapOperand(object operand)
    {
        Type nativeDeclType;
        Type RemapOperandType(Type t) => RemapType(t, NativeAssembly, nativeDeclType);
        switch (operand)
        { 
            case MethodInfo m when m.DeclaringType?.Assembly == ManagedAssembly:
            {
                nativeDeclType = RemapType(m.DeclaringType, NativeAssembly);
                // Find method definition (ignore generic args)
                var candidates = nativeDeclType.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static);
                MethodInfo method = candidates.FirstOrDefault(x =>
                {
                    if (x.Name != m.Name)
                        return false;

                    var mp = x.GetParameters();
                    var op = m.GetParameters();

                    if (mp.Length != op.Length)
                        return false;

                    for (int i = 0; i < mp.Length; i++)
                    {
                        var t1 = RemapOperandType(op[i].ParameterType);
                        var t2 = mp[i].ParameterType;

                        // Compare generic definitions if necessary
                        if (t2.IsGenericParameter)
                            continue;

                        if (t1.IsGenericType && t2.IsGenericType)
                        {
                            if (t1.GetGenericTypeDefinition() != t2.GetGenericTypeDefinition())
                                return false;
                        }
                        else if (t1 != t2)
                            return false;
                    }

                    return true;
                });
                if (method == null)
                    throw new Exception($"Failed to remap method: {m}");
                // Reconstruct generic method
                if (m.IsGenericMethod)
                {
                    var genericArgs = m.GetGenericArguments()
                        .Select(RemapOperandType)
                        .ToArray();

                    method = method.MakeGenericMethod(genericArgs);
                }

                return method;
            }
            case ConstructorInfo c when c.DeclaringType?.Assembly == ManagedAssembly:
            {
                 nativeDeclType = RemapType(c.DeclaringType, NativeAssembly);
                var paramTypes = c.GetParameters()
                    .Select(p => RemapOperandType(p.ParameterType))
                    .ToArray();

                return AccessTools.Constructor(nativeDeclType, paramTypes);
            }
            case Type t when t.Assembly == ManagedAssembly:
                return RemapType(t, NativeAssembly);
            default:
                return operand;
        }
    }
    }
}