# Proposal

## Why

用户指出鼠标资源应统一放在 pointer，圆环像素感偏重，并要求确认是否忠于原图。核对原始 GSO 后确认六帧逐像素一致；最近邻放大约 2.01 倍放大了像素台阶。

## What Changes

- 红剑资源与来源记录迁回 assets/pointer，由统一鼠标导入器管理。
- 仅圆环采用线性过滤，保留原始 PNG、尺寸、动画顺序与脚底锚点。
- 核对原始素材、现有交互与图形结果，更新架构和路线图。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。现有 monster-selection 已规定原版六帧、红剑、缩放及输入契约；本次仅修正资源归属与显示过滤参数，skip_specs 不修改行为契约。

## Impact

PointerCursor、MonstersView、两份资源导入器及来源清单、既有场景资源路径与文档。协议和选中交互保持现有规格。
