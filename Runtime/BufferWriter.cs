using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Xoderony.Networking {
    public ref struct BufferWriter {
        public Span<byte> Buffer;
        public int DataLength;

        public readonly int Remaining => Buffer.Length - DataLength;

        public BufferWriter(Span<byte> buffer) {
            Buffer = buffer;
            DataLength = 0;
        }

        /// <summary>
        /// 容量检查：能否再写 count 字节；写入前调用一次，之后可连续 Write* 而不逐次比较。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool CanWrite(int count) {
            return DataLength + count <= Buffer.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value) {
            Buffer[DataLength++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value) {
            Buffer[DataLength++] = (byte)value;
        }

        // bool 按 1 字节 0/1 写入。
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value) {
            Buffer[DataLength++] = new ByteBool { Bool = value }.Byte;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUShort(ushort value) {
            BinaryPrimitives.WriteUInt16LittleEndian(Buffer[DataLength..], value);
            DataLength += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShort(short value) {
            BinaryPrimitives.WriteUInt16LittleEndian(Buffer[DataLength..], (ushort)value);
            DataLength += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value) {
            BinaryPrimitives.WriteUInt16LittleEndian(Buffer[DataLength..], value);
            DataLength += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt(int value) {
            BinaryPrimitives.WriteInt32LittleEndian(Buffer[DataLength..], value);
            DataLength += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt(uint value) {
            BinaryPrimitives.WriteUInt32LittleEndian(Buffer[DataLength..], value);
            DataLength += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteULong(ulong value) {
            BinaryPrimitives.WriteUInt64LittleEndian(Buffer[DataLength..], value);
            DataLength += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLong(long value) {
            BinaryPrimitives.WriteUInt64LittleEndian(Buffer[DataLength..], (ulong)value);
            DataLength += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value) {
            BinaryPrimitives.WriteUInt32LittleEndian(Buffer[DataLength..], new UIntFloat { Float = value }.UInt);
            DataLength += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value) {
            BinaryPrimitives.WriteUInt64LittleEndian(Buffer[DataLength..], new ULongDouble { Double = value }.ULong);
            DataLength += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> data) {
            data.CopyTo(Buffer[DataLength..]);
            DataLength += data.Length;
        }
    }
}
