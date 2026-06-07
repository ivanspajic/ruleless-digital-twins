using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TestProject
{
    internal static class FmuTestRuntime
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        private static bool _interpreterPreloaded;
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
                    PreloadPythonInterpreter(configuredDirectory);
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

        // Equivalent of `LD_PRELOAD=libpython3.11.so` on Linux: pre-load the
        // versioned Python DLL into the test process so the FMU's forwarder
        // (python3.dll → pythonNNN.dll) resolves cleanly, and so Python's
        // sys.prefix detection finds the right install.
        private static void PreloadPythonInterpreter(string directory)
        {
            if (_interpreterPreloaded)
                return;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;

            // Set PYTHONHOME so Python embedded mode finds its stdlib (Lib, DLLs).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PYTHONHOME")))
                Environment.SetEnvironmentVariable("PYTHONHOME", directory);

            // Preload the most specific python3X.dll we can find next to python3.dll.
            string? versioned = null;
            try
            {
                versioned = Directory
                    .EnumerateFiles(directory, "python3*.dll")
                    .Where(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        return name != null
                            && name.StartsWith("python3", StringComparison.OrdinalIgnoreCase)
                            && name.Length > "python3".Length;
                    })
                    .OrderByDescending(f => f)
                    .FirstOrDefault();
            }
            catch
            {
                versioned = null;
            }

            if (versioned != null)
                LoadLibraryW(versioned);
            LoadLibraryW(Path.Combine(directory, "python3.dll"));
            _interpreterPreloaded = true;
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
