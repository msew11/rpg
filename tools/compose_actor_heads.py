"""按原始挂接点组合默认头部，生成供图集导入器读取的透明帧。"""

import argparse
import hashlib
import json
from pathlib import Path
import struct

from PIL import Image


# 原始创建角色界面 head=1 对应的男女首套头型，包含配套头发。
HEADS = {"男性": "head0101.gsn", "女性": "head0151.gsn"}


def read_header(data):
    """校验已支持的 DIMG 版本并返回画布与帧数。"""
    magic, tag, version, header, count, directions = struct.unpack_from("<6I", data)
    if (magic, tag, version, header, directions) != (0x1A465151, 0x474D4944, 0x10002, 36, 5):
        raise ValueError("不支持的 DIMG 头部")
    width, height = struct.unpack_from("<2i", data, header)
    return width, height, count


def frame_end(data, offset, width, height):
    """校验 ARGB4444 像素块边界，返回块尾。"""
    x, y, flags, w, h, stride, kind = struct.unpack_from("<7i", data, offset)
    end = offset + 28 + stride * h
    if not (0 <= x < width and 0 <= y < height and flags in (0, 256)
            and 0 < w <= width - x and 0 < h <= height - y
            and stride == w * 2 and kind == 1 and end + 12 <= len(data)):
        raise ValueError(f"无效像素块：{offset}")
    return end


def read_heads(path):
    """解码五方向默认头部，并读取各帧脖颈连接点。"""
    data = path.read_bytes()
    width, height, count = read_header(data)
    if count != 5:
        raise ValueError("默认头部必须每方向一帧")
    frames = []
    offset = 64
    while offset + 40 <= len(data):
        try:
            end = frame_end(data, offset, width, height)
        except ValueError:
            offset += 2
            continue
        x, y, _, w, h, _, _ = struct.unpack_from("<7i", data, offset)
        rgba = bytearray()
        for (pixel,) in struct.iter_unpack("<H", data[offset + 28:end]):
            rgba.extend((((pixel >> 8) & 15) * 17, ((pixel >> 4) & 15) * 17,
                         (pixel & 15) * 17, ((pixel >> 12) & 15) * 17))
        image = Image.new("RGBA", (width, height))
        image.paste(Image.frombytes("RGBA", (w, h), bytes(rgba)), (x, y))
        anchor_x, anchor_y = struct.unpack_from("<2i", data, end)
        frames.append((image, (x + anchor_x, y + anchor_y)))
        # 跳过像素块，后续染色副本不符合原画布坐标约束，不作为动画帧。
        offset = end
    if len(frames) != count:
        raise ValueError(f"头部解码帧数不符：{path}")
    return frames


def compose(source, raw, output):
    """按逐帧挂接点合成画布，并以同量平移原始世界锚点。"""
    output.mkdir(parents=True, exist_ok=True)
    # 中间 PNG 只供导入器读取，不参与 Godot 扫描与游戏导出。
    (output / ".gdignore").touch()
    animations = json.loads((source / "animation_frames.json").read_text(encoding="utf-8"))
    heads = {gender: read_heads(raw / "avatar/head" / name) for gender, name in HEADS.items()}
    records = []
    for animation in animations:
        data = (raw / "avatar/npc" / animation["source"]).read_bytes()
        width, height, count = read_header(data)
        if (width, height, count) != (animation["width"], animation["height"], len(animation["frames"])):
            raise ValueError("身体清单与原始素材不匹配")
        layers = []
        left, top, right, bottom = 0, 0, width, height
        for index, frame in enumerate(animation["frames"]):
            end = frame_end(data, frame["sourceOffset"], width, height)
            if index + 1 < count and animation["frames"][index + 1]["sourceOffset"] != end + 68:
                raise ValueError("身体挂接数据长度不符")
            # 挂接点相对裁剪块；加回裁剪偏移后才是身体画布坐标。
            attach_x, attach_y = struct.unpack_from("<2i", data, end + 12)
            crop_x, crop_y = struct.unpack_from("<2i", data, frame["sourceOffset"])
            head, (anchor_x, anchor_y) = heads[animation["gender"]][frame["direction"]]
            x, y = crop_x + attach_x - anchor_x, crop_y + attach_y - anchor_y
            layers.append((head, x, y))
            left, top = min(left, x), min(top, y)
            right, bottom = max(right, x + head.width), max(bottom, y + head.height)
        for frame, (head, x, y) in zip(animation["frames"], layers):
            if Path(frame["path"]).is_absolute() or ".." in Path(frame["path"]).parts:
                raise ValueError("帧路径必须为来源目录内的相对路径")
            source_frame = (source / frame["path"]).resolve()
            if not source_frame.is_relative_to(source.resolve()):
                raise ValueError("帧路径超出来源目录")
            with Image.open(source_frame) as body:
                if body.size != (width, height):
                    raise ValueError("身体 PNG 尺寸与清单不符")
                image = Image.new("RGBA", (right - left, bottom - top))
                image.alpha_composite(body.convert("RGBA"), (-left, -top))
            image.alpha_composite(head, (x - left, y - top))
            target = output / frame["path"]
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target)
        records.append({"gender": animation["gender"], "motion": animation["name"],
                        "body": animation["source"], "bodySha256": hashlib.sha256(data).hexdigest(),
                        "head": HEADS[animation["gender"]], "shift": [-left, -top],
                        "bodyAnchor": [animation["anchorX"], animation["anchorY"]],
                        "bodySize": [width, height], "frameCount": count})
        animation["width"], animation["height"] = right - left, bottom - top
        animation["anchorX"] -= left
        animation["anchorY"] -= top
    (output / "animation_frames.json").write_text(json.dumps(animations, ensure_ascii=False), encoding="utf-8")
    provenance = {"heads": {name: hashlib.sha256((raw / "avatar/head" / name).read_bytes()).hexdigest()
                            for name in HEADS.values()}, "animations": records}
    (output / "head_composition.json").write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    """接收外部只读来源与项目内中间输出目录。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--raw", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    source, raw, output = args.source.resolve(), args.raw.resolve(), args.output.resolve()
    project_build = Path(__file__).resolve().parents[1] / "build"
    if not output.is_relative_to(project_build) or output.is_relative_to(source) or output.is_relative_to(raw):
        raise ValueError("中间输出必须位于项目 build 目录且不能覆盖来源")
    compose(source, raw, output)


if __name__ == "__main__":
    main()
