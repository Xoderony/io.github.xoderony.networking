using System;

namespace Xoderony.Networking.Transport {
    /// <summary>传输数据事件委托，用于 <see cref="INetworkTransport.DataReceived"/>。</summary>
    /// <param name="peerId">发送数据的对等端 id。</param>
    /// <param name="payload">收到的数据；仅在同步调用期间有效，需要保留时必须拷贝。</param>
    /// <param name="delivery">数据的投递方式。</param>
    public delegate void NetworkDataReceivedHandler(ulong peerId, ReadOnlySpan<byte> payload, NetworkDelivery delivery);

    /// <summary>对等端之间的字节传输契约（P2P）。所有对等端（包括本地端）使用同一类型的 id 标识，不区分服务器与客户端。</summary>
    public interface INetworkTransport {
        /// <summary>本地对等端 id；<see cref="Start"/> 成功前为 0。</summary>
        ulong LocalPeerId { get; }

        /// <summary>启动传输；成功后可以建立连接和收发数据，已启动时不得重复调用。</summary>
        bool Start();

        /// <summary>停止传输并断开所有对等端；已建立连接的断开通过 <see cref="PeerDisconnected"/> 上报。</summary>
        void Stop();

        /// <summary>驱动传输处理待处理的连接状态、入站数据及其他传输事件；运行期间应定期调用。</summary>
        void Poll();

        /// <summary>请求与指定对等端建立连接；连接建立后通过 <see cref="PeerConnected"/> 上报，对同一对等端的重复请求不得产生重复的逻辑连接。</summary>
        void ConnectPeer(ulong peerId);

        /// <summary>断开与指定对等端的连接；连接断开后通过 <see cref="PeerDisconnected"/> 上报。</summary>
        void DisconnectPeer(ulong peerId);

        /// <summary>使用指定投递方式向已连接的对等端发送数据。</summary>
        void SendData(ulong peerId, ReadOnlySpan<byte> payload, NetworkDelivery delivery);

        /// <summary>与对等端建立连接时触发。</summary>
        event Action<ulong> PeerConnected;

        /// <summary>与对等端断开连接时触发。</summary>
        event Action<ulong> PeerDisconnected;

        /// <summary>收到对等端数据时触发。</summary>
        event NetworkDataReceivedHandler DataReceived;
    }
}
