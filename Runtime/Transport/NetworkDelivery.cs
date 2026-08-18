namespace Xoderony.Networking.Transport {
    /// <summary>指定网络数据的投递保证。</summary>
    public enum NetworkDelivery : byte {
        /// <summary>不保证数据送达或顺序，允许丢失和乱序。</summary>
        Unreliable = 0,

        /// <summary>在连接保持有效的前提下保证数据送达，并保持发送顺序。</summary>
        Reliable = 1,
    }
}
