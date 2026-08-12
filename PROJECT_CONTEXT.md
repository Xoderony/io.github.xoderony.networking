# Xoderony.Networking - 项目上下文

> 新工作区 / 新对话：先读本文件与 `AGENTS.md`，再改代码。

## 一句话

Unity 6 轻量联网底座：Steam 向、Distributed Authority、显式消息；**不**基于 NGO。

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

## 为何存在（已定结论）

- 无法在保留 stock ILPP 的前提下把 `NetworkObject`/`NetworkBehaviour` 换成纯 Entity 模型。
- 从 NGO 内部「抄干净」或 `NetworkManager2` 拷贝会级联炸裂，不值得。
- FishNet/Mirror/Fusion 仍偏 GO/SDK 形态；为解耦而换库性价比低。
- 项目可接受：无反作弊、显式消息、Host 中继 + Owner 权威；自研轻量包。

## 模块入口

```
Runtime/
  Session/NetSession.cs     # Host/Client、ClientId、Welcome、拥有 Bus/Spawn
  Transport/                # INetTransport, LoopbackNetTransport, SteamNetTransport(stub)
  Messaging/                # NetMessageBus, NetBuffer, NetMessageType
  Spawning/NetSpawn.cs      # 预制体注册、Spawn/Despawn、晚加入快照
  Core/NetworkEntity.cs     # 网络身份；Owner SendState
Samples~/LoopbackDemo/      # 同进程 Host+Client 冒烟
```

### 协议要点

- 信封：`ushort type | ulong senderClientId | payload`
- 内建类型：`Welcome=1`, `Spawn=2`, `Despawn=3`, `EntityState=4`；应用消息 `>= User(32)`
- Host 收到客户端包：先本地 handler，再中继；**`Spawn` 且 `networkId==0` 为请求，不中继**，由 Host 权威分配 id 后再 `SendToOthers`
- `HostClientId = 0`；客户端在 `Welcome` 后才有 `LocalClientId`
- `BindTransport` 时创建 Bus/Spawn（可先 `RegisterPrefab` 再 `Start*`）

### Loopback

- 同名 room 进程内组网；`StartClient(LoopbackNetTransport.RoomAddress(room))`
- 投递同步发生在 `Send`（`Poll` 空操作）

## 当前状态（2026-08）

已完成：包骨架、Session/Transport/Bus/Spawn/Entity、Loopback Demo、README、GitHub 首推 `main`。

未做 / 下一步候选：

1. 真 Steam：`INetTransport` + SteamNetworkingSockets（独立程序集或填 stub，勿强加 Steamworks 依赖到本包）
2. 样例场景/预制体（现仅脚本；需在 Unity 里挂 `LoopbackDemoBootstrap`）
3. 与游戏 `GameEntity` / JoG 的接入层（应在游戏或薄适配包，不进本包核心）
4. 协议硬化：可靠/不可靠语义、断线、Host 迁移（若需要）
5. `.meta` 由 Unity 刷新生成后视情况入库

## 新对话建议首句

```
读 AGENTS.md 和 PROJECT_CONTEXT.md，在本包内继续：<具体任务>
```

勿假设 Journey-of-Guest 工作区规则自动生效；本包以本仓库文档为准。
