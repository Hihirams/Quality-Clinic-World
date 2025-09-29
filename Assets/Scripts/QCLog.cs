// Assets/Scripts/QCLog.cs
using System.Diagnostics;

public static class QCLog
{
    [Conditional("QC_VERBOSE")] public static void Info(object msg)  => UnityEngine.Debug.Log(msg);
    [Conditional("QC_VERBOSE")] public static void Warn(object msg)  => UnityEngine.Debug.LogWarning(msg);
    [Conditional("QC_VERBOSE")] public static void Error(object msg) => UnityEngine.Debug.LogError(msg);
}
