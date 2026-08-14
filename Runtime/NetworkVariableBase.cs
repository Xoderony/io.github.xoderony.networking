namespace Xoderony.Networking
{
    /// <summary>
    /// 对象上的同步状态项。由 <see cref="INetworkObject"/> 登记进列表后按下标进入快照；
    /// <see cref="IsDirty"/> 一帧内多次置位只在 <see cref="INetworkObjectManager.Flush"/> 时写出当前值。
    /// Write/Read 成对推进同一条流，读写字节数必须一致。
    /// </summary>
    public abstract class NetworkVariableBase
    {
        public bool IsDirty { get; set; }

        public abstract void Write(ref BufferWriter writer);

        public abstract void Read(ref BufferReader reader);
    }
}
