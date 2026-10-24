## Why

用户提供的 item_export/index.html 包含具名道具及两级分类；现有图鉴仅按图片来源分类，不能按剑、法杖、项链等内容查找道具。

## What Changes

- 从同页 items.json 导入全部 21,792 条记录，复用 display_category 和 type_code/type_name，不自行推断分类。
- 默认显示道具清单的六个父类型及各自子类型，支持名称、ID、说明搜索与详情。
- 通过 icon_candidate 精确关联现有图集，保留缺图、重复 ID、来源行及分类确认程度。
- 原有 22,872 条图片来源浏览作为素材预览模式保留。

## Capabilities

### New Capabilities
无。

### Modified Capabilities
- `item-catalog`: 以具名道具的两级分类作为默认浏览方式，保留原素材预览及其交互契约。

## Impact

新增清单导入工具和静态道具数据模型，调整 ItemCatalogControl、交互验收、架构及路线图；复用现有图集，无业务服务或装备玩法变更。
