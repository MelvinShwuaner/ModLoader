using HarmonyLib;
using NeoModLoader.constants;

namespace NeoModLoader.AndroidCompatibilityModule;
#if IL2CPP
using MelonLoader.Utils;
using MelonLoader;
#endif
public static class MelonHelper
{
    #if IL2CPP
    public static string GetPath()
    {
        return MelonEnvironment.GameRootDirectory;
    }

    internal static void Init()
    {
        Log("Initializing android support module");
        TranspilerSupport.TranspilerSupport.Initialize(new Harmony(Others.harmony_id));
    }
    /// <summary>
     /// Reads a file in the apk assets directory
     /// </summary>
     /// <param name="assetPath">the path to the file from the assets folder in the apk</param>
     /// <returns>the file bytes, or null if not found</returns>
    public static byte[] ReadAPKAsset(string assetPath)
    {
        APKAssetManager.Initialize();
        if (!APKAssetManager.DoesAssetExist(assetPath))
        {
            MelonLogger.Warning($"File not found in APK assets: {assetPath}");
            return null;
        }
        var assetBytes = APKAssetManager.GetAssetBytes(assetPath);
        return assetBytes is { Length: > 0 } ? assetBytes : null;
    }

    public static void Log(string msg)
    {
        UnityEngine.Debug.Log(msg);
        MelonLogger.Msg(msg);
    }
    public static void LogError(string msg)
    {
        UnityEngine.Debug.LogError(msg);
        MelonLogger.Error(msg);
    }
    public static void LogWarning(string msg)
    {
        UnityEngine.Debug.LogWarning(msg);
        MelonLogger.Warning(msg);
    }
    #else
       public static string GetPath()
    {
       return "";
    }
     public static void Log(string msg)
    {
      UnityEngine.Debug.Log(msg);
    }
    public static void LogError(string msg)
    {
       UnityEngine.Debug.LogError(msg);
    }
    public static void LogWarning(string msg)
    {
        UnityEngine.Debug.LogWarning(msg);
    }
    public static byte[] ReadAPKAsset(string assetPath){
        throw new PlatformNotSupportedException("How did we get here?");
    }
    public static void Init(){}
    #endif
}