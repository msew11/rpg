## Context

当前 ConfigService 按 kind 分派，并同时持有 MapConfig 与道具字典。两份配置均已有 Schema、Godot 读取和失败诊断，需保留这些约束并统一缓存模型。

## Goals / Non-Goals

**Goals:** 以泛型基类决定根形状和存储方式，以特性定义文件名与主键，服务只管理具体缓存引用。

**Non-Goals:** 不迁移设置存档、图鉴资源或服务端存档，不增加自动程序集扫描、热重载和玩法规则。

## Decisions

- `CfgCache` 管理文件特性、Godot 读取、同名 Schema 校验及释放状态；`SingleCfgCache<TCfg>` 读取根对象为 Value；`MultiCfgCache<TKey, TCfg>` 读取根数组为只读 Items 字典。相比继续在服务内分支，增加配置只需添加数据、缓存及服务引用。
- `[CfgFile("xxx.json")]` 标注具体缓存类，Schema 按 `xxx.schema.json` 约定；标注不允许目录路径，所有文件平铺在 config/ 根下，不允许子目录。缺失、子目录和未注册 JSON 一律抛异常。
- `[CfgKey]` 标注多例数据的一项公开可读实例属性；必须且只能有一个，类型须匹配 TKey。加载前校验契约，读取时检查缺失/null/重复键；不默认使用名为 Id 的属性。相比硬编码，测试可用字符串 Code 验证可复用性。
- ConfigService 显式创建 MapCfgCache 与 ItemCfgCache，通过同一个 CfgCache.Load 调度到各基类。不做程序集自动发现，以保持固定服务组合方式。
- MapConfig 更名为 MapCfg，移除 Kind；地图范围及重复覆盖格校验迁入 MapCfgCache。缓存只在整份数据校验成功后发布，不允许重复加载。ConfigService 释放全部缓存，保留的缓存引用也拒绝读取。
- JSON 和调用入口一次迁移，不保留旧格式或转发入口。纯数据使用 init 属性，地图覆盖集合维持已有快照复制语义。

## Risks / Trade-offs

- 特性依赖反射 → 仅在缓存初始化解析文件和主键元数据，主键 getter 缓存为委托。
- 文件根形状与接口变化 → 同步配置、Schema、全部消费者及验收，运行完整检查；通过本地提交可整体回滚。
- 正式道具数据未定 → 保持 item.json 为 []，仅测试写入临时记录。
