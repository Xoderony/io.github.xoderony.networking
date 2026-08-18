namespace Xoderony.Networking {
    /// <summary>为本地生成的网络对象分配会话内唯一 id。</summary>
    public interface INetworkObjectIdAllocator {
        /// <summary>分配一个当前会话内未曾分配过的非零 id。</summary>
        uint Allocate();
    }
}
