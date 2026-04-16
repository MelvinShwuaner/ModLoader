using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using MonoMod.Cil;
using NeoModLoader.constants;
using NeoModLoader.services;
using NeoModLoader.utils;
using NeoModLoader.utils.Lists;
namespace NeoModLoader.AndroidCompatibilityModule.TranspilerSupport;

/// <summary>
/// this is a stub
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

    private MirrorData()
    {
    }

    public readonly Delegate Method;
    internal readonly SortedList<MethodInfo> Transpilers;

    /// <summary>
    /// gets all transpilers applied to this Mirror
    /// </summary>
    public List<MethodInfo> GetTranspilers()
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }
}

/// <summary>
/// A utility which replaces IL2CPP methods with managed methods (from the PC version) for transpilers. generic methods (or methods apart of generic classes) are NOT SUPPORTED
/// </summary>
public static class TranspilerSupport
{
    /// <summary>
    /// this is a stub
    /// </summary>
    public static bool DEBUG = false;

    /// <summary>
    /// this is a stub
    /// </summary>
    public static readonly MethodInfo il2cpp2managed =
        null;

    /// <summary>
    /// this is a stub
    /// </summary>
    public static object InvokeMirror(MethodBase original, object[] args)
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }

    /// <summary>
    /// this is a stub
    /// </summary>
    public static void CheckTranspiler(MethodBase original, ref MethodInfo transpiler)
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }
}

public class MirroredAssemblies
{
    /// <summary>
    /// this is a stub
    /// </summary>
    public static MirrorData GetMirror(MethodBase Method)
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }
    
    public static Assembly ManagedAssembly { get; internal set; }

    public static Assembly NativeAssembly { get; internal set; }

    public static Assembly LoadMirrorAssembly(string path)
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }

    public static void Destroy()
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }

    public static Type RemapType(Type type, Assembly targetAssembly, Type nativeDeclaringType = null)
    {
        throw new PlatformNotSupportedException("this is PC bro");
    }
    public static class Generator
    {
        public class GeneratorData
        {
            public List<CodeInstruction> Instructions;
            public ILGenerator Generator;
            public List<MethodInfo> Transpilers;
            public MethodInfo MirrorMethod;
            public MethodInfo OriginalMethod;
            public DynamicMethod Method;
        }

        /// <summary>
        /// Stub method
        /// </summary>
        public static MirrorData GenerateMirror(MethodBase original, List<MethodInfo> transpilers = null,
            List<Generators.GeneratorStage> Stages = null)
        {
            throw new PlatformNotSupportedException("this is PC bro");
        }

        /// <summary>
        /// Generates a delegate with a dynamic method
        /// </summary>
        /// <exception cref="InvalidDataException">if the IL code is invalid</exception>
        public static Delegate GenerateDelegate(DynamicMethod dm)
        {
            throw new PlatformNotSupportedException("this is PC bro");
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
                throw new PlatformNotSupportedException("this is PC bro");
            }

            public static void InvokeTranspilers(GeneratorData Data)
            {
                throw new PlatformNotSupportedException("this is PC bro");
            }

            public static void EmitInstructions(GeneratorData Data)
            {
                throw new PlatformNotSupportedException("this is PC bro");
            }

            public static void TransformFields(GeneratorData Data)
            {
                throw new PlatformNotSupportedException("this is PC bro");
            }

            public static void LogInfo(GeneratorData Data)
            {
                throw new PlatformNotSupportedException("this is PC bro");
            }
        }

        /// <summary>
        /// Transforms a Field into a getter / setter method
        /// </summary>
        public static bool TransformField(ref OpCode opcode, ref object operand)
        {
            throw new PlatformNotSupportedException("this is PC bro");
        }
    }
}