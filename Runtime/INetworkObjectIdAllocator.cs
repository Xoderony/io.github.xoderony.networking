namespace Xoderony.Networking {
    /// <summary>为本端生成的网络对象提供会话内唯一且稳定的 uint id。</summary>
    public interface INetworkObjectIdAllocator {
        /// <summary>从已授权的本地区间取得下一个 id。</summary>
        uint Allocate();
    }
}
