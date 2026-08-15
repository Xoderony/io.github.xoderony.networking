namespace Xoderony.Networking
{
    /// <summary>
    /// 单个网络对象的身份与会话状态。
    /// 公开契约视图：与 <see cref="NetworkObject"/> 的公开成员保持同步，类新增公开成员时同步补进接口。
    /// </summary>
    public interface INetworkObject
    {
        NetworkObjectId Id { get; }

        bool IsSpawned { get; }

        bool IsOwner { get; }

        int PrefabId { get; }
    }
}
