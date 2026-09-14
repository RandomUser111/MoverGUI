using System.Text.Json;

namespace MoverGUI;

internal sealed class ConfigStore
{
    private readonly string _dataDir;
    private readonly string _settingsPath;

    public string DataDirectory => _dataDir;
    public string LogsDirectory => Path.Combine(_dataDir, "Logs");

    public ConfigStore()
    {
        _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoverGUI");
        _settingsPath = Path.Combine(_dataDir, "settings.json");
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(LogsDirectory);
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                Normalize(config);
                return config;
            }
        }
        catch
        {
            // If settings are damaged, start with a clean config instead of blocking startup.
        }

        var imported = TryImportLegacySettings();
        Normalize(imported);
        return imported;
    }

    public void Save(AppConfig config)
    {
        Normalize(config);
        Directory.CreateDirectory(_dataDir);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    private static void Normalize(AppConfig config)
    {
        config.RemotePath = string.IsNullOrWhiteSpace(config.RemotePath) ? "/home/tc/storage/" : config.RemotePath.Trim();
        config.UserName = string.IsNullOrWhiteSpace(config.UserName) ? "tc" : config.UserName.Trim();
        config.Port = config.Port is < 1 or > 65535 ? 22 : config.Port;
        config.MaxParallel = Math.Clamp(config.MaxParallel, 1, 16);
        config.Cashes ??= [];
        config.Passwords ??= [];
    }

    private static AppConfig TryImportLegacySettings()
    {
        var config = new AppConfig();
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "settings.ini"),
            Path.Combine(AppContext.BaseDirectory, "Мувер2.0.0", "settings.ini")
        };

        var ini = candidates.FirstOrDefault(File.Exists);
        if (ini is null)
            return config;

        try
        {
            foreach (var raw in File.ReadAllLines(ini))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('[') || !line.Contains('='))
                    continue;

                var parts = line.Split('=', 2);
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (key.Equals("_send_to", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                    config.RemotePath = value;

                if (key.Equals("_IPlist", StringComparison.OrdinalIgnoreCase))
                {
                    var ips = value.Split([' ', '\t', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var n = 1;
                    foreach (var ip in ips.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        config.Cashes.Add(new CashRegister { Name = $"Касса {n++}", Ip = ip, Enabled = true });
                    }
                }
            }
        }
        catch
        {
            // Legacy import is optional.
        }

        return config;
    }
}
