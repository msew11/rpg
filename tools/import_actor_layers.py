"""导入独立身体动画、逐帧头部挂接点及全部男女头部；原始目录只读。"""

import argparse
import hashlib
import json
from pathlib import Path
import struct

from PIL import Image

from decode_actor_heads import frame_end, read_heads, read_header, preserve_source_pixels


def write_body(animation, source, raw, target):
    """按原始画布导入无头身体，并保存每帧的画布挂接坐标。"""
    gender = "male" if animation["gender"] == "男性" else "female"
    motion = animation["name"]
    width, height = animation["width"], animation["height"]
    count = animation["framesPerDirection"]
    data = (raw / "avatar/npc" / animation["source"]).read_bytes()
    if read_header(data) != (width, height, len(animation["frames"])):
        raise ValueError("身体清单与原始数据不一致")
    atlas = Image.new("RGBA", (width * count, height * 5))
    attachments = [None] * (count * 5)
    left_hands = [None] * (count * 5)
    right_hands = [None] * (count * 5)
    folder = target / gender
    folder.mkdir(parents=True, exist_ok=True)
    lines = [f'[gd_resource type="Resource" script_class="DataActorAnimation" load_steps={count * 5 + 4} format=3]',
             '[ext_resource type="Script" path="res://src/World/Views/DataActorAnimation.cs" id="1"]',
             f'[ext_resource type="Texture2D" path="res://assets/characters/{gender}/{motion}.png" id="2"]']
    for frame in animation["frames"]:
        direction, index = frame["direction"], frame["frame"]
        path = (source / frame["path"]).resolve()
        if not path.is_relative_to(source.resolve()):
            raise ValueError("帧路径超出来源目录")
        with Image.open(path) as image:
            if image.size != (width, height):
                raise ValueError("身体帧尺寸错误")
            atlas.paste(image, (index * width, direction * height))
        end = frame_end(data, frame["sourceOffset"], width, height)
        crop = struct.unpack_from("<2i", data, frame["sourceOffset"])
        attach = struct.unpack_from("<2i", data, end + 12)
        attachments[direction * count + index] = (crop[0] + attach[0], crop[1] + attach[1])
        # 源画布中两组手部连接点依次为左、右手；与源手原画一同整体镜像，不交换手别。
        for points, offset in [(left_hands, 28), (right_hands, 36)]:
            hand = struct.unpack_from("<2i", data, end + offset)
            points[direction * count + index] = (crop[0] + hand[0], crop[1] + hand[1])
        lines += [f'[sub_resource type="AtlasTexture" id="Frame_{direction}_{index}"]',
                  'atlas = ExtResource("2")', f'region = Rect2({index * width}, {direction * height}, {width}, {height})',
                  'filter_clip = false']
    if any(point is None for point in attachments):
        raise ValueError("身体挂接点不完整")
    atlas.save(folder / f"{motion}.png")
    preserve_source_pixels(folder / f"{motion}.png")
    lines += ['[sub_resource type="SpriteFrames" id="Animations"]', 'animations = [']
    for direction in range(5):
        frames = ', '.join('{"duration": 1.0, "texture": SubResource("Frame_' + f'{direction}_{index}' + '")}' for index in range(count))
        loop = 'true' if motion in ('stand', 'run') else 'false'
        lines.append('{"frames": [' + frames + '], "loop": ' + loop + f', "name": &"dir{direction}", "speed": 10.0' + '}' + (',' if direction < 4 else ''))
    points = ', '.join(str(value) for point in attachments for value in point)
    left_points = ', '.join(str(value) for point in left_hands for value in point)
    right_points = ', '.join(str(value) for point in right_hands for value in point)
    lines += [']', '[resource]', 'script = ExtResource("1")', 'Frames = SubResource("Animations")',
              f'Anchor = Vector2({animation["anchorX"]}, {animation["anchorY"]})',
              f'HeadAttachments = PackedVector2Array({points})',
              f'LeftHandAttachments = PackedVector2Array({left_points})',
              f'RightHandAttachments = PackedVector2Array({right_points})']
    (folder / f"{motion}.tres").write_text('\n'.join(lines) + '\n', encoding='utf-8')


def write_heads(raw, target):
    """按原始数字顺序给男女头型分配稳定编号，写入独立五方向资源。"""
    files = sorted((raw / 'avatar/head').glob('head*.gsn'))
    groups = {"male": [], "female": []}
    for path in files:
        number = int(path.stem[4:])
        groups['female' if number % 100 == 51 else 'male'].append(path)
    records = []
    for gender, paths in groups.items():
        if len(paths) != 74:
            raise ValueError(f'{gender} 头型数量应为 74')
        folder = target / 'heads' / gender
        folder.mkdir(parents=True, exist_ok=True)
        for index, path in enumerate(paths, 1):
            frames = read_heads(path)
            width, height = frames[0][0].size
            atlas = Image.new('RGBA', (width, height * 5))
            lines = ['[gd_resource type="Resource" script_class="DataActorHead" load_steps=8 format=3]',
                     '[ext_resource type="Script" path="res://src/World/Views/DataActorHead.cs" id="1"]',
                     f'[ext_resource type="Texture2D" path="res://assets/characters/heads/{gender}/{index}.png" id="2"]']
            for direction, (image, anchor) in enumerate(frames):
                atlas.paste(image, (0, height * direction))
                lines += [f'[sub_resource type="AtlasTexture" id="Dir{direction}"]', 'atlas = ExtResource("2")',
                          f'region = Rect2(0, {height * direction}, {width}, {height})', 'filter_clip = false']
            atlas.save(folder / f'{index}.png')
            preserve_source_pixels(folder / f'{index}.png')
            anchors = ', '.join(str(value) for _, anchor in frames for value in anchor)
            textures = ', '.join(f'SubResource("Dir{direction}")' for direction in range(5))
            lines += ['[resource]', 'script = ExtResource("1")', f'Frames = Array[Texture2D]([{textures}])',
                      f'Anchors = PackedVector2Array({anchors})']
            (folder / f'{index}.tres').write_text('\n'.join(lines) + '\n', encoding='utf-8')
            records.append({'gender': gender, 'id': index, 'source': path.name,
                            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    return records


def main():
    """从外部只读数据重建项目内所有分层资源和来源清单。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--raw', type=Path, required=True)
    args = parser.parse_args()
    target = Path(__file__).resolve().parents[1] / 'assets/characters'
    animations = json.loads((args.source / 'animation_frames.json').read_text(encoding='utf-8'))
    if len(animations) != 26 or sum(len(animation['frames']) for animation in animations) != 605:
        raise ValueError('需要男女 26 个动作及 605 帧')
    for animation in animations:
        write_body(animation, args.source, args.raw, target)
    heads = write_heads(args.raw, target)
    (target / 'head_composition.json').write_text(json.dumps({'layout': 'separate-layers', 'heads': heads},
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('已导入 26 个独立身体动作、605 帧挂接点和 148 套头部。')


if __name__ == '__main__':
    main()
