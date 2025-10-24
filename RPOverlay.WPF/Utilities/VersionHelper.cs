using System.Reflection;

namespace RPOverlay.WPF.Utilities
{
    public static class VersionHelper
    {
        public static string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
        }

        public static string GetFullVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "0.0.0.0";
        }
    }
}
