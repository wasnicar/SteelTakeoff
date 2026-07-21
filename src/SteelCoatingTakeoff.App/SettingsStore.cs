using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.App
{
    /// <summary>
    /// Loads/saves <see cref="SageSettings"/> using the built-in
    /// DataContractJsonSerializer (no NuGet deps).
    ///
    /// Settings are written PER USER to
    /// <c>%APPDATA%\SteelCoatingTakeoff\appsettings.json</c>. They deliberately are NOT
    /// written next to the executable: once installed under Program Files that location
    /// is read-only for a standard user, so saving would silently do nothing.
    ///
    /// On first run the file next to the exe (shipped with the installer) is read as the
    /// seed defaults, so a fresh install still starts with sensible values.
    /// </summary>
    public static class SettingsStore
    {
        private static string UserDir =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SteelCoatingTakeoff");

        /// <summary>Per-user settings file — the one that is written.</summary>
        public static string UserPath => System.IO.Path.Combine(UserDir, "appsettings.json");

        /// <summary>Read-only defaults shipped beside the exe — the seed on first run.</summary>
        private static string SeedPath =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static SageSettings Load()
        {
            // Per-user file wins; fall back to the installed defaults on first run.
            foreach (var path in new[] { UserPath, SeedPath })
            {
                var settings = TryRead(path);
                if (settings != null) return settings;
            }
            return new SageSettings();
        }

        private static SageSettings TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var ser = new DataContractJsonSerializer(typeof(SageSettings));
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                    return (SageSettings)ser.ReadObject(ms);
            }
            catch
            {
                // Corrupt/hand-edited file — fall through to the next candidate.
                return null;
            }
        }

        public static void Save(SageSettings settings)
        {
            try
            {
                Directory.CreateDirectory(UserDir);
                var ser = new DataContractJsonSerializer(typeof(SageSettings));
                using (var ms = new MemoryStream())
                {
                    ser.WriteObject(ms, settings);
                    File.WriteAllText(UserPath, Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch
            {
                // Non-fatal: settings just won't persist this run.
            }
        }
    }
}
