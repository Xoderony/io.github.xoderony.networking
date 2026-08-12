namespace Xoderony.Networking
{
    /// <summary>
    /// Built-in protocol types. User messages should use values &gt;= <see cref="User"/>.
    /// </summary>
    public static class NetMessageType
    {
        public const ushort Welcome = 1;
        public const ushort Spawn = 2;
        public const ushort Despawn = 3;
        public const ushort EntityState = 4;

        /// <summary>First type id reserved for application messages.</summary>
        public const ushort User = 32;
    }
}
