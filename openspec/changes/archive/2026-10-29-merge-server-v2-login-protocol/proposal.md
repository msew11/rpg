# Proposal

## Why

用户要求将 role_summary.proto 并入 login.proto，并统一该文件的声明顺序。

## What Changes

- 将 RoleGenderPb、RoleSummaryPb 移至 protov2/login.proto，移除 role_summary.proto 及其 import。
- login.proto 按请求响应、推送、enum 与结构排列；当前没有推送，不添加新消息。
- 重新生成 C# 并删除旧摘要生成文件，更新 V2 协议组织文档。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。仅调整协议来源文件，消息全名、字段号、枚举值与收发行为保持原有契约，使用 skip_specs。

## Impact

V2 proto 源文件、生成代码及协议组织说明。消息反射的来源文件改为 login.proto，线上字节格式不变。
