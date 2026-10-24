"""将已解码道具和武器首帧打包为原像素图集；依赖 Pillow。"""

import argparse
import hashlib
import json
import re
from pathlib import Path

from PIL import Image

# 来源目录是唯一分类依据，不推断道具名称与玩法类型。
CATEGORIES = {
    "item/item": "道具小图标",
    "item/itempic": "道具大图",
    "avatar/weaponl": "左手武器部件",
    "avatar/weaponr": "右手武器部件",
}
ATLAS_SIZE = 2048


def import_catalog(source: Path, destination: Path):
    """按稳定数字顺序打包全部图片，保留溯源数据并验证源目录存在。"""
    for folder in CATEGORIES:
        if not (source / "png" / folder).is_dir():
            raise ValueError(f"缺少素材目录：{folder}")
    destination.mkdir(parents=True, exist_ok=True)
    entries = []
    atlas = Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE))
    index = x = y = row_height = 0

    def save_atlas():
        """写出已填充的图集，统一使用无损 PNG。"""
        atlas.save(destination / f"atlas_{index:03}.png", optimize=True)

    for folder, category in CATEGORIES.items():
        files = sorted((source / "png" / folder).glob("*.png"),
                       key=lambda p: (int(re.search(r"\d+", p.stem)[0]), p.name))
        for file in files:
            with Image.open(file) as original:
                image = original.convert("RGBA")
            size = list(image.size)
            bounds = image.getbbox() or (0, 0, 1, 1)
            cropped = image.crop(bounds)
            width, height = cropped.size
            if width + 2 > ATLAS_SIZE or height + 2 > ATLAS_SIZE:
                raise ValueError(f"素材超出图集尺寸：{file}")
            if x + width + 2 > ATLAS_SIZE:
                x = 0
                y += row_height
                row_height = 0
            if y + height + 2 > ATLAS_SIZE:
                save_atlas()
                index += 1
                atlas = Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE))
                x = y = row_height = 0
            atlas.paste(cropped, (x + 1, y + 1))
            entries.append({
                "Id": f"{folder}/{file.stem}", "Name": file.stem,
                "Category": category, "Source": f"png/{folder}/{file.name}",
                "Sha256": hashlib.sha256(file.read_bytes()).hexdigest(),
                "Width": size[0], "Height": size[1],
                "CropX": bounds[0], "CropY": bounds[1],
                "Atlas": f"res://assets/items/atlas_{index:03}.png",
                "X": x + 1, "Y": y + 1, "W": width, "H": height,
            })
            x += width + 2
            row_height = max(row_height, height + 2)
    save_atlas()
    (destination / "catalog.json").write_text(
        json.dumps(entries, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    for category in CATEGORIES.values():
        print(category, sum(entry["Category"] == category for entry in entries))
    print(f"共 {len(entries)} 条素材，{index + 1} 张图集")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="art_export 目录")
    parser.add_argument("--destination", type=Path,
                        default=Path(__file__).resolve().parents[1] / "assets/items")
    args = parser.parse_args()
    import_catalog(args.source, args.destination)
