using System;

namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// 传输数据事件委托，用于 <see cref="NetworkTransport.DataReceived"/>。
    /// </summary>
    /// <param name="clientId">发送数据的对等端传输 id。</param>
    /// <param name="payload">收到的数据；仅在同步调用期间有效，需要保留时必须拷贝。</param>
    /// <param name="delivery">发送方声明的投递方式。</param>
    public delegate void NetworkDataReceivedHandler(ulong clientId, ReadOnlySpan<byte> payload, NetworkDelivery delivery);

    /// <summary>
    /// 对等端之间的字节管道，契约参照 Netcode for GameObjects 的传输层。
    /// 传输端 id 对上层不透明；寻址服务器时使用 <see cref="ServerClientId"/> 占位。
    /// </summary>
    public abstract class NetworkTransport : IDisposable
    {
        /// <summary>代表服务器的常量传输端 id。</summary>
        public abstract ulong ServerClientId { get; }

        /// <summary>指示当前运行时上下文是否支持该传输。</summary>
        public virtual bool IsSupported => true;

        /// <summary>有对等端建立连接时触发。</summary>
        public abstract event Action<ulong> PeerConnected;

        /// <summary>对等端断开连接时触发。</summary>
        public abstract event Action<ulong> PeerDisconnected;

        /// <summary>
        /// 收到对等端数据时触发。真实传输在自身底层轮询/回调中产生事件并同步触发本事件。
        /// </summary>  
        public abstract event NetworkDataReceivedHandler DataReceived;

        /// <summary>
        /// 每帧由上层驱动，供传输处理底层事件（如 Steam 回调/消息）；无需轮询的传输可留空。
        /// </summary>
        public virtual void Poll()
        {
        }

        /// <summary>向指定对等端发送数据，并声明所需的投递保证。</summary>
        public abstract void Send(ulong clientId, ReadOnlySpan<byte> payload, NetworkDelivery networkDelivery);

        /// <summary>本地客户端连接服务器。返回成功或失败。</summary>
        public abstract bool StartClient();

        /// <summary>开始监听客户端连接。返回成功或失败。</summary>
        public abstract bool StartServer();

        /// <summary>断开指定远程客户端。</summary>
        public abstract void DisconnectRemoteClient(ulong clientId);

        /// <summary>本地客户端与服务器断开。</summary>
        public abstract void DisconnectLocalClient();

        /// <summary>获取指定对等端的往返时延，单位毫秒。</summary>
        public abstract ulong GetCurrentRtt(ulong clientId);

        /// <summary>关闭传输并释放资源。</summary>
        public abstract void Shutdown();

        public void Dispose() => Shutdown();
    }
}
