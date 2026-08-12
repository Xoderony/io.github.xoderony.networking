using System;

namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// Placeholder for Steam P2P. Wire Steamworks / SteamNetworkingSockets in a consumer-specific
    /// assembly, or extend this type once a Steamworks reference is available.
    /// Use <see cref="LoopbackTransport"/> for logic development without Steam.
    /// </summary>
    public sealed class SteamNetworkTransport : NetworkTransport
    {
        public override bool IsRunning => false;
        public override bool IsHost => false;
        public override ulong LocalTransportId => 0;
        public ulong TargetSteamId { get; set; }

        public override event Action<ulong> PeerConnected
        {
            add { }
            remove { }
        }

        public override event Action<ulong> PeerDisconnected
        {
            add { }
            remove { }
        }

        public override event Action<ulong, ArraySegment<byte>, NetworkDelivery> DataReceived
        {
            add { }
            remove { }
        }

        public override void StartHost() => throw CreateNotWired();

        public override void StartClient(ulong remoteAddress)
        {
            TargetSteamId = remoteAddress;
            throw CreateNotWired();
        }

        public override void Disconnect()
        {
        }

        public override void Send(ulong transportPeerId, ArraySegment<byte> data, NetworkDelivery delivery) =>
            throw CreateNotWired();

        public override void Poll()
        {
        }

        private static NotSupportedException CreateNotWired() =>
            new NotSupportedException(
                "SteamNetworkTransport is not wired to Steamworks in this package build. " +
                "Use LoopbackTransport, or implement NetworkTransport in a consumer assembly.");
    }
}
