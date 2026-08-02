using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PeakLanMod.Lan.Services;

internal static class LanEndpointResolver
{
    internal static bool TryResolveHostLanIpv4(
        string preferredHostIpv4,
        string allowedHostInterfacesCsv,
        out string selectedIpv4,
        out string reason)
    {
        selectedIpv4 = string.Empty;
        reason = string.Empty;

        string preferred = preferredHostIpv4.Trim();

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            if (!TryParseUsableIpv4(preferred, out _))
            {
                reason = "PreferredHostIPv4 is not a usable non-loopback IPv4 address.";
                return false;
            }

            selectedIpv4 = preferred;
            reason = "Selected PreferredHostIPv4 override.";
            return true;
        }

        HashSet<string> allowedTokens = ParseCsv(
            allowedHostInterfacesCsv);

        List<Candidate> candidates = CollectCandidates(
            allowedTokens);

        if (candidates.Count == 0)
        {
            reason = allowedTokens.Count == 0
                ? "No active non-loopback IPv4 address found on supported interfaces."
                : "No active non-loopback IPv4 address matched AllowedHostInterfaces filters.";
            return false;
        }

        candidates.Sort(CandidateComparer.Instance);

        Candidate selected = candidates[0];
        selectedIpv4 = selected.Address;

        reason =
            $"Auto-selected interface '{selected.InterfaceName}' " +
            $"(type={selected.InterfaceType}, score={selected.PriorityScore}) " +
            $"from {candidates.Count} candidate(s).";

        return true;
    }

    private static List<Candidate> CollectCandidates(
        HashSet<string> allowedTokens)
    {
        var candidates = new List<Candidate>();

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!IsEligibleInterface(networkInterface))
            {
                continue;
            }

            if (allowedTokens.Count > 0 && !InterfaceMatchesFilter(networkInterface, allowedTokens))
            {
                continue;
            }

            IPInterfaceProperties properties = networkInterface.GetIPProperties();
            bool hasGateway = properties.GatewayAddresses.Count > 0;
            int priorityScore = GetInterfacePriority(networkInterface.NetworkInterfaceType);

            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
            {
                IPAddress address = unicast.Address;

                if (!TryParseUsableIpv4(address.ToString(), out _))
                {
                    continue;
                }

                if (IsLinkLocalIpv4(address))
                {
                    continue;
                }

                candidates.Add(
                    new Candidate(
                        address.ToString(),
                        networkInterface.Name,
                        networkInterface.Description,
                        networkInterface.NetworkInterfaceType,
                        priorityScore + (hasGateway ? 10 : 0)));
            }
        }

        return candidates;
    }

    private static bool IsEligibleInterface(
        NetworkInterface networkInterface)
    {
        if (networkInterface.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        return networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback
            and not NetworkInterfaceType.Tunnel;
    }

    private static bool InterfaceMatchesFilter(
        NetworkInterface networkInterface,
        HashSet<string> allowedTokens)
    {
        string name = networkInterface.Name;
        string description = networkInterface.Description;
        string identifier = networkInterface.Id;

        foreach (string token in allowedTokens)
        {
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase)
                || description.Contains(token, StringComparison.OrdinalIgnoreCase)
                || identifier.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseUsableIpv4(
        string value,
        out IPAddress address)
    {
        if (!IPAddress.TryParse(value, out address!))
        {
            address = IPAddress.None;
            return false;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        return true;
    }

    private static bool IsLinkLocalIpv4(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();

        return bytes.Length == 4
            && bytes[0] == 169
            && bytes[1] == 254;
    }

    private static int GetInterfacePriority(
        NetworkInterfaceType interfaceType)
    {
        return interfaceType switch
        {
            NetworkInterfaceType.Ethernet => 300,
            NetworkInterfaceType.Ethernet3Megabit => 280,
            NetworkInterfaceType.FastEthernetFx => 280,
            NetworkInterfaceType.FastEthernetT => 280,
            NetworkInterfaceType.GigabitEthernet => 300,
            NetworkInterfaceType.Wireless80211 => 200,
            _ => 100
        };
    }

    private static HashSet<string> ParseCsv(string value)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in value.Split(','))
        {
            string trimmed = part.Trim();

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                tokens.Add(trimmed);
            }
        }

        return tokens;
    }

    private sealed class CandidateComparer : IComparer<Candidate>
    {
        internal static readonly CandidateComparer Instance = new();

        public int Compare(Candidate? x, Candidate? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            int score = y.PriorityScore.CompareTo(x.PriorityScore);

            if (score != 0)
            {
                return score;
            }

            int name = string.Compare(
                x.InterfaceName,
                y.InterfaceName,
                StringComparison.OrdinalIgnoreCase);

            if (name != 0)
            {
                return name;
            }

            return string.Compare(
                x.Address,
                y.Address,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class Candidate
    {
        internal Candidate(
            string address,
            string interfaceName,
            string interfaceDescription,
            NetworkInterfaceType interfaceType,
            int priorityScore)
        {
            Address = address;
            InterfaceName = interfaceName;
            InterfaceDescription = interfaceDescription;
            InterfaceType = interfaceType;
            PriorityScore = priorityScore;
        }

        internal string Address { get; }

        internal string InterfaceName { get; }

        internal string InterfaceDescription { get; }

        internal NetworkInterfaceType InterfaceType { get; }

        internal int PriorityScore { get; }
    }
}
