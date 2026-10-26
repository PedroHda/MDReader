using Microsoft.Win32;

namespace MDReader.Services;

public static class WindowsFileIntegration
{
    public static void RegisterOpenWith()
    {
        using var application = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\MDReader.exe");
        application.SetValue("FriendlyAppName", "MDReader");
        using (var command = application.CreateSubKey(@"shell\open\command"))
            command.SetValue(string.Empty, $"\"{Application.ExecutablePath}\" \"%1\"");
        using (var supported = application.CreateSubKey("SupportedTypes"))
        {
            supported.SetValue(".md", string.Empty);
            supported.SetValue(".markdown", string.Empty);
        }
        using var program = Registry.CurrentUser.CreateSubKey(@"Software\Classes\MDReader.Markdown\shell\open\command");
        program.SetValue(string.Empty, $"\"{Application.ExecutablePath}\" \"%1\"");
        foreach (var extension in new[] { ".md", ".markdown" })
        {
            using var associations = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension + @"\OpenWithProgids");
            associations.SetValue("MDReader.Markdown", string.Empty);
        }
    }
}
