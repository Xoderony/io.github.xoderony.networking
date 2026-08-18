namespace Xoderony.Networking.Transport {
    ///// <summary>
    ///// 空壳占位：当前仅实现 Steam 传输，本类型仅保证接口可编译；功能待后续决定。
    ///// </summary>
    //public sealed class LoopbackTransport : INetworkTransport
    //{
    //    public ulong LocalPeerId => 0;

    //    public event Action<ulong> PeerConnected;
    //    public event Action<ulong> PeerDisconnected;
    //    public event NetworkDataReceivedHandler DataReceived;

    //    public bool Start() => false;

    //    public bool ConnectPeer(ulong peerId) => false;

    //    public void Stop()
    //    {
    //    }

    //    public void SendData(ulong peerId, ReadOnlySpan<byte> payload, NetworkDelivery delivery)
    //    {
    //    }

    //    public void DisconnectPeer(ulong peerId)
    //    {
    //    }

    //    public void Poll()
    //    {
    //    }

    //    public ulong GetRtt(ulong peerId) => 0;
    //}
}
