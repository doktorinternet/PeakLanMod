using ExitGames.Client.Photon;

namespace PeakLanMod.Lan.Model;

internal readonly struct LocalServerEndpoint
{
    internal LocalServerEndpoint(
        string address,
        int port,
        ConnectionProtocol protocol)
    {
        Address = address;
        Port = port;
        Protocol = protocol;
    }

    internal string Address { get; }
    internal int Port { get; }
    internal ConnectionProtocol Protocol { get; }
}
