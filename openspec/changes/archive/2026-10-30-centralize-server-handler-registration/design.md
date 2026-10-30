# Design

## Context

见 proposal.md。底层网络、邮箱与存储验收目前通过 ServerModules 工厂参数装配测试 Handler；正式业务回归直接创建 MockServer，使用完整正式模块。

## Goals / Non-Goals

正式 ServerModules 构造器统一自持三类完整处理器表，不再接收 login、role、world 工厂。保留全部既有边界验收，不增加运行时替换表、属性回填或反射注入。

## Decisions

- 将 MockServer 中 Login 注册原样移到 ServerModules 构造方法；Role/World 同样改为必定创建的本地工厂，保留每个分片独立表与延迟初始化。
- 宿主借用内部 IServerModules 契约。ServerModules 固定完整业务能力，测试 DoubleServerModules 自持测试分片与处理器组合，避免正式构造器保留额外测试参数或替换入口。
- 现有真实业务与场景回归继续覆盖正式模块的保存、调度和默认注册；底层受控屏障测试使用独立模块组合验证 Actor、仓库和宿主边界。

## Risks / Trade-offs

- 自定义测试装配与正式装配分离 → 现有正式业务回归及结构检查保持覆盖，双侧均执行完整项目检查。
- 旧调用点仍传入工厂 → 编译所有生产与测试程序集，并逐处更新。
