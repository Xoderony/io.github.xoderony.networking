using System;

namespace Xoderony.Networking
{
    /// <summary>
    /// Placeholder for Steam P2P. Wire Steamworks / SteamNetworkingSockets in a consumer-specific
    /// assembly or extend this type once a Steamworks reference is available.
    /// Use <see cref="LoopbackNetTransport"/> for logic development without Steam.
    /// </summary>
    public sealed class SteamNetTransport : INetTransport
    {
        public bool IsRunning => false;
        public bool IsHost => false;
        public ulong LocalTransportId => 0;
        public ulong TargetSteamId { get; set; }

        public event Action<ulong> PeerConnected
        {
            add { }
            remove { }
        }

        public event Action<ulong> PeerDisconnected
        {
            add { }
            remove { }
        }

        public event Action<ulong, ArraySegment<byte>, NetDelivery> DataReceived
        {
            add { }
            remove { }
        }

        public void StartHost() => throw CreateNotWired();

        public void StartClient(ulong remoteAddress)
        {
            TargetSteamId = remoteAddress;
            throw CreateNotWired();
        }

        public void Disconnect()
        {
        }

        public void Send(ulong transportPeerId, ArraySegment<byte> data, NetDelivery delivery) =>
            throw CreateNotWired();

        public void Poll()
        {
        }

        public void Dispose() => Disconnect();

        private static NotSupportedException CreateNotWired() =>
            new NotSupportedException(
                "SteamNetTransport is not wired to Steamworks in this package build. " +
                "Use LoopbackNetTransport, or implement Steam P2P against INetTransport.");
    }
}
