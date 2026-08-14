using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// 单个网络对象：状态变量列表 + RPC 通道。
    /// 状态用 <see cref="Register(NetworkVariableBase)"/> 登记，<see cref="NetworkVariableBase.IsDirty"/> 一帧内多次置位只在 <see cref="INetworkObjectManager.Flush"/> 时发最终值。
    /// RPC 用 <see cref="Register(byte, NetworkMessageHandler)"/> 登记通道，<see cref="SendToOthers"/> 每次立即发送。
    /// </summary>
    public interface INetworkObject
    {
        NetworkObjectId Id { get; }

        bool IsSpawned { get; }

        bool IsOwner { get; }

        void Register(NetworkVariableBase variable);

        void Register(byte channel, NetworkMessageHandler handler);

        void Unregister(NetworkVariableBase variable);

        void Unregister(byte channel, NetworkMessageHandler handler);

        void SendToOthers(byte channel, in BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable);

        void SendToAll(byte channel, in BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable);

        void SendToPeer(ulong peerId, byte channel, in BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable);
    }
}
