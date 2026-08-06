using System;
using System.IO;
using System.IO.Compression;

namespace SteelCoatingTakeoff.Core.Projects
{
    /// <summary>
    /// Backs up and restores saved takeoffs as a single .zip the user keeps on their
    /// machine — so projects can be preserved before a reinstall / machine move and
    /// restored afterwards, or handed to another estimator.
    ///
    /// Only project files (<see cref="ProjectStore.Extension"/>) are included, and only
    /// each entry's file NAME is used on restore (never a path), so a hand-edited or
    /// hostile zip can't write outside the projects folder.
    /// </summary>
    public static class ProjectBackup
    {
        /// <summary>Zip every project in <paramref name="projectsDir"/> to <paramref name="zipPath"/>. Returns the count.</summary>
        public static int Backup(string projectsDir, string zipPath)
        {
            if (zipPath == null) throw new ArgumentNullException(nameof(zipPath));

            var files = Directory.Exists(projectsDir)
                ? Directory.GetFiles(projectsDir, "*" + ProjectStore.Extension)
                : new string[0];

            var dir = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    var entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal);
                    using (var es = entry.Open())
                    using (var input = File.OpenRead(file))
                        input.CopyTo(es);
                }
            }
            return files.Length;
        }

        /// <summary>
        /// Restore projects from <paramref name="zipPath"/> into <paramref name="projectsDir"/>.
        /// Only .sctk entries are extracted. Existing files are replaced when
        /// <paramref name="overwrite"/> is true, otherwise skipped. Returns the count written.
        /// </summary>
        public static int Restore(string zipPath, string projectsDir, bool overwrite)
        {
            if (zipPath == null) throw new ArgumentNullException(nameof(zipPath));
            if (projectsDir == null) throw new ArgumentNullException(nameof(projectsDir));
            Directory.CreateDirectory(projectsDir);

            var restored = 0;
            using (var fs = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    // Use the bare file name only (zip-slip safe) and take .sctk only.
                    var fileName = Path.GetFileName(entry.Name);
                    if (string.IsNullOrEmpty(fileName)) continue;
                    if (!fileName.EndsWith(ProjectStore.Extension, StringComparison.OrdinalIgnoreCase)) continue;

                    var dest = Path.Combine(projectsDir, fileName);
                    if (File.Exists(dest) && !overwrite) continue;

                    using (var es = entry.Open())
                    using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write))
                        es.CopyTo(output);
                    restored++;
                }
            }
            return restored;
        }

        /// <summary>How many .sctk projects a backup zip contains (for a pre-restore prompt).</summary>
        public static int Count(string zipPath)
        {
            var n = 0;
            using (var fs = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                foreach (var entry in zip.Entries)
                    if ((entry.Name ?? "").EndsWith(ProjectStore.Extension, StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }
    }
}
