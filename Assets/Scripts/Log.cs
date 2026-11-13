using UnityEngine;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4
}

public static class Log
{
    // Configure default minimum log levels for each channel
    public static readonly HybridLogger Mqtt = new HybridLogger("MQTT", LogLevel.Warning); // Only warnings/errors by default
    public static readonly HybridLogger Vehicle = new HybridLogger("VEHICLE", LogLevel.Info);
    public static readonly HybridLogger System = new HybridLogger("SYSTEM", LogLevel.Debug);
}

public class HybridLogger
{
    private readonly string _prefix;
    
    // The minimum severity required for a log to output
    public LogLevel MinLevel { get; set; }

    public HybridLogger(string prefix, LogLevel defaultLevel)
    {
        _prefix = $"<b>[{prefix}]</b>";
        MinLevel = defaultLevel;
    }

    private bool IsLevelEnabled(LogLevel level)
    {
        return level >= MinLevel;
    }

    // ==========================================
    // 1. DEBUG LOGS (Lowest Severity)
    // ==========================================
    public void LogDebug(System.Func<string> messageFactory, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Debug)) return;
        Debug.Log($"{_prefix} <color=#7f8c8d>[DEBUG]</color> {messageFactory()}", context);
    }

    public void LogDebug(string staticMessage, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Debug)) return;
        Debug.Log($"{_prefix} <color=#7f8c8d>[DEBUG]</color> {staticMessage}", context);
    }

    // ==========================================
    // 2. INFO LOGS
    // ==========================================
    public void LogInfo(System.Func<string> messageFactory, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Info)) return;
        Debug.Log($"{_prefix} <color=#3498db>[INFO]</color> {messageFactory()}", context);
    }

    public void LogInfo(string staticMessage, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Info)) return;
        Debug.Log($"{_prefix} <color=#3498db>[INFO]</color> {staticMessage}", context);
    }

    // ==========================================
    // 3. WARNING LOGS
    // ==========================================
    public void LogWarning(System.Func<string> messageFactory, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Warning)) return;
        Debug.LogWarning($"{_prefix} {messageFactory()}", context);
    }

    public void LogWarning(string staticMessage, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Warning)) return;
        Debug.LogWarning($"{_prefix} {staticMessage}", context);
    }

    // ==========================================
    // 4. ERROR LOGS (Highest Severity)
    // ==========================================
    public void LogError(System.Func<string> messageFactory, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Error)) return;
        Debug.LogError($"{_prefix} {messageFactory()}", context);
    }

    public void LogError(string staticMessage, Object context = null)
    {
        if (!IsLevelEnabled(LogLevel.Error)) return;
        Debug.LogError($"{_prefix} {staticMessage}", context);
    }
}