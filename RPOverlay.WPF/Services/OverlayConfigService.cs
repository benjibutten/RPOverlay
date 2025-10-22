using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RPOverlay.WPF.Models;

namespace RPOverlay.WPF.Services
{
    public sealed class OverlayConfigService : IDisposable
    {
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private readonly FileSystemWatcher _watcher;
        private readonly string _configPath;
        private int _reloadGate;

        public OverlayConfigService()
        {
            _configPath = BuildConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            EnsureConfigFile();
            Current = LoadConfigFromDisk();

            _watcher = CreateWatcher();
        }

        public OverlayConfig Current { get; private set; }

    public event Action<OverlayConfig>? ConfigReloaded;

        private FileSystemWatcher CreateWatcher()
        {
            var watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_configPath) ?? string.Empty,
                Filter = Path.GetFileName(_configPath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            watcher.Changed += (_, _) => ScheduleReload();
            watcher.Created += (_, _) => ScheduleReload();
            watcher.Renamed += (_, _) => ScheduleReload();

            return watcher;
        }

        private void ScheduleReload()
        {
            if (Interlocked.Exchange(ref _reloadGate, 1) == 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150).ConfigureAwait(false);
                    var config = LoadConfigFromDisk();
                    Current = config;
                    ConfigReloaded?.Invoke(config);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to reload overlay config: {ex}");
                }
                finally
                {
                    Interlocked.Exchange(ref _reloadGate, 0);
                }
            });
        }

        private OverlayConfig LoadConfigFromDisk()
        {
            try
            {
                using var stream = File.OpenRead(_configPath);
                var config = JsonSerializer.Deserialize<OverlayConfig>(stream, _serializerOptions);
                return config ?? OverlayConfig.CreateDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load overlay config, using defaults: {ex}");
                return OverlayConfig.CreateDefault();
            }
        }

        private void EnsureConfigFile()
        {
            if (File.Exists(_configPath))
            {
                return;
            }

            var defaultConfig = OverlayConfig.CreateDefault();
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }

        private static string BuildConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "RPOverlay", "presets.json");
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}
