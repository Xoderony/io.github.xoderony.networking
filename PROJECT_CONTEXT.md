# Xoderony.Networking - 项目上下文

> 新工作区 / 新对话：先读本文件与 `AGENTS.md`，再改代码。

## 一句话

Unity 6 轻量联网底座：Steam 向、Distributed Authority、显式消息；**不**基于 NGO。类型名采用业界固定名（与 NGO 同名靠命名空间区分）。

## 仓库与路径

| 项 | 值 |
|----|-----|
| GitHub | https://github.com/Xoderony/io.github.xoderony.networking |
| 本地（作者机） | `D:\dev\io.github.xoderony.networking` |
| UPM name | `io.github.xoderony.networking` |
| 程序集 | `Xoderony.Networking` |
| 版本 | 0.1.0（foundation，协议未冻结） |

相关但**不要**当模板或依赖：

- `D:\dev\Journey-of-Guest`：游戏工程（曾嵌入/深改 NGO，已放弃用其污染本包）
- `Packages/io.github.xoderony.netcode`：NGO 扩展，另一条线
- `D:\dev\com.unity.netcode.gameobjects`：官方 NGO fork 工作区，与本包无关

## 命名空间与目录

| 命名空间 | 目录 | 类型 |
|----------|------|------|
| `Xoderony.Networking` | `Runtime/` | `NetworkManager`, `NetworkObject`, `NetworkSpawnManager`, `BufferWriter`, `BufferReader` |
| `Xoderony.Networking.Transport` | `Runtime/Transport/` | `NetworkTransport`, `NetworkDelivery`, `LoopbackTransport`, `SteamNetworkTransport` |
| `Xoderony.Networking.Messaging` | `Runtime/Messaging/` | `CustomMessagingManager`, `NetworkMessageType`, `NetworkMessageHandler` |
| `Xoderony.Networking.Samples` | `Samples~/LoopbackDemo/` | Demo |

规则：最多三层；文件夹匹配 ns 后缀；根 ns 类型放 `Runtime/` 根下。

## 模块入口（NGO 同名薄实现）

```
NetworkTransport → NetworkManager → CustomMessagingManager / NetworkSpawnManager / NetworkObject
```

| 角色 | 本包 |
|------|------|
| 运输 | `NetworkTransport` / `LoopbackTransport` / `SteamNetworkTransport`(stub) |
| 会话 | `NetworkManager`（`ServerClientId=0`，`CustomMessaging`，`SpawnManager`） |
| 消息 | `CustomMessagingManager` |
| 缓冲 | `BufferWriter` / `BufferReader` |
| 生成 | `NetworkSpawnManager`（`SpawnedObjects`） |
| 身份 | `NetworkObject`（`NetworkObjectId`，Owner `SendState`） |
| Behaviour / RPC / NV | **无** |

### 协议要点

- 信封：`ushort type | ulong senderClientId | payload`
- 内建：`Welcome=1`, `Spawn=2`, `Despawn=3`, `EntityState=4`；应用 `>= User(32)`
- Host 收到客户端包：先本地 handler，再中继；**`Spawn` 且 `networkObjectId==0` 为请求，不中继**
- Client：`IsConnected` / `Connected` 仅在 Welcome 之后（已有 `LocalClientId`）
- `BindTransport` 时创建 CustomMessaging / SpawnManager

### Loopback

- 同名 room 进程内组网；`StartClient(LoopbackTransport.RoomAddress(room))`
- 投递同步发生在 `Send`（`Poll` 空操作）

## 当前状态（2026-08）

已完成：业界命名对齐、目录/ns 对齐、`BufferWriter`/`BufferReader`（无 Fast 前缀）、Welcome 后 Connected、Loopback Demo 跟 API。

未做 / 下一步候选：

1. 真 Steam：`NetworkTransport` 实现（消费方程序集，勿强加 Steamworks 到本包）
2. 样例场景/预制体（需在 Unity 里挂 `LoopbackDemoBootstrap`）
3. 与游戏 Entity 的接入层（游戏或薄适配包，不进本包核心）
4. 协议硬化：断线细节、Host 迁移（若需要）
5. `.meta` 由 Unity 刷新后视情况入库

## 新对话建议首句

```
读 AGENTS.md 和 PROJECT_CONTEXT.md，在本包内继续：<具体任务>
```

勿假设 Journey-of-Guest 工作区规则自动生效；本包以本仓库文档为准。
