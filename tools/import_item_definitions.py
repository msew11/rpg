"""导入 item_export 道具清单的现有分类，精确关联项目内图集，不推断装备类型。"""

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

# 与用户指定页面的父类型及显示顺序保持一致。
PARENT_CATEGORIES = ["武器", "防具／饰品", "其他装备", "宠物", "收集品", "消耗品"]


def import_definitions(source: Path, destination: Path):
    """逐行保留具名记录及分类证据，来源表和行号共同确定唯一键。"""
    source_file = source / "items.json"
    source_bytes = source_file.read_bytes()
    records = json.loads(source_bytes)
    assets = json.loads((destination / "catalog.json").read_text(encoding="utf-8"))
    asset_by_source = {asset["Source"]: asset["Id"] for asset in assets}
    definitions = []
    keys = set()
    for record in records:
        key = f"{record['source']}:{record['source_row']}"
        if key in keys:
            raise ValueError(f"来源记录键重复：{key}")
        keys.add(key)
        category = record["display_category"]
        if category not in PARENT_CATEGORIES:
            raise ValueError(f"未知父类型：{category}")
        candidate = record["icon_candidate"]
        icon_id = ""
        if candidate:
            if not candidate.startswith("../art_export/"):
                raise ValueError(f"未知候选图标路径：{candidate}")
            icon_id = asset_by_source[candidate.removeprefix("../art_export/")]
        definitions.append({
            "Key": key, "ItemId": record["id"], "Name": record["name"],
            "Category": category, "CategoryStatus": record["category_status"],
            "InventoryType": record["inventory_type"], "TypeCode": record["type_code"],
            "TypeName": record["type_name"], "TypeStatus": record["type_status"],
            "Description": record["description"], "Source": record["source"],
            "SourceRow": record["source_row"], "IconAssetId": icon_id,
            "IconCandidate": candidate, "DescriptionMatch": record["description_match"],
            "DescriptionCandidates": record["description_candidates"],
        })
    # 先完成全部验证，再写出运行资源，避免输入错误留下半份清单。
    (destination / "definitions.json").write_text(
        json.dumps(definitions, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    evidence = {
        "Source": "item_export/items.json",
        "Sha256": hashlib.sha256(source_bytes).hexdigest(),
        "Count": len(definitions),
        "CategoryCounts": dict(Counter(item["Category"] for item in definitions)),
        "MissingIcons": sum(not item["IconAssetId"] for item in definitions),
        "SourceFiles": {name: hashlib.sha256((source / name).read_bytes()).hexdigest()
                        for name in ["index.html", "README.md", "summary.json", "classification_evidence.txt"]},
    }
    (destination / "definitions_source.json").write_text(
        json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"Count": evidence["Count"], "CategoryCounts": evidence["CategoryCounts"],
                      "MissingIcons": evidence["MissingIcons"]}, ensure_ascii=True))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="item_export 目录")
    parser.add_argument("--destination", type=Path,
                        default=Path(__file__).resolve().parents[1] / "assets/items")
    args = parser.parse_args()
    import_definitions(args.source, args.destination)
