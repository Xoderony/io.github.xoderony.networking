namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// 消息的传输投递（QoS）提示。传输实现负责将其映射到自身通道或标志；
    /// 上层不得依赖具体传输的线上行为。
    /// </summary>
    /// <remarks>
    /// 约定：<see cref="Reliable"/> 有序且不丢；<see cref="Unreliable"/> 不保证顺序与送达，
    /// 仅用于高频、只关心最新值的载荷。
    /// 大消息可靠投递的分片属于传输实现细节，不需要枚举值。
    /// </remarks>
    public enum NetworkDelivery : byte
    {
        /// <summary>
        /// 不保证顺序与送达，路径最快。用于高频瞬态状态，只关心最新值。
        /// 载荷上限取决于传输实现（Steam 下无分片 Unreliable 约 1200 字节），超出由传输拒绝；
        /// 协议消息默认 Reliable，不受此限。
        /// </summary>
        Unreliable = 0,

        /// <summary>
        /// 有序、不丢失。所有协议消息（生成/销毁、状态、聊天、生命值）的默认选择，
        /// 因为协议依赖顺序与完整性。
        /// </summary>
        Reliable = 1,
    }
}
