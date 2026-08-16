using System;

namespace Xoderony.Networking {
    /// <summary>逻辑会话成员与所有者事实；不负责传输启停或连接状态。</summary>
    public interface INetworkSession {
        /// <summary>本端是否处于逻辑会话中。</summary>
        bool IsJoined { get; }

        /// <summary>当前会话所有者的 PeerId；无活动会话时为 0。</summary>
        ulong OwnerPeerId { get; }

        /// <summary>本端是否为当前会话所有者。</summary>
        bool IsOwner { get; }

        /// <summary>本端进入逻辑会话后触发。</summary>
        event Action Started;

        /// <summary>本端离开逻辑会话后触发。</summary>
        event Action Stopped;

        /// <summary>远端成员加入逻辑会话时触发。</summary>
        event Action<ulong> MemberJoined;

        /// <summary>远端成员离开逻辑会话时触发。</summary>
        event Action<ulong> MemberLeft;

        /// <summary>会话所有者变化时触发，参数依次为旧、新所有者 PeerId。</summary>
        event Action<ulong, ulong> OwnerChanged;
    }
}
