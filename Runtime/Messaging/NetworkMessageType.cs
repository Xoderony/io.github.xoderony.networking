namespace Xoderony.Networking.Messaging
{
    /// <summary>
    /// 内置协议消息类型。应用消息使用 &gt;= <see cref="User"/> 的类型。
    /// 消息类型为 byte（0–255），信封首字节直接数组下标映射。
    /// </summary>
    public static class NetworkMessageType
    {
        public const byte Spawn = 2;
        public const byte Despawn = 3;
        public const byte EntityState = 4;

        /// <summary>应用消息的起始类型。</summary>
        public const byte User = 32;
    }
}
