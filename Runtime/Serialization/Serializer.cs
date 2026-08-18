namespace Xoderony.Networking.Serialization
{
    /// <summary>
    /// <typeparamref name="T"/> 默认按原始内存布局序列化；需要固定字段协议时覆盖委托。
    /// </summary>
    public static class Serializer<T> where T : unmanaged
    {
        public delegate void SerializeDelegate(ref BufferWriter writer, in T value);

        public static SerializeDelegate Serialize = static (ref BufferWriter writer, in T value) => writer.WriteUnmanaged(value);
    }
}
