using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RPOverlay.Core.Abstractions;
using RPOverlay.Core.Models;

namespace RPOverlay.Core.Services;

public sealed class OverlayConfigService : IDisposable
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IOverlayConfigPathProvider _pathProvider;
    private FileSystemWatcher? _watcher;
    private string _configPath = string.Empty;
    private int _reloadGate;
    private bool _disposed;
    private readonly object _syncRoot = new();

    public OverlayConfigService(IOverlayConfigPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        ReloadInternal(raiseEvent: false);
    }

    public OverlayConfig Current { get; private set; } = OverlayConfig.CreateDefault();

    public event Action<OverlayConfig>? ConfigReloaded;

    public OverlayConfig ReloadForCurrentProfile()
    {
        return ReloadInternal(raiseEvent: true);
    }

    private OverlayConfig ReloadInternal(bool raiseEvent)
    {
        OverlayConfig config;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OverlayConfigService));
            }

            var newPath = _pathProvider.GetConfigFilePath();
            if (string.IsNullOrWhiteSpace(newPath))
            {
                throw new InvalidOperationException("Overlay config path provider returned an empty path.");
            }

            var pathChanged = !string.Equals(_configPath, newPath, StringComparison.OrdinalIgnoreCase);

            if (pathChanged)
            {
                DisposeWatcher();
                _configPath = newPath;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            EnsureConfigFile();

            config = LoadConfigFromDisk();
            Current = config;

            if (pathChanged || _watcher is null)
            {
                _watcher = CreateWatcher();
            }

            Interlocked.Exchange(ref _reloadGate, 0);
        }

        if (raiseEvent)
        {
            ConfigReloaded?.Invoke(config);
        }

        return config;
    }

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
                if (_disposed)
                {
                    return;
                }

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
        if (string.IsNullOrWhiteSpace(_configPath))
        {
            throw new InvalidOperationException("Overlay config path is not initialized.");
        }

        if (File.Exists(_configPath))
        {
            return;
        }

        var defaultConfig = OverlayConfig.CreateDefault();
        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    public void Save(OverlayConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        try
        {
            // Temporarily disable file watcher to avoid reload loop
            var watcher = _watcher;
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
            }
            
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            
            Current = config;
        }
        finally
        {
            // Re-enable file watcher
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = true;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeWatcher();
    }

    private void DisposeWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }
}
