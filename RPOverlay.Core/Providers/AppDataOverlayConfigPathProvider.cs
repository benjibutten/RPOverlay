using System;
using System.IO;
using RPOverlay.Core.Abstractions;

namespace RPOverlay.Core.Providers;

public sealed class AppDataOverlayConfigPathProvider : IOverlayConfigPathProvider
{
    public string GetConfigFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "RPOverlay", "presets.json");
    }
}
