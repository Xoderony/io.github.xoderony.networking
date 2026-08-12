using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Xoderony.Networking
{
    public sealed class BufferWriter
    {
        private byte[] _buffer;
        private int _length;

        public int Length => _length;
        public int Capacity => _buffer.Length;

        public BufferWriter(int capacity = 256)
        {
            _buffer = new byte[capacity];
        }

        public void Clear() => _length = 0;

        public ArraySegment<byte> AsSegment() => new ArraySegment<byte>(_buffer, 0, _length);

        public void WriteByte(byte value)
        {
            EnsureWrite(1);
            _buffer[_length++] = value;
        }

        public void WriteUShort(ushort value)
        {
            EnsureWrite(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_length), value);
            _length += 2;
        }

        public void WriteInt(int value)
        {
            EnsureWrite(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void WriteUInt(uint value)
        {
            EnsureWrite(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void WriteULong(ulong value)
        {
            EnsureWrite(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_length), value);
            _length += 8;
        }

        public void WriteFloat(float value)
        {
            EnsureWrite(4);
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            EnsureWrite(data.Length);
            data.CopyTo(_buffer.AsSpan(_length));
            _length += data.Length;
        }

        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteUShort(0);
                return;
            }

            var byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > ushort.MaxValue)
            {
                throw new ArgumentException("String too long.", nameof(value));
            }

            WriteUShort((ushort)byteCount);
            EnsureWrite(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += byteCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureWrite(int count)
        {
            var needed = _length + count;
            if (needed <= _buffer.Length)
            {
                return;
            }

            var size = _buffer.Length;
            while (size < needed)
            {
                size *= 2;
            }

            Array.Resize(ref _buffer, size);
        }
    }
}
