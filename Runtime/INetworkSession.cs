using System;

namespace Xoderony.Networking {
    /// <summary>表示当前网络会话的生命周期、成员和所有者状态。</summary>
    public interface INetworkSession {
        /// <summary>本端是否处于活动会话中。</summary>
        bool IsStarted { get; }

        /// <summary>当前会话所有者的对等端 id；无活动会话时为 0。</summary>
        ulong OwnerPeerId { get; }

        /// <summary>本端是否为当前会话所有者。</summary>
        bool IsOwner { get; }

        /// <summary>本端进入活动会话时触发。</summary>
        event Action Started;

        /// <summary>本端离开活动会话时触发。</summary>
        event Action Stopped;

        /// <summary>远端成员加入当前会话并可进行通信时触发。</summary>
        event Action<ulong> MemberJoined;

        /// <summary>远端成员离开当前会话或失去通信连接时触发。</summary>
        event Action<ulong> MemberLeft;

        /// <summary>会话所有者变化时触发，参数依次为旧所有者和新所有者的对等端 id。</summary>
        event Action<ulong, ulong> OwnerChanged;
    }
}
