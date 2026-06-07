using System.Diagnostics;

namespace TestProject
{
    internal static class FmuTestRuntime
    {
        public static bool TryEnablePythonFmuRuntime(out string reason)
        {
            // Step 1: ensure python3.dll is reachable (Femyou-side requirement).
            if (!TryEnablePythonInterpreter(out reason))
                return false;

            // Step 2: ensure pythonfmu-export.dll is reachable. PythonFMU-generated
            // FMUs (NordPool, Fakepool, ...) import pythonfmu-export.dll at load
            // time but DO NOT bundle it inside the .fmu archive.
            // Resolution order:
            //   1. PYTHONFMU_EXPORT_DIR (explicit override)
            //   2. PATH already contains pythonfmu-export.dll
            //   3. ask the configured Python interpreter where the `pythonfmu`
            //      package is installed and use its bundled resources/binaries/win64
            if (!TryEnablePythonFmuExport(out reason))
                return false;

            return true;
        }

        public static string PythonExecutable =>
            Environment.GetEnvironmentVariable("PYTHONFMU_PYTHON_EXE") ?? "python3";

        public static bool FmuContainsCurrentPlatformBinary(string fmuPath)
        {
            if (!File.Exists(fmuPath))
                return false;

            var (platformDirectory, extension) = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? ("win64", ".dll")
                : ("linux64", ".so");

            using var archive = System.IO.Compression.ZipFile.OpenRead(fmuPath);
            return archive.Entries.Any(entry =>
                entry.FullName.StartsWith($"binaries/{platformDirectory}/", StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryEnablePythonInterpreter(out string reason)
        {
            var configuredDirectory = Environment.GetEnvironmentVariable("PYTHONFMU_RUNTIME_DIR");
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
            {
                if (DirectoryContains(configuredDirectory, "python3.dll"))
                {
                    PrependToPath(configuredDirectory);
                    reason = string.Empty;
                    return true;
                }

                reason = $"PYTHONFMU_RUNTIME_DIR does not contain python3.dll: {configuredDirectory}";
                return false;
            }

            if (PathContains("python3.dll"))
            {
                reason = string.Empty;
                return true;
            }

            reason = "PythonFMU tests require python3.dll. Set PYTHONFMU_RUNTIME_DIR to a compatible Python runtime directory.";
            return false;
        }

        private static bool TryEnablePythonFmuExport(out string reason)
        {
            var configuredDirectory = Environment.GetEnvironmentVariable("PYTHONFMU_EXPORT_DIR");
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
            {
                if (DirectoryContains(configuredDirectory, "pythonfmu-export.dll"))
                {
                    PrependToPath(configuredDirectory);
                    reason = string.Empty;
                    return true;
                }

                reason = $"PYTHONFMU_EXPORT_DIR does not contain pythonfmu-export.dll: {configuredDirectory}";
                return false;
            }

            if (PathContains("pythonfmu-export.dll"))
            {
                reason = string.Empty;
                return true;
            }

            if (TryFindPythonFmuExportViaInterpreter(out var discovered))
            {
                PrependToPath(discovered!);
                reason = string.Empty;
                return true;
            }

            reason = "PythonFMU tests require pythonfmu-export.dll (from the `pythonfmu` PyPI package). "
                + "Install it with `pip install pythonfmu` in the Python pointed by PYTHONFMU_PYTHON_EXE, "
                + "or set PYTHONFMU_EXPORT_DIR explicitly.";
            return false;
        }

        private static bool TryFindPythonFmuExportViaInterpreter(out string? directory)
        {
            directory = null;
            try
            {
                var (platformDirectory, exportLibraryName) = Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? ("win64", "pythonfmu-export.dll")
                    : ("linux64", "libpythonfmu-export.so");

                var script = "import os, pythonfmu; "
                    + $"print(os.path.join(os.path.dirname(pythonfmu.__file__), 'resources', 'binaries', '{platformDirectory}'))";

                var psi = new ProcessStartInfo
                {
                    FileName = PythonExecutable,
                    Arguments = $"-c \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                var stdout = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                    return false;

                if (!File.Exists(Path.Combine(stdout, exportLibraryName)))
                    return false;

                directory = stdout;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool PathContains(string libraryName)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            return path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Any(dir => DirectoryContains(dir, libraryName));
        }

        private static bool DirectoryContains(string directory, string libraryName)
        {
            try
            {
                return File.Exists(Path.Combine(directory, libraryName));
            }
            catch
            {
                return false;
            }
        }

        private static void PrependToPath(string directory)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var normalizedDirectory = NormalizePath(directory);
            var alreadyPresent = currentPath
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizePath)
                .Contains(normalizedDirectory, StringComparer.OrdinalIgnoreCase);

            if (!alreadyPresent)
                Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + currentPath);
        }

        private static string NormalizePath(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
