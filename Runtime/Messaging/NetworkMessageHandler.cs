using Xoderony.Networking.Serialization;

namespace Xoderony.Networking.Messaging
{
    /// <summary>网络消息处理委托。</summary>
    public delegate void NetworkMessageHandler(ulong senderPeerId, BufferReader reader);
}
