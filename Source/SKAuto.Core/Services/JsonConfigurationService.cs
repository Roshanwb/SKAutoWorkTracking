using SKAuto.Core.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SKAuto.Core.Services
{
    public class JsonConfigurationService : IConfigurationService
    {
        private readonly string _configFolder;
        private readonly object _lock = new();

        public JsonConfigurationService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configFolder = Path.Combine(appData, "SKAuto", "Config");
            Directory.CreateDirectory(_configFolder);
        }

        public async Task<T> GetAsync<T>(string key) where T : new()
        {
            var filePath = Path.Combine(_configFolder, $"{key}.json");
            if (!File.Exists(filePath))
                return new T();

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }

        public async Task SetAsync<T>(string key, T value)
        {
            var filePath = Path.Combine(_configFolder, $"{key}.json");
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
            lock (_lock)
            {
                File.WriteAllText(filePath, json);
            }
            await Task.CompletedTask;
        }
    }
}