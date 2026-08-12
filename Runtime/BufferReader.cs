using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Xoderony.Networking
{
    public sealed class BufferReader
    {
        private byte[] _buffer;
        private int _length;
        private int _position;

        public int Length => _length;
        public int Remaining => _length - _position;

        public int Position
        {
            get => _position;
            set => _position = value;
        }

        public BufferReader(int capacity = 256)
        {
            _buffer = new byte[capacity];
        }

        public void Clear()
        {
            _length = 0;
            _position = 0;
        }

        public void Load(ArraySegment<byte> data)
        {
            Clear();
            EnsureCapacity(data.Count);
            if (data.Count > 0)
            {
                Buffer.BlockCopy(data.Array!, data.Offset, _buffer, 0, data.Count);
            }

            _length = data.Count;
            _position = 0;
        }

        public byte ReadByte()
        {
            EnsureRead(1);
            return _buffer[_position++];
        }

        public ushort ReadUShort()
        {
            EnsureRead(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(_position));
            _position += 2;
            return value;
        }

        public int ReadInt()
        {
            EnsureRead(4);
            var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_position));
            _position += 4;
            return value;
        }

        public uint ReadUInt()
        {
            EnsureRead(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(_position));
            _position += 4;
            return value;
        }

        public ulong ReadULong()
        {
            EnsureRead(8);
            var value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.AsSpan(_position));
            _position += 8;
            return value;
        }

        public float ReadFloat()
        {
            EnsureRead(4);
            var value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.AsSpan(_position));
            _position += 4;
            return value;
        }

        public void ReadBytes(Span<byte> destination)
        {
            EnsureRead(destination.Length);
            _buffer.AsSpan(_position, destination.Length).CopyTo(destination);
            _position += destination.Length;
        }

        public ArraySegment<byte> ReadByteSegment(int length)
        {
            EnsureRead(length);
            var segment = new ArraySegment<byte>(_buffer, _position, length);
            _position += length;
            return segment;
        }

        public string ReadString()
        {
            var byteCount = ReadUShort();
            if (byteCount == 0)
            {
                return string.Empty;
            }

            EnsureRead(byteCount);
            var value = Encoding.UTF8.GetString(_buffer, _position, byteCount);
            _position += byteCount;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int count)
        {
            if (count <= _buffer.Length)
            {
                return;
            }

            var size = _buffer.Length;
            while (size < count)
            {
                size *= 2;
            }

            Array.Resize(ref _buffer, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureRead(int count)
        {
            if (_position + count > _length)
            {
                throw new InvalidOperationException("BufferReader underrun.");
            }
        }
    }
}
