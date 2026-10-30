# Design

## Context
当前 Net 构造创建空服务器持有服务，释放时关闭它；旧协议及替身网络没有本机托管能力。LogService 的内部格式测试构造接受可空 EnvService。

## Goals / Non-Goals
统一顶层服务所有权和非空依赖。保持启动时 MockServer 为空、纯客户端不启动服务器、后台调度和协议不变。

## Decisions
- 按 Env、Log、EventBus、Config、Setting、MockServer、Net、Session、World、Animation、Items 创建，严格逆序释放，失败继续回收。服务内部不访问全局入口。
- 生产 Net 构造接收非空 MockServerService、EventBusService、LogService，构建 TCP 适配。TCP 适配保存非空借用服务并随换连接保留；Net 不为旧链路或测试替身保存可空服务引用。进程内传输由内部显式工厂创建。
- LogService 将开关读取保存为非空委托；绑定环境的构造要求非空 EnvService，纯格式测试构造仅接收输出委托。全部服务参数统一检查空值和释放状态，反射校验非空声明。
- 原有隔离组装工厂注册完整服务，服务释放由注册句柄或显式调用方承担，Net 只释放自己的传输。

## Risks / Trade-offs
显式构造调用者需持有服务器服务 → 更新全部调用点并覆盖提前释放Net仍可连接服务器、整体退出及初始化失败回收。
