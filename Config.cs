using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Numerics;

namespace FutaZone
{
    public class Config
    {
        // Metadata
        public string Name { get; set; } = "default";

        // ESP
        public bool EnableESP { get; set; } = true;
        public bool EnableLines { get; set; }
        public int EspStyle { get; set; }
        public int EspMode { get; set; }
        public bool ShowTeammates { get; set; }
        public float[] TeamColor { get; set; } = new float[] { 0.6f, 0.827f, 0.0f, 1.0f };
        public float[] EnemyColor { get; set; } = new float[] { 1.0f, 0.6f, 0.75f, 1.0f };
        public float[] BonesColor { get; set; } = new float[] { 0.5f, 0.0f, 0.5f, 1.0f };
        
        // SoundESP
        public bool EnableSoundESP { get; set; }
        public int SoundEspStyle { get; set; }
        public float[] SoundEspColor { get; set; } = new float[] { 1.0f, 0.0f, 0.0f, 1.0f };

        // Misc Visuals
        public bool EnableBombTimer { get; set; } = true;
        public bool EnableWatermark { get; set; } = true;
        public bool EnableHitSound { get; set; } = true;
        public string HitSoundFile { get; set; } = "";
        
        // Aimbot
        public bool EnableAimbot { get; set; }
        public float AimbotFOV { get; set; } = 250f;
        public float AimbotSmoothness { get; set; } = 3.0f;
        public bool AimAtTeammates { get; set; }
        public bool AimbotVisibleCheck { get; set; } = true;
        public bool DisableWhenFlashed { get; set; }
        public float FlashDurationThreshold { get; set; } = 5.0f;
        public bool ShowAimTarget { get; set; }
        public int AimMode { get; set; }
        public bool RandomizeSpeed { get; set; }
        public int SpeedChangeDuration { get; set; } = 500;
        public float OvershootScale { get; set; } = 1.2f;
        public int AimKey { get; set; } = 0x10;
        public int TargetBoneIndex { get; set; } = 2;
        
        // TriggerBot
        public bool EnableTriggerBot { get; set; }
        public int TriggerDelayMs { get; set; }
        public float TriggerMaxVelocity { get; set; }
        public bool TriggerOnTeammates { get; set; }
        public int TriggerKey { get; set; } = 0x12;

        // AutoStop
        public bool EnableAutoStop { get; set; }
        public float AutoStopTriggerThreshold { get; set; }
        public float AutoStopStopThreshold { get; set; }
    }

    public static class ConfigSystem
    {
        private static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FUTAZONE");
        public static List<string> AvailableConfigs { get; private set; } = new List<string>();

        public static void Initialize()
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }
            RefreshConfigs();
        }

        public static void RefreshConfigs()
        {
            AvailableConfigs.Clear();
            if (Directory.Exists(ConfigDirectory))
            {
                var files = Directory.GetFiles(ConfigDirectory, "*.json");
                foreach (var file in files)
                {
                    AvailableConfigs.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
        }

        public static void SaveConfig(Config config, string configName)
        {
            try 
            {
                config.Name = configName;
                string jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(ConfigDirectory, configName + ".json");
                File.WriteAllText(filePath, jsonString);
                RefreshConfigs();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        public static Config LoadConfig(string configName)
        {
            try
            {
                string filePath = Path.Combine(ConfigDirectory, configName + ".json");
                if (File.Exists(filePath))
                {
                    string jsonString = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<Config>(jsonString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }
            return null;
        }

        // Helper to convert Vector4 to float[]
        public static float[] ToFloatArray(Vector4 v)
        {
            return new float[] { v.X, v.Y, v.Z, v.W };
        }

        // Helper to convert float[] to Vector4
        public static Vector4 ToVector4(float[] f)
        {
            if (f != null && f.Length == 4)
                return new Vector4(f[0], f[1], f[2], f[3]);
            return new Vector4(1, 1, 1, 1);
        }
    }
}
