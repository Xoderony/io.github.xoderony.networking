namespace Xoderony.Networking.Messaging
{
    /// <summary>
    /// 基础消息协议限制：只定义协议级数据上限；具体协议的固定头由各自模块计算。
    /// </summary>
    public static class NetworkMessageLimits
    {
        /// <summary>对象状态最大字节数，对齐项目状态数据上限。</summary>
        public const int StateDataCapacity = 1024;

        /// <summary>单条消息载荷上限（含内置协议固定头余量）。会话信封按此分配。</summary>
        public const int PayloadCapacity = StateDataCapacity + 64;
    }
}
