using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace SteelCoatingTakeoff.App
{
    public partial class App : Application
    {
        /// <summary>
        /// The Sage Estimating SDK assembly depends on a large web of siblings that live
        /// beside it in the SDK folder (EstBase, EstDataProxy, Sage.Platform.Core, ...),
        /// so it cannot simply be copied next to this exe. The SDK's own samples solve
        /// this by building into that folder; a desktop app instead points the loader at
        /// it. No SDK types are named here — this is just a path — so the app still
        /// builds and runs without the SDK present.
        /// </summary>
        static App()
        {
            var sdkDir = ResolveSdkDirectory();
            if (sdkDir == null) return;

            var loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                var simpleName = new AssemblyName(args.Name).Name;

                // Cache every outcome, including misses (stored as null), so a name we
                // cannot satisfy is answered immediately instead of re-probing.
                if (loaded.TryGetValue(simpleName, out var cached)) return cached;
                loaded[simpleName] = null;

                var path = Path.Combine(sdkDir, simpleName + ".dll");
                if (!File.Exists(path)) return null;

                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    loaded[simpleName] = assembly;
                    return assembly;
                }
                catch
                {
                    // A sibling that fails to load is not fatal on its own; let the
                    // loader report the real failure against the assembly that needed it.
                    return null;
                }
            };
        }

        /// <summary>
        /// SDK location, in order:
        ///   1. SAGE_SDK_DIR environment variable (per-machine override);
        ///   2. an "Sdk" folder shipped next to the exe (what the installer bundles);
        ///   3. the path baked in at build time from the SageSdkDir MSBuild property
        ///      (the dev machine).
        /// Returns null when none exists, in which case the app runs on the mock/Dry-run
        /// connector.
        /// </summary>
        private static string ResolveSdkDirectory()
        {
            var exeDir = Path.GetDirectoryName(typeof(App).Assembly.Location) ?? ".";

            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("SAGE_SDK_DIR"),
                Path.Combine(exeDir, "Sdk"),
                typeof(App).Assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "SageSdkDir")?.Value
            };

            return candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .FirstOrDefault(c => Directory.Exists(c) &&
                                     File.Exists(Path.Combine(c, "Sage.Estimating.Sdk.dll")));
        }
    }
}
