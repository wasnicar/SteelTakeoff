using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace SteelCoatingTakeoff.Core.Projects
{
    /// <summary>
    /// Reads and writes saved takeoffs as JSON files in a folder. Uses the built-in
    /// DataContractJsonSerializer, the same as the settings store — no NuGet dependency.
    ///
    /// One file per project, extension ".sctk". The folder is supplied by the caller so
    /// the app can point it at a per-user location or a shared network share.
    /// </summary>
    public static class ProjectStore
    {
        public const string Extension = ".sctk";

        /// <summary>The file a project of this name maps to, with illegal characters scrubbed.</summary>
        public static string PathFor(string directory, string name)
        {
            var safe = Sanitize(name);
            if (safe.Length == 0) safe = "takeoff";
            return Path.Combine(directory, safe + Extension);
        }

        public static void Save(string path, TakeoffProject project)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (project == null) throw new ArgumentNullException(nameof(project));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var ser = new DataContractJsonSerializer(typeof(TakeoffProject));
            // Write to a temp file then move, so a crash mid-write can't corrupt a project.
            var tmp = path + ".tmp";
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, project);
                File.WriteAllText(tmp, Encoding.UTF8.GetString(ms.ToArray()));
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static TakeoffProject Load(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(TakeoffProject));
            using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                return (TakeoffProject)ser.ReadObject(ms);
        }

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>All projects in the folder, newest first. Unreadable files are skipped.</summary>
        public static IReadOnlyList<ProjectSummary> List(string directory)
        {
            var summaries = new List<ProjectSummary>();
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return summaries;

            foreach (var file in Directory.GetFiles(directory, "*" + Extension))
            {
                try
                {
                    var project = Load(file);
                    summaries.Add(new ProjectSummary
                    {
                        Name = string.IsNullOrWhiteSpace(project.Name)
                            ? Path.GetFileNameWithoutExtension(file) : project.Name,
                        Path = file,
                        EstimateName = project.EstimateName ?? "",
                        MemberCount = project.Lines?.Count ?? 0,
                        Modified = File.GetLastWriteTime(file)
                    });
                }
                catch
                {
                    // Corrupt or foreign file — leave it out of the list rather than fail.
                }
            }

            return summaries.OrderByDescending(s => s.Modified).ToList();
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var sb = new StringBuilder(name.Length);
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var ch in name.Trim())
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '-' : ch);
            return sb.ToString();
        }
    }
}
