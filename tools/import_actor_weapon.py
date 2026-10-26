"""从只读 DIMG 导入七件测试装备的男女帧、持握连接点和逐帧遮挡。"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from PIL import Image

from decode_actor_heads import frame_end, read_header


def decode_weapon(path):
    """只提取原始画布内像素块，跳过染色副本并校验完整五方向帧数。"""
    data = path.read_bytes()
    width, height, count = read_header(data)
    frames = []
    offset = 64
    while offset + 40 <= len(data):
        try:
            end = frame_end(data, offset, width, height)
        except ValueError:
            offset += 2
            continue
        x, y, _, w, h, _, _ = struct.unpack_from('<7i', data, offset)
        rgba = bytearray()
        for (pixel,) in struct.iter_unpack('<H', data[offset + 28:end]):
            rgba.extend((((pixel >> 8) & 15) * 17, ((pixel >> 4) & 15) * 17,
                         (pixel & 15) * 17, ((pixel >> 12) & 15) * 17))
        image = Image.new('RGBA', (width, height))
        image.paste(Image.frombytes('RGBA', (w, h), bytes(rgba)), (x, y))
        anchor = struct.unpack_from('<2i', data, end)
        frames.append((image, (x + anchor[0], y + anchor[1])))
        offset = end
    if len(frames) != count or count % 5:
        raise ValueError(f'武器帧数不符：{path}: {len(frames)} != {count}')
    return frames


def read_layers(path):
    """读取各动作方向的 50 槽遮挡表；首项为槽数量，后续为每帧相对层级。"""
    sections = re.split(r'\[([^]]+)\]', path.read_text(encoding='ascii'))
    layers = {}
    for name, content in zip(sections[1::2], sections[2::2]):
        directions = re.findall(r'\(dir[1-5]\)\s*([\d,\-]+)', content)
        if len(directions) != 5:
            raise ValueError(f'武器遮挡方向不完整：{name}')
        layers[name] = []
        for row in directions:
            values = [int(value) for value in row.split(',')]
            if values[0] != 50 or len(values) != 51:
                raise ValueError('武器遮挡表必须为 50 槽')
            layers[name].append(values[1:])
    return layers


def write_weapon(gender, motion, action, raw, target, layers, item_id=1012, hand="weaponr", series=1):
    """按来源动作导入一次武器图集，并保留每帧独立连接点及层级。"""
    number = series * 100 + action + (50 if gender == 'female' else 0)
    source = raw / 'avatar' / hand / f'{hand}{number:04}.gsn'
    frames = decode_weapon(source)
    count = len(frames) // 5
    width, height = frames[0][0].size
    atlas = Image.new('RGBA', (width * count, height * 5))
    lines = [f'[gd_resource type="Resource" script_class="DataActorWeapon" load_steps={len(frames) + 3} format=3]',
             '[ext_resource type="Script" path="res://src/World/Views/DataActorWeapon.cs" id="1"]',
             f'[ext_resource type="Texture2D" path="res://assets/characters/weapons/{item_id}/{gender}/{motion}.png" id="2"]']
    for index, (image, _) in enumerate(frames):
        direction, frame = divmod(index, count)
        atlas.paste(image, (frame * width, direction * height))
        lines += [f'[sub_resource type="AtlasTexture" id="Frame{index}"]', 'atlas = ExtResource("2")',
                  f'region = Rect2({frame * width}, {direction * height}, {width}, {height})', 'filter_clip = true']
    folder = target / gender
    folder.mkdir(parents=True, exist_ok=True)
    atlas.save(folder / f'{motion}.png')
    textures = ', '.join(f'SubResource("Frame{index}")' for index in range(len(frames)))
    anchors = ', '.join(str(value) for _, anchor in frames for value in anchor)
    z_indices = ', '.join(str(layers[motion][direction][frame]) for direction in range(5) for frame in range(count))
    lines += ['[resource]', 'script = ExtResource("1")', f'Frames = Array[Texture2D]([{textures}])',
              f'Anchors = PackedVector2Array({anchors})', f'Layers = PackedInt32Array({z_indices})']
    (folder / f'{motion}.tres').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    return {'gender': gender, 'motion': motion, 'source': source.relative_to(raw).as_posix(),
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'framesPerDirection': count}


def main():
    """按明确系列绑定重建七件装备，缺失源动作复用站立并记录证据。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1] / 'assets/characters/weapons'
    # 已核对源图形：基础剑、长剑、盾、木杖、枪、短刃、飞刀；编号不从物品主键推导。
    bindings = {1012: ('weaponr', 1), 1013: ('weaponr', 9), 1014: ('weaponl', 11),
                1015: ('weaponr', 21), 1016: ('weaponr', 31), 1017: ('weaponr', 41),
                1018: ('weaponl', 105)}
    actions = {'stand': 1, 'run': 2, 'sit': 3, 'attack1': 5, 'magic': 8, 'prepare1': 9, 'hurt': 12}
    item_data = (args.raw / 'zipbin/ov_arm.bin').read_bytes()
    _, _, row_size, row_count = struct.unpack_from('<4I', item_data)
    if len(item_data) != 16 + row_size * row_count:
        raise ValueError('原始装备表长度不符')
    for item_id, (hand, series) in bindings.items():
        target = root / str(item_id)
        layer_file = args.raw / 'avatar' / hand / (hand.capitalize() + '.ini')
        layers = read_layers(layer_file)
        records = []
        for gender in ['male', 'female']:
            for motion, action in actions.items():
                number = series * 100 + action + (50 if gender == 'female' else 0)
                source = args.raw / 'avatar' / hand / f'{hand}{number:04}.gsn'
                selected = action
                if not source.exists():
                    selected = {'attack1': 7, 'prepare1': 11}.get(motion, 1) if hand == 'weaponl' else 1
                if not (args.raw / 'avatar' / hand / f'{hand}{series * 100 + selected + (50 if gender == 'female' else 0):04}.gsn').exists():
                    selected = 1
                record = write_weapon(gender, motion, selected, args.raw, target, layers, item_id, hand, series)
                record['sourceAction'] = selected
                records.append(record)
        evidence = {'itemId': item_id, 'binding': f'{hand}{series:02}',
                    'bindingNote': '项目显式选取同类基础持握系列；物品编号不等于源素材编号。副手拳刃复用右手原画。',
                    'motionReuse': {'die': 'sit', 'attack2': 'attack1', 'attack3': 'attack1',
                                    'prepare2': 'prepare1', 'prepare3': 'prepare1', 'ride': 'stand'},
                    'layerSource': layer_file.relative_to(args.raw).as_posix(),
                    'layerSha256': hashlib.sha256(layer_file.read_bytes()).hexdigest(), 'animations': records}
        matches = [(index + 1, item_data[16 + index * row_size:16 + (index + 1) * row_size])
                   for index in range(row_count) if struct.unpack_from('<I', item_data, 16 + index * row_size)[0] == item_id]
        if len(matches) != 1:
            raise ValueError(f'原始装备表记录不唯一：{item_id}')
        evidence.update({'itemSource': 'zipbin/ov_arm.bin', 'itemSourceRow': matches[0][0],
                         'itemSourceSha256': hashlib.sha256(item_data).hexdigest(), 'itemRowHex': matches[0][1].hex()})
        (target / 'source.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(f'{item_id}: {len(records)} sets, {sum(r["framesPerDirection"] * 5 for r in records)} frames')


if __name__ == '__main__':
    main()
