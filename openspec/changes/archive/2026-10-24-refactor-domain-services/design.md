## Context

用户要求 Service 持有状态和方法，GameServices 提供统一入口；复核后补充要求：各服务内部初始化自身属性，并实际减少转发层和公开接口。

## Goals / Non-Goals

Godot 节点只保留表现状态和订阅；主控制器传入运行目录启动 GameServices，不组装业务对象。保持协议、存档、画面和已有验收行为。

## Decisions

- 直接把配置加载、设置状态与读写、动画缓存迁入 ConfigService、SettingService、AnimationService，移除 RepoGameConfig、RepoSettings、RepoActorAnimation 包装链。配置构造即加载，原始加载与设置文件读写方法改为私有。
- 将 GameSession 合入 SessionService，直接持有 LoginCache 和 RoleCache；缓存容器、已有响应构造和网络事件借用入口收为内部接口，UI 只读摘要和角色快照。
- WorldService 拥有世界生命周期、地图、点击意图与权威同步；移除只有一次调用的 InputSystem，输入转换和移动序号直接放在世界服务。MapManager 与 WorldManager 保留为有独立算法职责的内部实现。
- GameServices 公开初始化只接收运行目录及可选传输创建边界，在内部构造服务；显式传入实例的入口仅供内部隔离测试。服务通过构造参数借用同次启动依赖，自身状态由自身初始化。
- IService 仅约定释放；保留显式注册句柄、重复注册保护和逆依赖清理，单项清理失败后继续释放其他服务。HUD 在 Godot _Ready 自行建立，主控制器不再补调用初始化。
- SessionService 在缓存阶段建立账号，非法摘要阻止普通 UI 回调；WorldService 在普通阶段先于 UI 建立世界和提交同步 Action。

## Reference

参考 Dalamud 的 Service{T}、ServiceManager 和 IDalamudService 的服务契约、显式依赖与逆依赖卸载原则。当前保持固定类型的 GameServices 属性入口。

## Validation

重新执行 70 个单元测试、三个 Godot 场景与 tools/check.ps1；核对节点字段、公开接口、初始化调用和文档，确认不存在上述旧类型和逐方法包装。
