using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Quizzer.Base
{
    public sealed class WindowPlacementStore
    {
        private readonly string _filePath;
        private Dictionary<string, WindowPlacement> _data = new();

        public WindowPlacementStore(string appName = "Quizzer")
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName);

            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "windowPlacements.json");
            Load();
        }

        public bool TryGet(string key, out WindowPlacement? placement)
            => _data.TryGetValue(key, out placement);

        public void Set(string key, WindowPlacement placement)
        {
            _data[key] = placement;
            Save();
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;

            try
            {
                var json = File.ReadAllText(_filePath);
                _data = JsonSerializer.Deserialize<Dictionary<string, WindowPlacement>>(json)
                        ?? new Dictionary<string, WindowPlacement>();
            }
            catch
            {
                _data = new Dictionary<string, WindowPlacement>();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // ignore: placement saving should never crash the app
            }
        }
    }

    public sealed class WindowPlacement
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int WindowState { get; set; } // (int)System.Windows.WindowState
    }
}