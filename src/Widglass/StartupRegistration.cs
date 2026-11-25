using Microsoft.Win32;

namespace Widglass;

/// <summary>
/// "Start with Windows": an entry in the current user's Run key that opens this .exe
/// with <c>--background</c> at sign-in, so only the widgets and the tray icon appear.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Widglass";
    private const string OldValueName = "ProjetoWidget";
    public const string BackgroundArg = "--background";

    public static void Apply(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.DeleteValue(OldValueName, throwOnMissingValue: false); // entry from before the rename
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
