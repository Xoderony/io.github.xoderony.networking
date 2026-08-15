using System;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>会话契约（对等模型），供依赖注入与上层解耦。</summary>
    public interface INetworkManager
    {
        /// <summary>当前绑定并驱动的传输。</summary>
        INetworkTransport NetworkTransport { get; }

        /// <summary>会话状态。</summary>
        SessionState State { get; }

        /// <summary>本地 PeerId（即本机传输端 id）。</summary>
        ulong LocalPeerId { get; }

        /// <summary>会话是否运行中。</summary>
        bool IsRunning { get; }

        /// <summary>会话启动时触发。</summary>
        event Action Started;

        /// <summary>会话停止时触发。</summary>
        event Action Stopped;

        /// <summary>有对等端加入时触发（所有端都会收到）。</summary>
        event Action<ulong> PeerJoined;

        /// <summary>有对等端离开时触发（所有端都会收到）。</summary>
        event Action<ulong> PeerLeft;

        /// <summary>启动会话（初始化传输并进入运行态）。</summary>
        bool Start();

        /// <summary>停止会话并释放资源。</summary>
        void Stop();

        /// <summary>每帧驱动传输处理底层事件；由外部生命周期调用。</summary>
        void Poll();

        /// <summary>注册消息处理。</summary>
        void RegisterMessage(byte messageType, NetworkMessageHandler handler);

        /// <summary>注销消息处理。</summary>
        void UnregisterMessage(byte messageType, NetworkMessageHandler handler);

        /// <summary>发送给所有已连接对端（网格直发）。</summary>
        void SendToOthers(byte messageType, ReadOnlySpan<byte> payload, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>广播给所有已连接对端并本地投递（本地回显走同一 handler）。</summary>
        void SendToAll(byte messageType, ReadOnlySpan<byte> payload, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>定向发送给指定对端（需已建立连接）。</summary>
        void SendToPeer(ulong peerId, byte messageType, ReadOnlySpan<byte> payload, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>建立到指定对端（SteamID）的直连。</summary>
        bool ConnectPeer(ulong peerId);
    }
}
