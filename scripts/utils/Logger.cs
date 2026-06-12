using Godot;

public static class Logger
{
    public static void Info(string message)
    {
        GD.Print($"[INFO] {message}");
    }

    public static void Warn(string message)
    {
        GD.PushWarning($"[WARN] {message}");
    }

    public static void Error(string message)
    {
        GD.PushError($"[ERROR] {message}");
    }
}
