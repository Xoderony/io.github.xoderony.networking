using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Xoderony.Networking
{
    public sealed class NetBuffer
    {
        private byte[] _buffer;
        private int _write;
        private int _read;

        public int Length => _write;
        public int Position
        {
            get => _read;
            set => _read = value;
        }

        public NetBuffer(int capacity = 256)
        {
            _buffer = new byte[capacity];
        }

        public void Clear()
        {
            _write = 0;
            _read = 0;
        }

        public ArraySegment<byte> AsSegment() => new ArraySegment<byte>(_buffer, 0, _write);

        public void ResetRead() => _read = 0;

        public void WriteByte(byte value)
        {
            EnsureWrite(1);
            _buffer[_write++] = value;
        }

        public void WriteUShort(ushort value)
        {
            EnsureWrite(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_write), value);
            _write += 2;
        }

        public void WriteInt(int value)
        {
            EnsureWrite(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_write), value);
            _write += 4;
        }

        public void WriteUInt(uint value)
        {
            EnsureWrite(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_write), value);
            _write += 4;
        }

        public void WriteULong(ulong value)
        {
            EnsureWrite(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_write), value);
            _write += 8;
        }

        public void WriteFloat(float value)
        {
            EnsureWrite(4);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_write), value);
#else
            var bits = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bits);
            }
            Buffer.BlockCopy(bits, 0, _buffer, _write, 4);
#endif
            _write += 4;
        }

        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            EnsureWrite(data.Length);
            data.CopyTo(_buffer.AsSpan(_write));
            _write += data.Length;
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
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _write);
            _write += byteCount;
        }

        public byte ReadByte()
        {
            EnsureRead(1);
            return _buffer[_read++];
        }

        public ushort ReadUShort()
        {
            EnsureRead(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(_read));
            _read += 2;
            return value;
        }

        public int ReadInt()
        {
            EnsureRead(4);
            var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_read));
            _read += 4;
            return value;
        }

        public uint ReadUInt()
        {
            EnsureRead(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(_read));
            _read += 4;
            return value;
        }

        public ulong ReadULong()
        {
            EnsureRead(8);
            var value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.AsSpan(_read));
            _read += 8;
            return value;
        }

        public float ReadFloat()
        {
            EnsureRead(4);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            var value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.AsSpan(_read));
#else
            var value = BitConverter.ToSingle(_buffer, _read);
            if (!BitConverter.IsLittleEndian)
            {
                var tmp = new byte[4];
                Buffer.BlockCopy(_buffer, _read, tmp, 0, 4);
                Array.Reverse(tmp);
                value = BitConverter.ToSingle(tmp, 0);
            }
#endif
            _read += 4;
            return value;
        }

        public void ReadBytes(Span<byte> destination)
        {
            EnsureRead(destination.Length);
            _buffer.AsSpan(_read, destination.Length).CopyTo(destination);
            _read += destination.Length;
        }

        public ArraySegment<byte> ReadByteSegment(int length)
        {
            EnsureRead(length);
            var segment = new ArraySegment<byte>(_buffer, _read, length);
            _read += length;
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
            var value = Encoding.UTF8.GetString(_buffer, _read, byteCount);
            _read += byteCount;
            return value;
        }

        public void Load(ArraySegment<byte> data)
        {
            Clear();
            EnsureWrite(data.Count);
            Buffer.BlockCopy(data.Array!, data.Offset, _buffer, 0, data.Count);
            _write = data.Count;
            _read = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureWrite(int count)
        {
            var needed = _write + count;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureRead(int count)
        {
            if (_read + count > _write)
            {
                throw new InvalidOperationException("NetBuffer underrun.");
            }
        }
    }
}
