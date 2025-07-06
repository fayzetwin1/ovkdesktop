using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using ovkdesktop.Models;
using System.IO;

namespace ovkdesktop
{
    public class SettingsHelper
    {
        private const string ConfigFileName = "ovklastfmcfg.json";
        private LastFmConfig _config;

        // Private constructor prevents direct instantiation
        private SettingsHelper()
        {
            _config = new LastFmConfig();
        }

        // Factory method - only way to create an instance
        // Ensures object is created and fully initialized
        public static async Task<SettingsHelper> CreateAsync()
        {
            var settings = new SettingsHelper();
            await settings.InitializeAsync(); // Initialize the created instance
            return settings;
        }

        // Private initialization method
        private async Task InitializeAsync()
        {
            try
            {
                // This property will be 100% set by the time of call
                string configFilePath = Path.Combine(App.LocalFolderPath, ConfigFileName);

                if (File.Exists(configFilePath))
                {
                    string json = await File.ReadAllTextAsync(configFilePath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var loadedConfig = JsonSerializer.Deserialize<LastFmConfig>(json);
                        if (loadedConfig != null)
                        {
                            _config = loadedConfig;
                            Debug.WriteLine("[SettingsHelper] Config loaded from file.");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[SettingsHelper] Config file not found. Using default config.");
                }

                Debug.WriteLine("[SettingsHelper] Async initialization complete.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHelper] CRITICAL: Error during async initialization: {ex.GetType().Name} - {ex.Message}");
                // Continue with default (empty) config on error
            }
        }

        // EnsureInitializedAsync method no longer needed
        // as object creation already guarantees initialization

        public async Task SaveAsync()
        {
            try
            {
                string configFilePath = Path.Combine(App.LocalFolderPath, ConfigFileName);
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configFilePath, json);
                Debug.WriteLine("[SettingsHelper] Config saved successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHelper] FAILED to save config: {ex.GetType().Name} - {ex.Message}");
            }
        }

        // Properties remain unchanged
        public bool IsLastFmEnabled { get => _config.IsEnabled; set => _config.IsEnabled = value; }
        public string LastFmApiKey { get => _config.ApiKey; set => _config.ApiKey = value; }
        public string LastFmApiSecret { get => _config.ApiSecret; set => _config.ApiSecret = value; }
        public string LastFmSessionKey { get => _config.SessionKey; set => _config.SessionKey = value; }
        public string LastFmUsername { get => _config.Username; set => _config.Username = value; }
    }
}
