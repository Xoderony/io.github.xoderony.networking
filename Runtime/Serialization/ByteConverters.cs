using System.Runtime.InteropServices;

namespace Xoderony.Networking.Serialization {
    /// <summary>
    /// 1 字节位重解释：bool 与 byte 互转（bool 变量恒为 0/1，写侧安全）。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct ByteBool {
        [FieldOffset(0)] public byte Byte;
        [FieldOffset(0)] public bool Bool;
    }

    /// <summary>
    /// 4 字节位重解释：float 与 uint 互转，用于协议小端写入前的浮点位转换。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntFloat {
        [FieldOffset(0)] public float Float;
        [FieldOffset(0)] public uint UInt;
    }

    /// <summary>
    /// 8 字节位重解释：double 与 ulong 互转。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct ULongDouble {
        [FieldOffset(0)] public double Double;
        [FieldOffset(0)] public ulong ULong;
    }
}
