using System;

namespace Xoderony.Networking {
    /// <summary>
    /// 玩法层对等会话成员与 Owner 事实；由项目实现组合平台会话与传输连接。
    /// </summary>
    public interface INetworkSession {
        /// <summary>本端是否处于已 <see cref="Started"/>、尚未 <see cref="Stopped"/> 的逻辑会话中。</summary>
        bool IsStarted { get; }

        /// <summary>当前会话所有者的 PeerId；无活动会话时为 0。</summary>
        ulong OwnerPeerId { get; }

        /// <summary>本端是否为当前会话所有者。</summary>
        bool IsOwner { get; }

        /// <summary>本端进入逻辑会话后触发（如进入平台 Lobby）。</summary>
        event Action Started;

        /// <summary>本端离开逻辑会话后触发。不逐个补发 <see cref="MemberLeft"/>。</summary>
        event Action Stopped;

        /// <summary>远端成员与本端建立传输连接时触发，与 <see cref="INetworkTransport.PeerConnected"/> 一一对应。</summary>
        event Action<ulong> MemberJoined;

        /// <summary>远端成员传输连接断开时触发，与 <see cref="INetworkTransport.PeerDisconnected"/> 一一对应。</summary>
        event Action<ulong> MemberLeft;

        /// <summary>会话所有者变化时触发，参数依次为旧、新所有者 PeerId。</summary>
        event Action<ulong, ulong> OwnerChanged;
    }
}

