namespace Xoderony.Networking.Serialization
{
    /// <summary>
    /// <typeparamref name="T"/> 默认按原始内存布局反序列化；需要固定字段协议时覆盖委托。
    /// </summary>
    public static class Deserializer<T> where T : unmanaged
    {
        public delegate T DeserializeDelegate(ref BufferReader reader);

        public static DeserializeDelegate Deserialize = static (ref BufferReader reader) => reader.ReadUnmanaged<T>();
    }
}
