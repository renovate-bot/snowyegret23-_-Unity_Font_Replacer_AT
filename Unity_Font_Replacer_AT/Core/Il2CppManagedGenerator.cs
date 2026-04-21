using System.Diagnostics;
using Spectre.Console;
using UnityFontReplacer.CLI;

namespace UnityFontReplacer.Core;

public static class Il2CppManagedGenerator
{
    public static void EnsureManagedFolder(ResolvedGamePath resolved)
    {
        if (!string.IsNullOrWhiteSpace(resolved.ManagedPath) &&
            Directory.Exists(resolved.ManagedPath))
        {
            return;
        }

        var managedDir = Path.Combine(resolved.DataPath, "Managed");
        if (Directory.Exists(managedDir))
        {
            resolved.ManagedPath = managedDir;
            return;
        }

        var binaryPath = ResolveGameAssemblyPath(resolved);
        var metadataPath = Path.Combine(
            resolved.DataPath,
            "il2cpp_data",
            "Metadata",
            "global-metadata.dat");

        bool hasBinary = binaryPath != null;
        bool hasMetadata = File.Exists(metadataPath);
        if (!hasBinary && !hasMetadata)
        {
            return;
        }

        if (!hasBinary || !hasMetadata)
        {
            throw new InvalidOperationException(IsKo()
                ? "Il2Cpp 게임으로 보이지만 'GameAssembly.dll' 또는 'global-metadata.dat'가 없습니다."
                : "This looks like an Il2Cpp game, but 'GameAssembly.dll' or 'global-metadata.dat' is missing.");
        }

        var dumperPath = ResolveDumperPath();
        if (dumperPath == null)
        {
            throw new InvalidOperationException(IsKo()
                ? "Managed 폴더가 없고 Il2CppDumper를 찾을 수 없습니다. 실행 파일 폴더 또는 현재 작업 폴더에 'Il2CppDumper\\Il2CppDumper.exe'가 필요합니다."
                : "Managed folder is missing and Il2CppDumper was not found. Expected 'Il2CppDumper\\Il2CppDumper.exe' next to the executable or in the current working directory.");
        }

        var tempOutputDir = Path.Combine(resolved.DataPath, "Managed_");
        CleanupDirectory(tempOutputDir);
        Directory.CreateDirectory(tempOutputDir);

        AnsiConsole.MarkupLine(IsKo()
            ? "[yellow]Managed 폴더가 없어 Il2CppDumper로 더미 DLL을 생성합니다...[/]"
            : "[yellow]Managed folder not found. Generating dummy DLLs with Il2CppDumper...[/]");

        var psi = new ProcessStartInfo
        {
            FileName = dumperPath,
            WorkingDirectory = Path.GetDirectoryName(dumperPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(binaryPath!);
        psi.ArgumentList.Add(metadataPath);
        psi.ArgumentList.Add(tempOutputDir);

        string stdout;
        string stderr;
        int exitCode;

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Il2CppDumper.");
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            exitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            CleanupDirectory(tempOutputDir);
            throw new InvalidOperationException(IsKo()
                ? $"Il2CppDumper 실행 중 예외가 발생했습니다: {ex.Message}"
                : $"Exception while running Il2CppDumper: {ex.Message}");
        }

        var dummyDllDir = Path.Combine(tempOutputDir, "DummyDll");
        if (exitCode != 0 || !Directory.Exists(dummyDllDir))
        {
            var details = BuildFailureDetails(stdout, stderr);
            CleanupDirectory(tempOutputDir);
            throw new InvalidOperationException(IsKo()
                ? $"Il2CppDumper 실행에 실패했습니다.{details}"
                : $"Il2CppDumper failed.{details}");
        }

        Directory.Move(dummyDllDir, managedDir);
        CleanupDirectory(tempOutputDir);

        resolved.ManagedPath = managedDir;
        AnsiConsole.MarkupLine(IsKo()
            ? $"[green]더미 DLL 생성 완료:[/] {Markup.Escape(managedDir)}"
            : $"[green]Dummy DLL generation complete:[/] {Markup.Escape(managedDir)}");
    }

    private static string? ResolveGameAssemblyPath(ResolvedGamePath resolved)
    {
        foreach (var root in EnumerateGameRootCandidates(resolved))
        {
            var candidate = Path.Combine(root, "GameAssembly.dll");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateGameRootCandidates(ResolvedGamePath resolved)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[]
                 {
                     resolved.GamePath,
                     Directory.GetParent(resolved.DataPath)?.FullName,
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                Directory.Exists(candidate) &&
                seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string? ResolveDumperPath()
    {
        foreach (var baseDir in EnumerateSearchRoots())
        {
            foreach (var exeName in new[] { "Il2CppDumper.exe", "Il2CppDumper-x86.exe" })
            {
                var candidate = Path.Combine(baseDir, "Il2CppDumper", exeName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[]
                 {
                     AppDomain.CurrentDomain.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                 })
        {
            if (Directory.Exists(root) && seen.Add(root))
                yield return root;
        }
    }

    private static void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static string BuildFailureDetails(string stdout, string stderr)
    {
        var lines = string.Join(Environment.NewLine,
            (stdout + Environment.NewLine + stderr)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Take(12));

        if (string.IsNullOrWhiteSpace(lines))
            return "";

        return IsKo()
            ? $"{Environment.NewLine}출력:{Environment.NewLine}{lines}"
            : $"{Environment.NewLine}Output:{Environment.NewLine}{lines}";
    }

    private static bool IsKo()
    {
        return string.Equals(Strings.Lang, "ko", StringComparison.OrdinalIgnoreCase);
    }
}
