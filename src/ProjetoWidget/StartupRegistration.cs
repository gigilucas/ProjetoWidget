using Microsoft.Win32;

namespace ProjetoWidget;

/// <summary>
/// "Start with Windows": an entry in the current user's Run key that opens this .exe
/// with <c>--background</c> at sign-in, so only the widgets and the tray icon appear.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ProjetoWidget";
    public const string BackgroundArg = "--background";

    public static void Apply(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\" {BackgroundArg}");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Not being able to change it is not fatal.
        }
    }
}
