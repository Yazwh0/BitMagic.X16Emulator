using System.Text.Json;

namespace BitMagic.X16Emulator.Display;

// Remembers the window position between runs, one entry per host (X16D, X16E).
// Stored in the user's application data folder (%APPDATA% on Windows, ~/.config on Linux).
internal static class WindowSettings
{
    private sealed class Position
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    private static readonly object _lock = new();

    private static string FileName => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BitMagic", "window.json");

    public static (int X, int Y)? LoadPosition(string key)
    {
        try
        {
            lock (_lock)
            {
                var settings = Read();
                if (settings.TryGetValue(key, out var position))
                    return (position.X, position.Y);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not read window settings: {e.Message}");
        }

        return null;
    }

    public static void SavePosition(string key, int x, int y)
    {
        try
        {
            lock (_lock)
            {
                var settings = Read();
                settings[key] = new Position { X = x, Y = y };

                Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
                File.WriteAllText(FileName, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not save window settings: {e.Message}");
        }
    }

    private static Dictionary<string, Position> Read()
    {
        if (!File.Exists(FileName))
            return new();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Position>>(File.ReadAllText(FileName)) ?? new();
        }
        catch (JsonException)
        {
            return new(); // corrupt file, start again
        }
    }
}
