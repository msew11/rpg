# Design

## Context

红剑当前位于怪物目录，但其他鼠标素材统一位于 assets/pointer。圆环源画布 69×43，树妖画布宽 139，因此当前放大到约 139×87；Nearest 会扩大像素台阶。已从 GSO 独立重解码并核对六帧与项目 PNG 完全一致。

## Goals / Non-Goals

归一鼠标资源路径并减轻圆环锯齿，不重绘或重采样源 PNG，不改变选择、动画或命中行为。

## Decisions

- 剑形 PNG、import 描述和来源条目归入 assets/pointer，统一导入器加入 AttackH；怪物导入器只处理圆环，避免重复维护鼠标来源。
- 圆环 AnimatedSprite2D 使用 Linear，覆盖父级 Nearest；保持最近邻的怪物贴图与鼠标。沿用原画布、帧序、锚点、尺寸及播放参数。
- 既有专项仅更新剑形素材路径，运行真实键鼠场景与 Windows 图形模式目视核对；不为单个过滤参数添加重复实现的断言。

## Risks / Trade-offs

原素材本身分辨率低且 alpha 为 4 位，平滑过滤只能缓和台阶，不能增加原始细节；保留源 PNG 可随时调整显示方式。
