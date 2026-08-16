using System;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>消息注册、路由与发送契约；消息首字节为类型，其余为载荷。</summary>
    public interface INetworkMessageManager {
        /// <summary>注册消息处理。</summary>
        void RegisterMessage(byte messageType, NetworkMessageHandler handler);

        /// <summary>注销消息处理。</summary>
        void UnregisterMessage(byte messageType, NetworkMessageHandler handler);

        /// <summary>发送给所有已连接对端（网格直发）。</summary>
        void SendToOthers(ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>广播给所有已连接对端并本地投递；本地回显走同一 handler。</summary>
        void SendToAll(ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>定向发送给指定对端（需已建立连接）。</summary>
        void SendToPeer(ulong peerId, ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable);
    }
}
