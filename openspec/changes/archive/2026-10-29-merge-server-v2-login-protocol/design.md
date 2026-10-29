# Design

## Context

角色摘要仅被 V2 登录响应引用，目前单独生成 RoleSummary.cs。

## Goals / Non-Goals

收拢登录协议及其辅助类型，遵循用户的声明顺序，不新增协议、推送或业务。

## Decisions

- 保留所有声明名称、字段号、类型和枚举数值，直接将枚举和摘要结构放在 Login/LoginRt 后。
- 使用现有 Grpc.Tools 构建生成 Login.cs；删除不再有 proto 来源的 RoleSummary.cs 和 UID，不手改生成内容。
- 当前没有推送，省略该分区；以后有推送时放在请求响应之后、辅助类型之前。

## Risks / Trade-offs

- [生成旧文件遗留或类型重复] → 删除旧生成物，构建并运行现有编解码及登录摘要验收。
- [反射所属文件改变] → 全仓库检索旧反射/来源文件引用，更新文档；消息字节和全名保持不变。
