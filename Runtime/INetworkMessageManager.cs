using System;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>网络消息的处理器注册、路由与发送契约；消息首字节为消息类型，其余为消息载荷。</summary>
    public interface INetworkMessageManager {
        /// <summary>为指定消息类型注册处理器。</summary>
        void RegisterHandler(byte messageType, NetworkMessageHandler handler);

        /// <summary>注销指定消息类型的处理器。</summary>
        void UnregisterHandler(byte messageType, NetworkMessageHandler handler);

        /// <summary>向所有已连接的远端对等端发送消息。</summary>
        void SendToOthers(ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>向所有已连接的远端对等端发送消息，并在本地投递一次。</summary>
        void SendToAll(ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable);

        /// <summary>向指定的已连接对等端发送消息。</summary>
        void SendToPeer(ulong peerId, ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable);
    }
}
