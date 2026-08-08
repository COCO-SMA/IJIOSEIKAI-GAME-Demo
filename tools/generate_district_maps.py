"""
Generate pixel-art district maps for Kuncheng RPG.
Outputs: src/data/districts/jinyong.json, src/data/districts/jiuxu.json
Uses the city tileset tile IDs:
0 grass, 1 wall, 2 brick bldg, 3 water, 4 road, 5 sidewalk, 6 plaza, 7 floor,
8 tree, 9 bush, 10 door, 11 window, 12 roof, 13 streetlight, 14 planter, 15 bridge,
16 sand, 17 parking, 18 fence, 19 trashbin, 20 glass bldg, 21 cement bldg,
22 road_h, 23 road_v
"""
import json
import random

WIDTH = 30
HEIGHT = 20


def new_map():
    return [[0 for _ in range(WIDTH)] for _ in range(HEIGHT)]


def fill_border(tiles, tile=1):
    for x in range(WIDTH):
        tiles[0][x] = tile
        tiles[HEIGHT - 1][x] = tile
    for y in range(HEIGHT):
        tiles[y][0] = tile
        tiles[y][WIDTH - 1] = tile


def fill_rect(tiles, x, y, w, h, tile):
    for dy in range(h):
        for dx in range(w):
            tiles[y + dy][x + dx] = tile


def fill_h_line(tiles, x, y, length, tile):
    for dx in range(length):
        tiles[y][x + dx] = tile


def fill_v_line(tiles, x, y, length, tile):
    for dy in range(length):
        tiles[y + dy][x] = tile


def is_inside(x, y):
    return 0 <= x < WIDTH and 0 <= y < HEIGHT


def draw_h_road(tiles, x, y, length):
    fill_h_line(tiles, x, y, length, 22)


def draw_v_road(tiles, x, y, length):
    fill_v_line(tiles, x, y, length, 23)


def draw_road_intersection(tiles, x, y):
    tiles[y][x] = 4


def draw_sidewalk_around_road(tiles, road_tiles):
    # Expand a 1-tile wide buffer of sidewalk around all road tiles
    for y in range(HEIGHT):
        for x in range(WIDTH):
            if tiles[y][x] in (4, 22, 23):
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if is_inside(nx, ny) and tiles[ny][nx] == 0:
                            # Don't overwrite other roads
                            if not (dx == 0 and dy == 0):
                                tiles[ny][nx] = 5


def draw_building(tiles, x, y, w, h, wall_tile=2, roof=True, windows=True, door=True, door_x=None):
    """Draw a rectangular building with walls, optional roof strip, windows, door."""
    fill_rect(tiles, x, y, w, h, wall_tile)
    if roof and y > 0:
        fill_h_line(tiles, x, y, w, 12)
        # Move the top row of walls down one
        if h > 1:
            fill_rect(tiles, x, y + 1, w, h - 1, wall_tile)
    if windows and h >= 3 and w >= 3:
        for wy in range(y + 1, y + h):
            for wx in range(x + 1, x + w - 1):
                if (wx - x) % 2 == 1 and (wy - y) % 2 == 1:
                    tiles[wy][wx] = 11
    if door and h >= 3:
        if door_x is None:
            door_x = x + w // 2
        if y + h - 1 < HEIGHT:
            tiles[y + h - 1][door_x] = 10


def place_trees(tiles, count, seed=0):
    random.seed(seed)
    placed = 0
    attempts = 0
    while placed < count and attempts < count * 20:
        attempts += 1
        x = random.randint(1, WIDTH - 2)
        y = random.randint(1, HEIGHT - 2)
        if tiles[y][x] == 0:
            tiles[y][x] = 8
            placed += 1


def place_streetlights(tiles, count, seed=0):
    random.seed(seed)
    placed = 0
    attempts = 0
    while placed < count and attempts < count * 20:
        attempts += 1
        x = random.randint(1, WIDTH - 2)
        y = random.randint(1, HEIGHT - 2)
        if tiles[y][x] == 5:
            tiles[y][x] = 13
            placed += 1


def place_planters(tiles, count, seed=0):
    random.seed(seed)
    placed = 0
    attempts = 0
    while placed < count and attempts < count * 20:
        attempts += 1
        x = random.randint(1, WIDTH - 2)
        y = random.randint(1, HEIGHT - 2)
        if tiles[y][x] == 5:
            tiles[y][x] = 14
            placed += 1


def place_trashbins(tiles, count, seed=0):
    random.seed(seed)
    placed = 0
    attempts = 0
    while placed < count and attempts < count * 20:
        attempts += 1
        x = random.randint(1, WIDTH - 2)
        y = random.randint(1, HEIGHT - 2)
        if tiles[y][x] == 5:
            tiles[y][x] = 19
            placed += 1


def make_jinyong():
    tiles = new_map()
    fill_border(tiles, 1)

    # Main roads
    draw_h_road(tiles, 1, 10, WIDTH - 2)   # horizontal central road
    draw_v_road(tiles, 15, 1, HEIGHT - 2)   # vertical central road
    draw_road_intersection(tiles, 15, 10)

    # Ring sidewalk around central roads
    for y in range(1, HEIGHT - 1):
        for x in range(1, WIDTH - 1):
            if tiles[y][x] in (4, 22, 23):
                for dy in (-1, 1):
                    ny = y + dy
                    if is_inside(x, ny) and tiles[ny][x] == 0:
                        tiles[ny][x] = 5
                for dx in (-1, 1):
                    nx = x + dx
                    if is_inside(nx, y) and tiles[y][nx] == 0:
                        tiles[y][nx] = 5

    # Quarter 1: top-left (civic center / government plaza)
    fill_rect(tiles, 2, 2, 12, 7, 6)  # large plaza
    draw_building(tiles, 3, 2, 4, 4, wall_tile=21, roof=True, door_x=4)  # cement civic building
    draw_building(tiles, 9, 2, 4, 4, wall_tile=21, roof=True, door_x=10)
    place_trees(tiles, 3, seed=101)

    # Quarter 2: top-right (CBD / glass towers)
    fill_rect(tiles, 17, 2, 11, 7, 5)  # sidewalk grid
    draw_building(tiles, 18, 2, 3, 5, wall_tile=20, roof=True, door=False)
    draw_building(tiles, 22, 2, 3, 5, wall_tile=20, roof=True, door=False)
    draw_building(tiles, 18, 8, 3, 1, wall_tile=20, roof=False, door=True)  # small lobby strip
    place_streetlights(tiles, 4, seed=102)

    # Quarter 3: bottom-left (commercial / brick + trees)
    fill_rect(tiles, 2, 12, 12, 6, 0)
    draw_building(tiles, 2, 12, 4, 4, wall_tile=2, roof=True, door_x=3)
    draw_building(tiles, 7, 12, 4, 4, wall_tile=2, roof=True, door_x=8)
    draw_building(tiles, 2, 17, 3, 1, wall_tile=2, roof=False, door=True)
    place_trees(tiles, 5, seed=103)

    # Quarter 4: bottom-right (residential / mixed + small pond)
    fill_rect(tiles, 17, 12, 11, 6, 0)
    draw_building(tiles, 18, 13, 4, 4, wall_tile=21, roof=True, door_x=19)
    draw_building(tiles, 23, 13, 4, 4, wall_tile=2, roof=True, door_x=24)
    # Small pond
    fill_rect(tiles, 20, 16, 4, 2, 3)
    tiles[17][22] = 15  # bridge over pond
    place_trees(tiles, 4, seed=104)

    # Some random fences/parking near edges
    tiles[11][1] = 18
    tiles[12][1] = 18
    tiles[13][1] = 18
    tiles[18][28] = 17  # parking slot

    # Ensure exits are walkable
    tiles[10][1] = 5   # left exit
    tiles[10][28] = 5  # right exit
    tiles[1][15] = 5   # top exit

    # Ensure NPC spots are walkable and clearly visible
    jinyong_npcs = [{"x": 12, "y": 9}, {"x": 8, "y": 12}, {"x": 20, "y": 13}]
    for npc in jinyong_npcs:
        if tiles[npc["y"]][npc["x"]] in (1, 2, 3, 8, 9, 11, 12, 13, 14, 18, 19, 20, 21):
            tiles[npc["y"]][npc["x"]] = 5

    # Place street decorations
    place_planters(tiles, 4, seed=105)
    place_trashbins(tiles, 4, seed=106)

    data = {
        "id": "jinyong",
        "name": "Jinyong",
        "anomalyType": "authority_anomaly",
        "anomalyDescription": "certain rules actually get enforced here. including the unwritten ones.",
        "width": WIDTH,
        "height": HEIGHT,
        "tiles": tiles,
        "subDistricts": [
            {"id": "zhonghuan", "name": "Zhonghuan", "desc": "the CBD. glass towers. people in suits walking fast."},
            {"id": "zhengxin", "name": "Zhengxin", "desc": "civic center. where forms get stamped. or don't."},
            {"id": "dianjie", "name": "Dianjie", "desc": "electronics street. everything has a price. some have two."},
            {"id": "guankou", "name": "Guankou", "desc": "the checkpoint. crossing means rules change."},
            {"id": "miaodun", "name": "Miaodun", "desc": "old temple area. commuters rush past something old."}
        ],
        "npcs": [
            {"id": "lin_jie", "name": "Sister Lin", "x": 12, "y": 9, "dialogueId": "lin_jie_01"},
            {"id": "principal_huang", "name": "Principal Huang", "x": 8, "y": 12, "dialogueId": "huang_01"},
            {"id": "lao_wu", "name": "Old Wu", "x": 20, "y": 13, "dialogueId": "lao_wu_01"}
        ],
        "exits": [
            {"type": "district", "target": "yundun", "x": 1, "y": 10},
            {"type": "district", "target": "jiuxu", "x": 28, "y": 10},
            {"type": "district", "target": "zhongling", "x": 15, "y": 1}
        ],
        "points": [
            {"id": "zhonghuan_poi", "name": "Zhonghuan CBD", "x": 22, "y": 4, "type": "landmark"},
            {"id": "zhengxin_poi", "name": "Civic Center", "x": 6, "y": 5, "type": "landmark"},
            {"id": "dianjie_poi", "name": "Electronics Street", "x": 22, "y": 14, "type": "landmark"}
        ],
        "music": "jinyong_bgm",
        "atmosphere": "glass towers, suited people walking fast, something about the air feels enforceable"
    }
    return data


def make_jiuxu():
    tiles = new_map()
    fill_border(tiles, 1)

    # Old city: winding alleys, central market plaza, dense brick buildings
    # Create a rough alley network
    draw_h_road(tiles, 1, 6, WIDTH - 2)
    draw_h_road(tiles, 1, 14, WIDTH - 2)
    draw_v_road(tiles, 8, 1, HEIGHT - 2)
    draw_v_road(tiles, 22, 1, HEIGHT - 2)
    draw_road_intersection(tiles, 8, 6)
    draw_road_intersection(tiles, 22, 6)
    draw_road_intersection(tiles, 8, 14)
    draw_road_intersection(tiles, 22, 14)

    # Sidewalks around roads
    for y in range(1, HEIGHT - 1):
        for x in range(1, WIDTH - 1):
            if tiles[y][x] in (4, 22, 23):
                for dy in (-1, 1):
                    ny = y + dy
                    if is_inside(x, ny) and tiles[ny][x] == 0:
                        tiles[ny][x] = 5
                for dx in (-1, 1):
                    nx = x + dx
                    if is_inside(nx, y) and tiles[y][nx] == 0:
                        tiles[y][nx] = 5

    # Central market plaza
    fill_rect(tiles, 10, 7, 10, 6, 6)
    # Market stalls (planters as improvised stalls)
    tiles[8][12] = 14
    tiles[8][16] = 14
    tiles[11][13] = 14

    # Dense brick buildings around the plaza
    draw_building(tiles, 2, 2, 5, 4, wall_tile=2, roof=True, door_x=4)
    draw_building(tiles, 10, 2, 5, 4, wall_tile=2, roof=True, door_x=12)
    draw_building(tiles, 18, 2, 5, 4, wall_tile=2, roof=True, door_x=20)
    draw_building(tiles, 25, 2, 3, 4, wall_tile=2, roof=True, door_x=26)

    draw_building(tiles, 2, 8, 4, 5, wall_tile=2, roof=True, door_x=3)
    draw_building(tiles, 21, 8, 6, 5, wall_tile=2, roof=True, door_x=23)

    draw_building(tiles, 2, 15, 5, 3, wall_tile=2, roof=True, door_x=4)
    draw_building(tiles, 9, 15, 5, 3, wall_tile=2, roof=True, door_x=11)
    draw_building(tiles, 17, 15, 5, 3, wall_tile=2, roof=True, door_x=19)
    draw_building(tiles, 24, 15, 4, 3, wall_tile=2, roof=True, door_x=26)

    # Wet market (lower right corner)
    fill_rect(tiles, 24, 10, 4, 3, 6)
    tiles[11][26] = 14

    # Small canal/pond
    fill_rect(tiles, 2, 11, 4, 2, 3)
    tiles[12][3] = 15
    tiles[12][4] = 15

    # Old trees scattered
    place_trees(tiles, 6, seed=201)
    place_streetlights(tiles, 3, seed=202)
    place_planters(tiles, 3, seed=203)
    place_trashbins(tiles, 3, seed=204)

    # Ensure exits are walkable
    tiles[6][1] = 5    # left exit
    tiles[14][28] = 5  # right exit
    tiles[1][22] = 5   # top exit on vertical road

    # Ensure NPC spots are walkable and clearly visible
    jiuxu_npcs = [{"x": 12, "y": 4}, {"x": 8, "y": 12}, {"x": 20, "y": 16}]
    for npc in jiuxu_npcs:
        if tiles[npc["y"]][npc["x"]] in (1, 2, 3, 8, 9, 11, 12, 13, 14, 18, 19, 20, 21):
            tiles[npc["y"]][npc["x"]] = 5

    data = {
        "id": "jiuxu",
        "name": "Jiuxu",
        "anomalyType": "legacy_anomaly",
        "anomalyDescription": "old things remember things. sometimes they tell you. sometimes they show you.",
        "width": WIDTH,
        "height": HEIGHT,
        "tiles": tiles,
        "subDistricts": [
            {"id": "dongxu", "name": "Dongxu Night Market", "desc": "night market. steam, noise, something cooking that shouldn't be."},
            {"id": "laojie", "name": "Old Street Arcade", "desc": "covered walkways. old shops. older debts."},
            {"id": "caishichang", "name": "Wet Market", "desc": "fish, vegetables, gossip. the real news network."},
            {"id": "jiuxu_edge", "name": "Jiuxu Edge", "desc": "where the old buildings end and the new ones haven't started."}
        ],
        "npcs": [
            {"id": "chen_bo", "name": "Uncle Chen", "x": 12, "y": 4, "dialogueId": "chen_bo_01"},
            {"id": "pang_sao", "name": "Fat Sister-in-law", "x": 8, "y": 12, "dialogueId": "pang_sao_01"},
            {"id": "a_qiang", "name": "Ah Qiang", "x": 20, "y": 16, "dialogueId": "a_qiang_01"}
        ],
        "exits": [
            {"type": "district", "target": "jinyong", "x": 1, "y": 6},
            {"type": "district", "target": "chagang", "x": 22, "y": 1}
        ],
        "points": [
            {"id": "dongxu_poi", "name": "Dongxu Night Market", "x": 12, "y": 4, "type": "landmark"},
            {"id": "laojie_poi", "name": "Old Street", "x": 5, "y": 8, "type": "landmark"},
            {"id": "caishichang_poi", "name": "Wet Market", "x": 26, "y": 11, "type": "landmark"}
        ],
        "music": "jiuxu_bgm",
        "atmosphere": "old arcade buildings, steam from food stalls, something remembers you were here"
    }
    return data


def save_json(path, data):
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=4, ensure_ascii=False)


def main():
    jinyong = make_jinyong()
    jiuxu = make_jiuxu()
    save_json(r'C:\Users\Administrator\WorkBuddy\晖晖的小游戏\src\data\districts\jinyong.json', jinyong)
    save_json(r'C:\Users\Administrator\WorkBuddy\晖晖的小游戏\src\data\districts\jiuxu.json', jiuxu)
    print('Generated district maps:')
    print('  - src/data/districts/jinyong.json')
    print('  - src/data/districts/jiuxu.json')


if __name__ == '__main__':
    main()
