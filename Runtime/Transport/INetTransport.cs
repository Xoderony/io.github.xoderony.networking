using System;

namespace Xoderony.Networking
{
    /// <summary>
    /// Byte pipe between peers. Transport peer ids are opaque to upper layers.
    /// </summary>
    public interface INetTransport : IDisposable
    {
        bool IsRunning { get; }
        bool IsHost { get; }
        ulong LocalTransportId { get; }

        event Action<ulong> PeerConnected;
        event Action<ulong> PeerDisconnected;
        event Action<ulong, ArraySegment<byte>, NetDelivery> DataReceived;

        void StartHost();
        void StartClient(ulong remoteAddress);
        void Disconnect();
        void Send(ulong transportPeerId, ArraySegment<byte> data, NetDelivery delivery);
        void Poll();
    }
}
