namespace Xoderony.Networking
{
    /// <summary>会话状态，角色中立：服务器与客户端共用。</summary>
    public enum SessionState
    {
        /// <summary>未启动或已停止（对应 <c>Stop</c> 之后）。</summary>
        Stopped,

        /// <summary>运行中：会话已启动。</summary>
        Running,
    }
}
