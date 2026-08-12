# Xoderony.Networking - 协作规则

## 工作方式

- 默认简体中文；代码标识符、命令、路径、协议字段保持原文。
- 开始任务先读根目录 `PROJECT_CONTEXT.md`，再读直接相关源码。
- `AGENTS.md` 只记长期规则；`PROJECT_CONTEXT.md` 只记事实、入口与当前状态。形成持久规则时同步更新对应文档。
- 只改当前任务所需内容；默认不做 Unity 编译/完整验证，除非用户明确要求。
- 未经用户要求不做 Git 操作；用户要求提交时消息格式「类型: 简述」（`feat`/`fix`/`refactor`/`chore`），中文描述。

## 包边界

- 独立 UPM：`io.github.xoderony.networking`，程序集/命名空间 `Xoderony.Networking`。
- **禁止**依赖 Journey-of-Guest、`JoG.*`、NGO（`Unity.Netcode`）、或游戏工程内的 Entity/玩法代码。
- 与游戏的 `io.github.xoderony.netcode`（NGO 扩展）是不同包，勿混名、勿合并职责。
- Steamworks 不进本包依赖；Steam 走 `INetTransport` 实现（本包仅 stub），Loopback 用于无 Steam 逻辑开发。

## 架构原则

- Distributed Authority：Owner 推状态；Host 分配 ClientId、中继消息、权威 Spawn id；无反作弊假设。
- 显式消息，无 RPC / NetworkVariable。
- 约定大于配置；可读与性能优先；用断言暴露违规，不为未证实需求加抽象。
- C#：UTF-8（无 BOM）、LF；API `PascalCase`；私有字段 `_camelCase`。
- 热路径避免托管分配；异步若引入则用 UniTask + `CancellationToken`（当前基础层可不引）。

## 命名

- GitHub / 目录：`io.github.xoderony.networking`
- 不用 `NetSync` / `Xoderony.Net`（易误解为状态同步或 .NET）
