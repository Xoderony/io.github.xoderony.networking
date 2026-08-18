namespace Xoderony.Networking.Messaging
{
    /// <summary>
    /// 基础消息协议限制：只定义协议级数据上限；具体协议的固定头由各自模块计算。
    /// </summary>
    public static class NetworkMessageLimits
    {
        /// <summary>单条消息的最大载荷容量。</summary>
        public const int PayloadCapacity = 1088;

        /// <summary>单条消息的最大容量：类型 + 载荷。</summary>
        public const int MessageCapacity = sizeof(byte) + PayloadCapacity;
    }
}
