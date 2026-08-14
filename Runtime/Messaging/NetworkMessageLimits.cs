namespace Xoderony.Networking.Messaging
{
    /// <summary>
    /// 基础消息协议限制：只定义协议级数据上限；具体协议（如 Spawn）的固定头与信封容量由各自模块计算。
    /// </summary>
    public static class NetworkMessageLimits
    {
        /// <summary>状态数据（EntityState 载荷与 Spawn 初始状态）最大字节数，对齐项目状态数据上限。</summary>
        public const int StateDataCapacity = 1024;
    }
}
