namespace TestProject
{
    internal static class FmuTestRuntime
    {
        public static bool TryEnablePythonFmuRuntime(out string reason)
        {
            var configuredDirectory = Environment.GetEnvironmentVariable("PYTHONFMU_RUNTIME_DIR");
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
            {
                if (DirectoryContainsPythonRuntime(configuredDirectory))
                {
                    PrependToPath(configuredDirectory);
                    reason = string.Empty;
                    return true;
                }

                reason = $"PYTHONFMU_RUNTIME_DIR does not contain python3.dll: {configuredDirectory}";
                return false;
            }

            if (PathContainsPythonRuntime())
            {
                reason = string.Empty;
                return true;
            }

            reason = "PythonFMU tests require python3.dll. Set PYTHONFMU_RUNTIME_DIR to a compatible Python runtime directory.";
            return false;
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

        private static bool PathContainsPythonRuntime()
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            return path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Any(DirectoryContainsPythonRuntime);
        }

        private static bool DirectoryContainsPythonRuntime(string directory) =>
            File.Exists(Path.Combine(directory, "python3.dll"));

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
