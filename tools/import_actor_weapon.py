"""从只读DIMG导入完整武器、原生动作类别及男女完整角色合成表。"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from PIL import Image

from decode_actor_heads import frame_end, read_header, preserve_source_pixels


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


# 动作顺序与RoleMotion一致；值为原客户端资源动作编号。
ACTIONS = dict(zip(('stand', 'run', 'sit', 'die', 'attack1', 'attack2', 'attack3',
                    'magic', 'prepare1', 'prepare2', 'prepare3', 'hurt', 'ride'), range(1, 14)))
# 经源图标和客户端动作类别核对的测试装备系列。
BINDINGS = {1012: ('weaponr', 1, 4), 1013: ('weaponr', 41, 4), 1014: ('weaponl', 11, 0),
            1015: ('weaponr', 21, 5), 1016: ('weaponr', 31, 5),
            1017: ('weaponr', 51, 6), 1018: ('weaponl', 105, 6)}


def import_hand(raw, root, item_id, hand, series, attack, variant=None, subfolder=''):
    """导入完整原生纹理；缺失动作入口共享站立纹理，保留实际来源编号。"""
    records = []
    for gender in ('male', 'female'):
        folder = root / str(item_id) / subfolder / gender
        folder.mkdir(parents=True, exist_ok=True)
        for motion, action in ACTIONS.items():
            number = series * 100 + action + (50 if gender == 'female' else 0)
            source = raw / 'avatar' / hand / f'{hand}{number:04}.gsn'
            if not source.exists():
                action = 1
                source = raw / 'avatar' / hand / f'{hand}{series * 100 + action + (50 if gender == "female" else 0):04}.gsn'
            frames = decode_weapon(source)
            count = len(frames) // 5
            width, height = frames[0][0].size
            texture_motion = motion if action != 1 else 'stand'
            if texture_motion == motion:
                atlas = Image.new('RGBA', (width * count, height * 5))
                for index, (image, _) in enumerate(frames):
                    direction, frame = divmod(index, count)
                    atlas.paste(image, (frame * width, direction * height))
                atlas.save(folder / f'{motion}.png')
                preserve_source_pixels(folder / f'{motion}.png')
            resource_folder = folder.relative_to(root).as_posix()
            lines = [f'[gd_resource type="Resource" script_class="DataActorWeapon" load_steps={len(frames)+3+int(variant is not None)} format=3]',
                     '[ext_resource type="Script" path="res://src/World/Views/DataActorWeapon.cs" id="1"]',
                     f'[ext_resource type="Texture2D" path="res://assets/characters/weapons/{resource_folder}/{texture_motion}.png" id="2"]']
            if variant:
                lines += [f'[ext_resource type="Resource" path="res://assets/characters/weapons/{item_id}/{variant}/{gender}/{motion}.tres" id="3"]']
            for index in range(len(frames)):
                direction, frame = divmod(index, count)
                lines += [f'[sub_resource type="AtlasTexture" id="Frame{index}"]', 'atlas = ExtResource("2")',
                          f'region = Rect2({frame*width}, {direction*height}, {width}, {height})', 'filter_clip = false']
            textures = ', '.join(f'SubResource("Frame{index}")' for index in range(len(frames)))
            anchors = ', '.join(str(value) for _, point in frames for value in point)
            lines += ['[resource]', 'script = ExtResource("1")', f'SourceRightHand = {str(hand == "weaponr").lower()}',
                      f'SourceAction = {action}', f'AttackMotion = {attack}', f'Frames = Array[Texture2D]([{textures}])',
                      f'Anchors = PackedVector2Array({anchors})']
            if variant:
                lines += [f'{variant.capitalize()}HandVariant = ExtResource("3")']
            (folder / f'{motion}.tres').write_text('\n'.join(lines)+'\n', encoding='utf-8')
            records.append(dict(gender=gender, motion=motion, source=source.relative_to(raw).as_posix(),
                                sha256=hashlib.sha256(source.read_bytes()).hexdigest(), sourceAction=action,
                                framesPerDirection=count))
    return records


def write_composition(raw, target):
    """导入完整男女身体动作层次，不随武器缺失动作的站立回退而改变。"""
    parts = [('npc', 'Npc.ini'), ('head', 'Head.ini'), ('weaponl', 'Weaponl.ini'), ('weaponr', 'Weaponr.ini')]
    tables = [read_layers(raw / 'avatar' / part / filename) for part, filename in parts]
    layers = [value for suffix in ('', 'g') for motion in ACTIONS for table in tables
              for row in table[motion + suffix] for value in row]
    # 原客户端0x5FFD60，原方向顺序为SW/W/NW/N/NE/E/SE/S，过滤到身体、头、副手、主手。
    orders = [[3, 0, 1, 2]]*3 + [[3, 2, 0, 1]] + [[3, 0, 1, 2]]*3 + [[0, 1, 3, 2]]
    text = ['[gd_resource type="Resource" script_class="DataActorComposition" load_steps=2 format=3]',
            '[ext_resource type="Script" path="res://src/World/Views/DataActorComposition.cs" id="1"]',
            '[resource]', 'script = ExtResource("1")',
            'BaseOrder = PackedInt32Array('+', '.join(str(v) for row in orders for v in row)+')',
            'Layers = PackedInt32Array('+', '.join(map(str, layers))+')']
    target.write_text('\n'.join(text)+'\n', encoding='utf-8')
    evidence = {str(Path('avatar') / part / name): hashlib.sha256((raw / 'avatar' / part / name).read_bytes()).hexdigest()
                for part, name in parts}
    target.with_suffix('.json').write_text(json.dumps(dict(layerSources=evidence, orderingEvidence='docs/source-actor-composition.md'), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def main():
    """从只读来源重建七件装备与原客户端完整角色合成表。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1] / 'assets/characters/weapons'
    item_data = (args.raw / 'zipbin/ov_arm.bin').read_bytes()
    _, _, row_size, row_count = struct.unpack_from('<4I', item_data)
    if len(item_data) != 16 + row_size * row_count:
        raise ValueError('原始装备表长度不符')
    for item_id, (hand, series, attack) in BINDINGS.items():
        other = 'weaponl' if hand == 'weaponr' else 'weaponr'
        variant = ('left' if hand == 'weaponr' else 'right') if (args.raw / 'avatar' / other / f'{other}{series*100+1:04}.gsn').exists() else None
        records = import_hand(args.raw, root, item_id, hand, series, attack, variant)
        evidence = dict(itemId=item_id, binding=f'{hand}{series:02}', nativeHand='right' if hand=='weaponr' else 'left',
                        attackMotion=list(ACTIONS)[attack] if attack else None, animations=records,
                        bindingNote='完整原生像素及挂点，整体镜像不换源手；缺少动作回退站立', oppositeHandAvailable=variant is not None)
        if variant:
            evidence[f'{variant}HandVariant'] = dict(nativeHand=variant, binding=f'{other}{series:02}',
                animations=import_hand(args.raw, root, item_id, other, series, attack, subfolder=variant))
        rows = [(index + 1, item_data[16 + index*row_size:16 + (index+1)*row_size])
                for index in range(row_count) if struct.unpack_from('<I', item_data, 16 + index*row_size)[0] == item_id]
        if len(rows) != 1:
            raise ValueError(f'原始装备表记录不唯一：{item_id}')
        evidence.update(itemSource='zipbin/ov_arm.bin', itemSourceRow=rows[0][0], itemRowHex=rows[0][1].hex(),
                        itemSourceSha256=hashlib.sha256(item_data).hexdigest())
        (root / str(item_id) / 'source.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        print(f'{item_id}: native attack={attack}, {len(records)} motion entrypoints')
    write_composition(args.raw, root.parent / 'composition.tres')
    # 仅清理该生成目录内已不再被资源引用的派生图集和导入描述，不接触来源目录。
    referenced = {match for resource in root.rglob('*.tres') for match in re.findall(r'path="res://([^"\n]+\.png)"', resource.read_text(encoding='utf-8'))}
    project = root.parents[2]
    for texture in root.rglob('*.png'):
        if texture.relative_to(project).as_posix() not in referenced:
            texture.unlink()
            texture.with_suffix('.png.import').unlink(missing_ok=True)


if __name__ == '__main__':
    main()
