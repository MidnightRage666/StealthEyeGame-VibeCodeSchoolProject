using System;
using System.IO;
using System.Text.Json;

namespace StealthEyeGame.Systems
{
    public static class SaveSystem
    {
        private static readonly string SaveDirectory =
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Saves");

        private static string GetSaveFile(int slot)
        {
            return Path.Combine(
                SaveDirectory,
                $"save{slot}.json");
        }

        public static bool SaveExists(int slot)
        {
            if (slot < 1 || slot > 3)
                return false;

            return File.Exists(GetSaveFile(slot));
        }

        public static bool Save(int slot, SaveData data)
        {
            if (slot < 1 || slot > 3)
                return false;

            try
            {
                Directory.CreateDirectory(
                    SaveDirectory);

                JsonSerializerOptions options =
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                string json =
                    JsonSerializer.Serialize(
                        data,
                        options);

                File.WriteAllText(
                    GetSaveFile(slot),
                    json);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static SaveData? Load(int slot)
        {
            if (slot < 1 || slot > 3)
                return null;

            try
            {
                string file =
                    GetSaveFile(slot);

                if (!File.Exists(file))
                    return null;

                string json =
                    File.ReadAllText(file);

                return JsonSerializer.Deserialize<SaveData>(
                    json);
            }
            catch
            {
                return null;
            }
        }

        public static bool DeleteSave(int slot)
        {
            if (slot < 1 || slot > 3)
                return false;

            try
            {
                string file =
                    GetSaveFile(slot);

                if (File.Exists(file))
                {
                    File.Delete(file);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}