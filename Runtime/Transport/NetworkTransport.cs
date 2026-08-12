using System;

namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// Byte pipe between peers. Transport peer ids are opaque to upper layers.
    /// </summary>
    public abstract class NetworkTransport : IDisposable
    {
        public abstract bool IsRunning { get; }
        public abstract bool IsHost { get; }
        public abstract ulong LocalTransportId { get; }

        public abstract event Action<ulong> PeerConnected;
        public abstract event Action<ulong> PeerDisconnected;
        public abstract event Action<ulong, ArraySegment<byte>, NetworkDelivery> DataReceived;

        public abstract void StartHost();
        public abstract void StartClient(ulong remoteAddress);
        public abstract void Disconnect();
        public abstract void Send(ulong transportPeerId, ArraySegment<byte> data, NetworkDelivery delivery);
        public abstract void Poll();

        public virtual void Dispose() => Disconnect();
    }
}
