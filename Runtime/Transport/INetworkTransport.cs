using System;

namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// 传输数据事件委托，用于 <see cref="INetworkTransport.DataReceived"/>。
    /// </summary>
    /// <param name="peerId">发送数据的对等端 id。</param>
    /// <param name="payload">收到的数据；仅在同步调用期间有效，需要保留时必须拷贝。</param>
    /// <param name="delivery">发送方声明的投递方式。</param>
    public delegate void NetworkDataReceivedHandler(ulong peerId, ReadOnlySpan<byte> payload, NetworkDelivery delivery);

    /// <summary>
    /// 对等端之间的字节管道契约（P2P）。
    /// 单一 id 空间：所有对等端（含本机）以 <see cref="LocalPeerId"/> 同类 id 标识，无服务器/客户端之分。
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>本机对等端 id（Steam 下即 SteamID）；<see cref="Start"/> 成功前无效（0）。</summary>
        ulong LocalPeerId { get; }

        /// <summary>初始化或重新启动本端并开始监听入站连接；已运行时不得重复调用。</summary>
        bool Start();

        /// <summary>
        /// 停止当前传输；逐一经 <see cref="PeerDisconnected"/> 上报并断开所有连接，随后释放本次运行资源。
        /// 再次 <see cref="Start"/> 前不能收发数据。
        /// </summary>
        void Stop();

        /// <summary>每帧由上层驱动，供传输处理底层事件（如 Steam 回调/消息）。</summary>
        void Poll();

        /// <summary>
        /// 建立到指定对等端的出站连接；连接建立后经 <see cref="PeerConnected"/> 上报。
        /// 同一对等端至多一条活跃连接：重复调用（含对端同时发起连接）幂等，多余连接由实现收敛。
        /// </summary>
        bool ConnectPeer(ulong peerId);

        /// <summary>断开指定对等端的连接；断开后经 <see cref="PeerDisconnected"/> 上报一次。</summary>
        void DisconnectPeer(ulong peerId);

        /// <summary>向对等端发送数据（参数顺序与上层一致：载荷在前、投递方式在后）。</summary>
        void SendData(ulong peerId, ReadOnlySpan<byte> payload, NetworkDelivery delivery);

        /// <summary>有对等端建立连接时触发；每对等端至多一次。</summary>
        event Action<ulong> PeerConnected;

        /// <summary>有对等端断开时触发；每对等端至多一次。</summary>
        event Action<ulong> PeerDisconnected;

        /// <summary>收到对等端数据时触发。</summary>
        event NetworkDataReceivedHandler DataReceived;

        /// <summary>
        /// 获取指定对等端的往返时延（毫秒）。诊断可选：未实现或未知时返回 0。
        /// </summary>
        ulong GetRtt(ulong peerId);
    }
}
