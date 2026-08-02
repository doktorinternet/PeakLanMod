using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace PeakLanMod.Lan.Services;

internal static class LuxonConfigManager
{
    private const string ExternalAddressKey = "external_address:";

    internal static bool TryUpdateExternalAddresses(
        string host,
        string configPath,
        out LuxonConfigUpdateResult result)
    {
        result = LuxonConfigUpdateResult.CreateFailure("", "Unknown failure.");

        string trimmedHost = host.Trim();

        if (!TryParseUsableHost(trimmedHost))
        {
            result = LuxonConfigUpdateResult.CreateFailure(
                configPath,
                "Configured host is not a usable LAN endpoint.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(configPath))
        {
            result = LuxonConfigUpdateResult.CreateFailure(
                configPath,
                "Luxon config path is empty.");
            return false;
        }

        string resolvedPath = ResolvePath(configPath.Trim());

        if (!File.Exists(resolvedPath))
        {
            result = LuxonConfigUpdateResult.CreateFailure(
                resolvedPath,
                "Luxon config file was not found.");
            return false;
        }

        string[] lines = File.ReadAllLines(resolvedPath);
        if (lines.Length == 0)
        {
            result = LuxonConfigUpdateResult.CreateFailure(
                resolvedPath,
                "Luxon config file is empty.");
            return false;
        }

        var updatedLines = new string[lines.Length];

        ConfigSection currentSection = ConfigSection.None;
        bool nameServerMatched = false;
        bool masterServerMatched = false;
        bool gameServerMatched = false;
        int matchedEntryCount = 0;
        int updatedEntryCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            currentSection = UpdateSection(trimmed, currentSection);

            if (!IsTrackedSection(currentSection))
            {
                updatedLines[i] = line;
                continue;
            }

            int keyIndex = line.IndexOf(ExternalAddressKey, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                updatedLines[i] = line;
                continue;
            }

            if (!TryReplaceExternalAddressHost(
                    line,
                    keyIndex,
                    trimmedHost,
                    out string replaced,
                    out bool changed))
            {
                updatedLines[i] = line;
                continue;
            }

            matchedEntryCount++;
            if (changed)
            {
                updatedEntryCount++;
            }

            switch (currentSection)
            {
                case ConfigSection.NameServer:
                    nameServerMatched = true;
                    break;

                case ConfigSection.MasterServer:
                    masterServerMatched = true;
                    break;

                case ConfigSection.GameServer:
                    gameServerMatched = true;
                    break;
            }

            updatedLines[i] = replaced;
        }

        var missingSections = new List<string>();

        if (!nameServerMatched)
        {
            missingSections.Add("NameServer");
        }

        if (!masterServerMatched)
        {
            missingSections.Add("MasterServer");
        }

        if (!gameServerMatched)
        {
            missingSections.Add("GameServer");
        }

        if (missingSections.Count > 0)
        {
            result = LuxonConfigUpdateResult.CreateFailure(
                resolvedPath,
                "Missing external_address in section(s): " +
                string.Join(", ", missingSections));
            return false;
        }

        bool wasChanged = updatedEntryCount > 0;

        if (wasChanged)
        {
            File.WriteAllLines(resolvedPath, updatedLines);
        }

        result = LuxonConfigUpdateResult.CreateSuccess(
            resolvedPath,
            matchedEntryCount,
            updatedEntryCount,
            wasChanged);

        return true;
    }

    private static bool TryReplaceExternalAddressHost(
        string line,
        int keyIndex,
        string host,
        out string replaced,
        out bool changed)
    {
        int valueStart = keyIndex + ExternalAddressKey.Length;
        string prefix = line[..valueStart];
        string suffix = line[valueStart..];

        int commentIndex = suffix.IndexOf('#');
        string valueAndWhitespace = commentIndex >= 0
            ? suffix[..commentIndex]
            : suffix;
        string comment = commentIndex >= 0
            ? suffix[commentIndex..]
            : string.Empty;

        int firstNonWhitespace = 0;
        while (firstNonWhitespace < valueAndWhitespace.Length
               && char.IsWhiteSpace(valueAndWhitespace[firstNonWhitespace]))
        {
            firstNonWhitespace++;
        }

        string whitespace = valueAndWhitespace[..firstNonWhitespace];
        string rawValue = valueAndWhitespace[firstNonWhitespace..].Trim();

        if (rawValue.Length == 0)
        {
            replaced = line;
            changed = false;
            return false;
        }

        bool wrappedInSingleQuotes = rawValue.Length > 1
            && rawValue[0] == '\''
            && rawValue[^1] == '\'';
        bool wrappedInDoubleQuotes = rawValue.Length > 1
            && rawValue[0] == '"'
            && rawValue[^1] == '"';

        string unwrapped = wrappedInSingleQuotes || wrappedInDoubleQuotes
            ? rawValue[1..^1]
            : rawValue;

        if (!TryGetPort(unwrapped, out int port))
        {
            replaced = line;
            changed = false;
            return false;
        }

        string updatedValue = host + ":" +
            port.ToString(CultureInfo.InvariantCulture);

        if (wrappedInSingleQuotes)
        {
            updatedValue = "'" + updatedValue + "'";
        }
        else if (wrappedInDoubleQuotes)
        {
            updatedValue = "\"" + updatedValue + "\"";
        }

        replaced = prefix + whitespace + updatedValue + comment;
        changed = !string.Equals(line, replaced, StringComparison.Ordinal);

        return true;
    }

    private static bool TryGetPort(string value, out int port)
    {
        port = 0;

        int separator = value.LastIndexOf(':');
        if (separator <= 0 || separator >= value.Length - 1)
        {
            return false;
        }

        string portPart = value[(separator + 1)..].Trim();

        if (!int.TryParse(
                portPart,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out port))
        {
            return false;
        }

        return port is >= 1 and <= 65535;
    }

    private static string ResolvePath(string rawPath)
    {
        if (Path.IsPathRooted(rawPath))
        {
            return rawPath;
        }

        return Path.GetFullPath(
            Path.Combine(
                Environment.CurrentDirectory,
                rawPath));
    }

    private static bool TryParseUsableHost(string value)
    {
        if (!IPAddress.TryParse(value, out IPAddress? parsed))
        {
            return false;
        }

        if (parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        if (IPAddress.IsLoopback(parsed))
        {
            return false;
        }

        return true;
    }

    private static ConfigSection UpdateSection(
        string trimmedLine,
        ConfigSection current)
    {
        if (trimmedLine.StartsWith("NameServer:", StringComparison.Ordinal))
        {
            return ConfigSection.NameServer;
        }

        if (trimmedLine.StartsWith("MasterServer:", StringComparison.Ordinal))
        {
            return ConfigSection.MasterServer;
        }

        if (trimmedLine.StartsWith("GameServer:", StringComparison.Ordinal))
        {
            return ConfigSection.GameServer;
        }

        if (trimmedLine.StartsWith("Servers:", StringComparison.Ordinal)
            || trimmedLine.StartsWith("HTTP:", StringComparison.Ordinal)
            || trimmedLine.StartsWith("EnableIPv6:", StringComparison.Ordinal)
            || trimmedLine.StartsWith("MaxConnections:", StringComparison.Ordinal)
            || trimmedLine.StartsWith("MaxGamePeers:", StringComparison.Ordinal)
            || trimmedLine.StartsWith("TickTimeBudget:", StringComparison.Ordinal))
        {
            return ConfigSection.None;
        }

        return current;
    }

    private static bool IsTrackedSection(ConfigSection section)
    {
        return section is ConfigSection.NameServer
            or ConfigSection.MasterServer
            or ConfigSection.GameServer;
    }

    private enum ConfigSection
    {
        None,
        NameServer,
        MasterServer,
        GameServer
    }
}

internal readonly struct LuxonConfigUpdateResult
{
    private LuxonConfigUpdateResult(
        bool success,
        string configPath,
        string message,
        int matchedEntryCount,
        int updatedEntryCount,
        bool wasChanged)
    {
        Success = success;
        ConfigPath = configPath;
        Message = message;
        MatchedEntryCount = matchedEntryCount;
        UpdatedEntryCount = updatedEntryCount;
        WasChanged = wasChanged;
    }

    internal bool Success { get; }

    internal string ConfigPath { get; }

    internal string ConfigPathForLog => string.IsNullOrWhiteSpace(ConfigPath)
        ? "<empty>"
        : Path.GetFileName(ConfigPath);

    internal string Message { get; }

    internal int MatchedEntryCount { get; }

    internal int UpdatedEntryCount { get; }

    internal bool WasChanged { get; }

    internal static LuxonConfigUpdateResult CreateSuccess(
        string configPath,
        int matchedEntryCount,
        int updatedEntryCount,
        bool wasChanged)
    {
        return new LuxonConfigUpdateResult(
            success: true,
            configPath,
            "OK",
            matchedEntryCount,
            updatedEntryCount,
            wasChanged);
    }

    internal static LuxonConfigUpdateResult CreateFailure(
        string configPath,
        string message)
    {
        return new LuxonConfigUpdateResult(
            success: false,
            configPath,
            message,
            matchedEntryCount: 0,
            updatedEntryCount: 0,
            wasChanged: false);
    }
}
