using System;

namespace Xoderony.Networking {
    /// <summary>逻辑会话成员与所有者事实；不负责传输启停或连接状态。</summary>
    public interface INetworkSession {
        /// <summary>本端是否处于已 <see cref="Started"/>、尚未 <see cref="Stopped"/> 的逻辑会话中。</summary>
        bool IsStarted { get; }

        /// <summary>当前会话所有者的 PeerId；无活动会话时为 0。</summary>
        ulong OwnerPeerId { get; }

        /// <summary>本端是否为当前会话所有者。</summary>
        bool IsOwner { get; }

        /// <summary>本端进入逻辑会话后触发。已有成员由实现侧会话读模型提供，不经 <see cref="MemberJoined"/> 补发。</summary>
        event Action Started;

        /// <summary>本端离开逻辑会话后触发。不逐个补发 <see cref="MemberLeft"/>。</summary>
        event Action Stopped;

        /// <summary>远端成员在本端已处于会话中之后加入时触发。</summary>
        event Action<ulong> MemberJoined;

        /// <summary>远端成员离开逻辑会话时触发。</summary>
        event Action<ulong> MemberLeft;

        /// <summary>会话所有者变化时触发，参数依次为旧、新所有者 PeerId。</summary>
        event Action<ulong, ulong> OwnerChanged;
    }
}
