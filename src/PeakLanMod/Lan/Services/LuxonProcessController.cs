using System;
using System.Diagnostics;
using System.IO;

namespace PeakLanMod.Lan.Services;

internal enum LuxonProcessOwnershipState
{
    NotStartedByPlugin,
    StartedByPlugin,
    StoppedByPlugin
}

internal readonly struct LuxonProcessEnsureResult
{
    private LuxonProcessEnsureResult(
        bool success,
        bool startedByPlugin,
        bool externalProcessDetected,
        int processId,
        string message,
        string executablePathForLog,
        string workingDirectoryForLog)
    {
        Success = success;
        StartedByPlugin = startedByPlugin;
        ExternalProcessDetected = externalProcessDetected;
        ProcessId = processId;
        Message = message;
        ExecutablePathForLog = executablePathForLog;
        WorkingDirectoryForLog = workingDirectoryForLog;
    }

    internal bool Success { get; }
    internal bool StartedByPlugin { get; }
    internal bool ExternalProcessDetected { get; }
    internal int ProcessId { get; }
    internal string Message { get; }
    internal string ExecutablePathForLog { get; }
    internal string WorkingDirectoryForLog { get; }

    internal static LuxonProcessEnsureResult CreateSuccess(
        bool startedByPlugin,
        bool externalProcessDetected,
        int processId,
        string message,
        string executablePathForLog,
        string workingDirectoryForLog)
    {
        return new LuxonProcessEnsureResult(
            success: true,
            startedByPlugin,
            externalProcessDetected,
            processId,
            message,
            executablePathForLog,
            workingDirectoryForLog);
    }

    internal static LuxonProcessEnsureResult CreateFailure(
        string message,
        string executablePathForLog,
        string workingDirectoryForLog)
    {
        return new LuxonProcessEnsureResult(
            success: false,
            startedByPlugin: false,
            externalProcessDetected: false,
            processId: 0,
            message,
            executablePathForLog,
            workingDirectoryForLog);
    }
}

internal static class LuxonProcessController
{
    private static Process? _ownedProcess;

    internal static LuxonProcessOwnershipState OwnershipState { get; private set; } =
        LuxonProcessOwnershipState.NotStartedByPlugin;

    internal static bool TryEnsureRunning(
        string executablePath,
        string executableResolveBaseDirectory,
        string workingDirectory,
        string startArguments,
        out LuxonProcessEnsureResult result)
    {
        result = LuxonProcessEnsureResult.CreateFailure(
            "Unknown failure.",
            "<unknown>",
            "<unknown>");

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            result = LuxonProcessEnsureResult.CreateFailure(
                "Local server executable path is empty.",
                "<empty>",
                "<unknown>");
            return false;
        }

        string resolvedExecutablePath = ResolveExecutablePath(
            executablePath.Trim(),
            executableResolveBaseDirectory,
            workingDirectory);

        if (!File.Exists(resolvedExecutablePath))
        {
            result = LuxonProcessEnsureResult.CreateFailure(
                "Local server executable was not found.",
                SanitizePathForLog(resolvedExecutablePath),
                "<unknown>");
            return false;
        }

        string resolvedWorkingDirectory = ResolveWorkingDirectory(
            workingDirectory,
            resolvedExecutablePath,
            out bool usedWorkingDirectoryFallback);

        string workingDirectoryForLog = SanitizePathForLog(
            resolvedWorkingDirectory);

        if (TryGetRunningOwnedProcess(out Process? ownedProcess))
        {
            result = LuxonProcessEnsureResult.CreateSuccess(
                startedByPlugin: false,
                externalProcessDetected: false,
                processId: ownedProcess!.Id,
                message: "Owned local server process is already running.",
                executablePathForLog: SanitizePathForLog(resolvedExecutablePath),
                workingDirectoryForLog);
            return true;
        }

        if (TryFindExternalProcess(resolvedExecutablePath, out int externalPid))
        {
            OwnershipState = LuxonProcessOwnershipState.NotStartedByPlugin;

            result = LuxonProcessEnsureResult.CreateSuccess(
                startedByPlugin: false,
                externalProcessDetected: true,
                processId: externalPid,
                message: "Detected already-running external local server process.",
                executablePathForLog: SanitizePathForLog(resolvedExecutablePath),
                workingDirectoryForLog);
            return true;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedExecutablePath,
                WorkingDirectory = resolvedWorkingDirectory,
                Arguments = startArguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? started = Process.Start(startInfo);

            if (started is null)
            {
                result = LuxonProcessEnsureResult.CreateFailure(
                    "Process.Start returned null.",
                    SanitizePathForLog(resolvedExecutablePath),
                    workingDirectoryForLog);
                return false;
            }

            _ownedProcess = started;
            OwnershipState = LuxonProcessOwnershipState.StartedByPlugin;

            result = LuxonProcessEnsureResult.CreateSuccess(
                startedByPlugin: true,
                externalProcessDetected: false,
                processId: started.Id,
                message: usedWorkingDirectoryFallback
                    ? "Started local server process with executable-directory fallback for working directory."
                    : "Started local server process.",
                executablePathForLog: SanitizePathForLog(resolvedExecutablePath),
                workingDirectoryForLog);
            return true;
        }
        catch (Exception ex)
        {
            result = LuxonProcessEnsureResult.CreateFailure(
                $"Failed to start local server process: {ex.GetType().Name}: {ex.Message}",
                SanitizePathForLog(resolvedExecutablePath),
                workingDirectoryForLog);
            return false;
        }
    }

    internal static bool TryStopOwnedProcess(
        int stopTimeoutMs,
        bool forceKill,
        out string resultMessage)
    {
        if (_ownedProcess is null)
        {
            resultMessage = "No owned local server process is tracked.";
            return false;
        }

        Process process = _ownedProcess;

        if (process.HasExited)
        {
            resultMessage =
                $"Owned local server process already exited. pid={process.Id}";
            MarkStopped();
            return true;
        }

        bool closeRequested = false;

        try
        {
            closeRequested = process.CloseMainWindow();
        }
        catch
        {
            closeRequested = false;
        }

        if (stopTimeoutMs > 0 && process.WaitForExit(stopTimeoutMs))
        {
            resultMessage =
                $"Owned local server exited after close request. pid={process.Id}; closeRequested={closeRequested}";
            MarkStopped();
            return true;
        }

        if (process.HasExited)
        {
            resultMessage =
                $"Owned local server exited quickly. pid={process.Id}";
            MarkStopped();
            return true;
        }

        if (!forceKill)
        {
            resultMessage =
                $"Owned local server did not exit before timeout. pid={process.Id}; forceKillDisabled=true";
            return false;
        }

        try
        {
            process.Kill();

            if (stopTimeoutMs > 0)
            {
                process.WaitForExit(stopTimeoutMs);
            }

            if (process.HasExited)
            {
                resultMessage =
                    $"Owned local server process was force-killed. pid={process.Id}";
                MarkStopped();
                return true;
            }

            resultMessage =
                $"Force kill requested but process is still running. pid={process.Id}";
            return false;
        }
        catch (Exception ex)
        {
            resultMessage =
                $"Failed to stop owned local server process. pid={process.Id}; error={ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool TryGetRunningOwnedProcess(out Process? process)
    {
        process = _ownedProcess;

        if (process is null)
        {
            return false;
        }

        if (process.HasExited)
        {
            MarkStopped();
            process = null;
            return false;
        }

        return true;
    }

    private static bool TryFindExternalProcess(
        string resolvedExecutablePath,
        out int processId)
    {
        processId = 0;

        string fileName = Path.GetFileNameWithoutExtension(
            resolvedExecutablePath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        Process[] candidates = Process.GetProcessesByName(fileName);

        foreach (Process candidate in candidates)
        {
            try
            {
                string? candidatePath = candidate.MainModule?.FileName;

                if (string.Equals(
                        candidatePath,
                        resolvedExecutablePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    processId = candidate.Id;
                    return true;
                }
            }
            catch
            {
                // Access to MainModule can fail for some processes.
                // Keep scanning candidates and only accept a definitive path match.
            }
        }

        return false;
    }

    private static string ResolveExecutablePath(
        string configuredPath,
        string resolveBaseDirectory,
        string configuredWorkingDirectory)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        string relativePath = configuredPath;

        string candidateFromWorkingDirectory = string.Empty;

        if (!string.IsNullOrWhiteSpace(configuredWorkingDirectory))
        {
            string trimmedWorkingDirectory = configuredWorkingDirectory.Trim();

            string resolvedWorkingDirectory = Path.IsPathRooted(trimmedWorkingDirectory)
                ? trimmedWorkingDirectory
                : Path.GetFullPath(
                    Path.Combine(Environment.CurrentDirectory, trimmedWorkingDirectory));

            candidateFromWorkingDirectory = Path.GetFullPath(
                Path.Combine(resolvedWorkingDirectory, relativePath));

            if (File.Exists(candidateFromWorkingDirectory))
            {
                return candidateFromWorkingDirectory;
            }
        }

        string candidateFromCurrentDirectory = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, relativePath));

        if (File.Exists(candidateFromCurrentDirectory))
        {
            return candidateFromCurrentDirectory;
        }

        if (!string.IsNullOrWhiteSpace(resolveBaseDirectory))
        {
            string currentBase = Path.GetFullPath(resolveBaseDirectory);

            while (true)
            {
                string candidate = Path.GetFullPath(
                    Path.Combine(currentBase, relativePath));

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(currentBase);
                if (parent is null)
                {
                    break;
                }

                currentBase = parent.FullName;
            }
        }

        if (!string.IsNullOrWhiteSpace(candidateFromWorkingDirectory))
        {
            return candidateFromWorkingDirectory;
        }

        return candidateFromCurrentDirectory;
    }

    private static string ResolveWorkingDirectory(
        string configuredWorkingDirectory,
        string resolvedExecutablePath,
        out bool usedFallback)
    {
        usedFallback = false;

        string executableDirectory = Path.GetDirectoryName(
            resolvedExecutablePath)
            ?? Environment.CurrentDirectory;

        if (!string.IsNullOrWhiteSpace(configuredWorkingDirectory))
        {
            string trimmed = configuredWorkingDirectory.Trim();

            string candidate = Path.IsPathRooted(trimmed)
                ? trimmed
                : Path.GetFullPath(
                    Path.Combine(Environment.CurrentDirectory, trimmed));

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            usedFallback = true;
            return executableDirectory;
        }

        return executableDirectory;
    }

    private static string SanitizePathForLog(string path)
    {
        string fileName = Path.GetFileName(path);
        string fingerprint = LanRuntimeContext.Fingerprint(path);

        return string.IsNullOrWhiteSpace(fileName)
            ? $"<path:{fingerprint}>"
            : $"{fileName} ({fingerprint})";
    }

    private static void MarkStopped()
    {
        OwnershipState = LuxonProcessOwnershipState.StoppedByPlugin;
        _ownedProcess = null;
    }
}
