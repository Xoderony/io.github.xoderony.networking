using System;

namespace Xoderony.Networking
{
    /// <summary>
    /// 网络对象全局唯一 id：由生成该对象的对等端 id 与本端自增序号复合。
    /// 对等网格无中心分配：每个对等端各自分配 <see cref="LocalId"/>，<see cref="PeerId"/> 部分保证全局唯一。
    /// 协议载荷只传 <see cref="LocalId"/>，PeerId 取自信封 sender（DA：owner == sender）。
    /// </summary>
    public readonly struct NetworkObjectId : IEquatable<NetworkObjectId>
    {
        /// <summary>生成并拥有该对象的对等端 id。</summary>
        public readonly ulong PeerId;

        /// <summary>生成端本地自增序号，仅在生成端内唯一。</summary>
        public readonly uint LocalId;

        public NetworkObjectId(ulong peerId, uint localId)
        {
            PeerId = peerId;
            LocalId = localId;
        }

        public bool Equals(NetworkObjectId other)
        {
            return PeerId == other.PeerId && LocalId == other.LocalId;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkObjectId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)((PeerId * 397) ^ LocalId);
            }
        }

        public static bool operator ==(NetworkObjectId left, NetworkObjectId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NetworkObjectId left, NetworkObjectId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{PeerId}:{LocalId}";
        }
    }
}
