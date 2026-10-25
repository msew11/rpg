# Design

## Context

见 proposal.md。现有图集已包含两套素材，GroundItemsView 使用 IconAssetId 并按 alpha 命中。

## Goals / Non-Goals

只修改客户端表现和配置，权威位置、拾取距离和共同落地保持现有边界。

## Decisions

- GroundAssetId 明确绑定导出小图编号，避免从业务 ID 或大图名称推算；保留资源解析器共用，不新增服务。
- 命中矩形为 32×32 显示框向外扩展 8 像素，名称继续命中。轮廓仍用 alpha 膨胀，命中和轮廓解耦。
- 省略地面素材使用既有问号占位，避免悄然复用错误大图。重叠时保留原最上层优先。

## Risks / Trade-offs

扩大区域可能重叠 → 保留可预测的最上层优先与 UI 遮挡。

## Migration Plan

配置随程序升级，仅增加可选字段，无存档迁移；回滚本提交即可恢复。
