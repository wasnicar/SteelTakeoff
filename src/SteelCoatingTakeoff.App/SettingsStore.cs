using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.App
{
    /// <summary>
    /// Loads/saves <see cref="SageSettings"/> to appsettings.json next to the
    /// executable, using the built-in DataContractJsonSerializer (no NuGet deps).
    /// </summary>
    public static class SettingsStore
    {
        private static string Path =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static SageSettings Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var bytes = File.ReadAllBytes(Path);
                    var ser = new DataContractJsonSerializer(typeof(SageSettings));
                    using (var ms = new MemoryStream(bytes))
                        return (SageSettings)ser.ReadObject(ms) ?? new SageSettings();
                }
            }
            catch
            {
                // Corrupt/edited file — fall back to defaults rather than crash.
            }
            return new SageSettings();
        }

        public static void Save(SageSettings settings)
        {
            try
            {
                var ser = new DataContractJsonSerializer(typeof(SageSettings));
                using (var ms = new MemoryStream())
                {
                    ser.WriteObject(ms, settings);
                    File.WriteAllText(Path, Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch
            {
                // Non-fatal: settings just won't persist this run.
            }
        }
    }
}
