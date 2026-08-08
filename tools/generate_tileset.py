"""
Generate pixel-art city tileset for Kuncheng RPG.
Output: assets/tilesets/city_tileset.png (256x96, 8 cols x 3 rows, 32px each)
"""
import random
from PIL import Image, ImageDraw

# ============================================================
# CONFIG
# ============================================================
TILE = 32
COLS = 8
ROWS = 3
ATLAS_W = COLS * TILE  # 256
ATLAS_H = ROWS * TILE  # 96
SEED = 42

# Color palette
C = {
    # Grass
    'grass_1': (106, 190, 48),
    'grass_2': (90, 170, 40),
    'grass_3': (74, 150, 32),
    'grass_dark': (58, 122, 24),
    'grass_light': (130, 210, 60),
    # Wall / border
    'wall_1': (60, 60, 70),
    'wall_2': (45, 45, 55),
    'wall_3': (80, 80, 90),
    'wall_dark': (30, 30, 40),
    # Brick building
    'brick_1': (192, 80, 65),
    'brick_2': (165, 60, 50),
    'brick_3': (140, 45, 38),
    'brick mortar': (200, 195, 190),
    # Glass building
    'glass_1': (93, 173, 226),
    'glass_2': (52, 152, 219),
    'glass_3': (133, 193, 233),
    'glass_dark': (33, 97, 140),
    # Cement building
    'cement_1': (138, 138, 142),
    'cement_2': (118, 118, 122),
    'cement_3': (158, 158, 162),
    'cement_dark': (90, 90, 95),
    # Road
    'road_1': (58, 58, 58),
    'road_2': (45, 45, 45),
    'road_3': (72, 72, 72),
    'road_line': (241, 196, 15),
    'road_line_white': (236, 240, 241),
    # Sidewalk
    'sw_1': (176, 176, 176),
    'sw_2': (156, 156, 156),
    'sw_3': (196, 196, 196),
    'sw_dark': (120, 120, 120),
    # Plaza tiles
    'plaza_1': (200, 195, 185),
    'plaza_2': (180, 175, 165),
    'plaza_3': (160, 155, 145),
    'plaza_line': (140, 135, 125),
    # Indoor floor (wood)
    'wood_1': (160, 120, 70),
    'wood_2': (140, 100, 55),
    'wood_3': (180, 140, 85),
    'wood_dark': (110, 80, 40),
    # Water
    'water_1': (41, 128, 185),
    'water_2': (31, 97, 141),
    'water_3': (84, 153, 199),
    'water_light': (133, 193, 233),
    # Tree
    'trunk_1': (110, 75, 45),
    'trunk_2': (85, 55, 30),
    'leaves_1': (45, 106, 20),
    'leaves_2': (35, 86, 15),
    'leaves_3': (55, 126, 28),
    'leaves_light': (75, 146, 38),
    # Bush
    'bush_1': (50, 116, 25),
    'bush_2': (40, 96, 20),
    'bush_3': (60, 136, 32),
    # Door
    'door_1': (100, 65, 35),
    'door_2': (80, 50, 25),
    'door_3': (120, 80, 45),
    'door_handle': (220, 180, 50),
    # Window
    'win_frame': (90, 90, 100),
    'win_glass': (130, 180, 220),
    'win_glass_dark': (90, 140, 180),
    'win_light': (180, 210, 240),
    # Roof
    'roof_1': (180, 70, 55),
    'roof_2': (150, 50, 40),
    'roof_3': (200, 90, 70),
    'roof_dark': (120, 35, 28),
    # Streetlight
    'pole_1': (70, 70, 80),
    'pole_2': (50, 50, 60),
    'lamp_1': (255, 230, 120),
    'lamp_2': (255, 210, 80),
    # Planter
    'planter_1': (140, 110, 80),
    'planter_2': (110, 85, 60),
    'flower_r': (220, 60, 60),
    'flower_y': (240, 200, 50),
    'flower_p': (200, 100, 180),
    # Bridge
    'bridge_1': (150, 115, 70),
    'bridge_2': (120, 90, 50),
    'bridge_3': (170, 130, 85),
    # Sand
    'sand_1': (220, 200, 140),
    'sand_2': (200, 180, 120),
    'sand_3': (235, 215, 155),
    # Parking
    'park_1': (70, 70, 70),
    'park_2': (55, 55, 55),
    'park_line': (240, 240, 240),
    # Fence
    'fence_1': (130, 100, 60),
    'fence_2': (100, 75, 45),
    'fence_3': (150, 120, 75),
    # Trash bin
    'bin_1': (80, 100, 80),
    'bin_2': (60, 80, 60),
    'bin_3': (100, 120, 95),
    # Shadow
    'shadow': (0, 0, 0, 60),
}


def get_pos(tile_id):
    """Get (col, row) for a tile ID in the atlas."""
    return (tile_id % COLS, tile_id // COLS)


def paste_tile(atlas, tile_id, tile_img):
    """Paste a 32x32 tile into the atlas at the correct position."""
    col, row = get_pos(tile_id)
    atlas.paste(tile_img, (col * TILE, row * TILE))


def noise_pixels(img, colors, density=0.15, seed=0):
    """Add random noise pixels to an image."""
    random.seed(seed)
    draw = ImageDraw.Draw(img)
    w, h = img.size
    for _ in range(int(w * h * density)):
        x = random.randint(0, w - 1)
        y = random.randint(0, h - 1)
        c = random.choice(colors)
        draw.point((x, y), fill=c)


def draw_grass(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['grass_1'])
    noise_pixels(img, [C['grass_2'], C['grass_3'], C['grass_light'], C['grass_dark']], 0.2, SEED + tile_id)
    # Add a few grass blades
    draw = ImageDraw.Draw(img)
    random.seed(SEED + tile_id + 100)
    for _ in range(6):
        x = random.randint(2, TILE - 3)
        y = random.randint(2, TILE - 3)
        c = random.choice([C['grass_dark'], C['grass_3']])
        draw.point((x, y), fill=c)
        draw.point((x, y - 1), fill=c)
    paste_tile(atlas, tile_id, img)


def draw_wall(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['wall_1'])
    draw = ImageDraw.Draw(img)
    # Brick pattern
    for row in range(4):
        y = row * 8
        offset = (row % 2) * 8
        for col in range(5):
            x = col * 8 - offset
            if x < 0:
                x = 0
            if x >= TILE:
                continue
            w = min(7, TILE - 1 - x)
            if w <= 0:
                continue
            color = C['wall_2'] if (row + col) % 2 == 0 else C['wall_3']
            draw.rectangle([x, y, x + w, y + 6], fill=color)
        # Mortar line
        draw.line([(0, y + 7), (TILE - 1, y + 7)], fill=C['wall_dark'], width=1)
    # Top and bottom border
    draw.rectangle([0, 0, TILE - 1, 1], fill=C['wall_dark'])
    draw.rectangle([0, TILE - 2, TILE - 1, TILE - 1], fill=C['wall_dark'])
    paste_tile(atlas, tile_id, img)


def draw_brick_bldg(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['brick_1'])
    draw = ImageDraw.Draw(img)
    # Brick pattern
    for row in range(5):
        y = row * 6 + 1
        offset = (row % 2) * 4
        for col in range(5):
            x = col * 8 - offset
            if x < 0:
                x = 0
            if x >= TILE:
                continue
            w = min(7, TILE - x - 1)
            color = C['brick_2'] if (row + col) % 2 == 0 else C['brick_1']
            draw.rectangle([x, y, x + w, y + 4], fill=color)
        draw.line([(0, y + 5), (TILE - 1, y + 5)], fill=(200, 195, 190), width=1)
    # Top edge
    draw.rectangle([0, 0, TILE - 1, 0], fill=C['brick_3'])
    paste_tile(atlas, tile_id, img)


def draw_glass_bldg(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['glass_2'])
    draw = ImageDraw.Draw(img)
    # Window grid
    for row in range(4):
        for col in range(4):
            x = col * 8 + 1
            y = row * 8 + 1
            color = C['glass_1'] if (row + col) % 2 == 0 else C['glass_3']
            draw.rectangle([x, y, x + 6, y + 6], fill=color)
            # Highlight
            draw.point((x + 1, y + 1), fill=C['water_light'])
    # Frame
    for i in range(0, TILE, 8):
        draw.line([(i, 0), (i, TILE - 1)], fill=C['glass_dark'], width=1)
        draw.line([(0, i), (TILE - 1, i)], fill=C['glass_dark'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_cement_bldg(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['cement_1'])
    draw = ImageDraw.Draw(img)
    noise_pixels(img, [C['cement_2'], C['cement_3'], C['cement_dark']], 0.15, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Panel lines
    for i in range(0, TILE, 16):
        draw.line([(i, 0), (i, TILE - 1)], fill=C['cement_dark'], width=1)
    draw.line([(0, 15), (TILE - 1, 15)], fill=C['cement_dark'], width=1)
    # Small windows
    for row in range(2):
        for col in range(2):
            x = col * 16 + 4
            y = row * 16 + 4
            draw.rectangle([x, y, x + 7, y + 5], fill=C['glass_dark'])
            draw.rectangle([x + 1, y + 1, x + 6, y + 4], fill=C['glass_1'])
    paste_tile(atlas, tile_id, img)


def draw_water(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['water_1'])
    draw = ImageDraw.Draw(img)
    noise_pixels(img, [C['water_2'], C['water_3']], 0.2, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Wave lines
    for y in range(4, TILE, 8):
        for x in range(0, TILE, 6):
            offset = (y // 8) * 3
            wx = (x + offset) % TILE
            draw.point((wx, y), fill=C['water_light'])
            if wx + 1 < TILE:
                draw.point((wx + 1, y), fill=C['water_light'])
    paste_tile(atlas, tile_id, img)


def draw_road_h(tile_id, atlas):
    """Horizontal road with yellow center line."""
    img = Image.new('RGBA', (TILE, TILE), C['road_1'])
    draw = ImageDraw.Draw(img)
    noise_pixels(img, [C['road_2'], C['road_3']], 0.1, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Center yellow lines
    draw.line([(0, 15), (TILE - 1, 15)], fill=C['road_line'], width=1)
    draw.line([(0, 16), (TILE - 1, 16)], fill=C['road_line'], width=1)
    # Edge lines
    draw.line([(0, 2), (TILE - 1, 2)], fill=C['road_line_white'], width=1)
    draw.line([(0, 29), (TILE - 1, 29)], fill=C['road_line_white'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_road_v(tile_id, atlas):
    """Vertical road with yellow center line."""
    img = Image.new('RGBA', (TILE, TILE), C['road_1'])
    draw = ImageDraw.Draw(img)
    noise_pixels(img, [C['road_2'], C['road_3']], 0.1, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Center yellow lines
    draw.line([(15, 0), (15, TILE - 1)], fill=C['road_line'], width=1)
    draw.line([(16, 0), (16, TILE - 1)], fill=C['road_line'], width=1)
    # Edge lines
    draw.line([(2, 0), (2, TILE - 1)], fill=C['road_line_white'], width=1)
    draw.line([(29, 0), (29, TILE - 1)], fill=C['road_line_white'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_road_plain(tile_id, atlas):
    """Plain road (intersection / generic)."""
    img = Image.new('RGBA', (TILE, TILE), C['road_1'])
    noise_pixels(img, [C['road_2'], C['road_3']], 0.12, SEED + tile_id)
    paste_tile(atlas, tile_id, img)


def draw_sidewalk(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['sw_1'])
    draw = ImageDraw.Draw(img)
    noise_pixels(img, [C['sw_2'], C['sw_3']], 0.1, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Tile pattern
    for row in range(2):
        for col in range(2):
            x = col * 16
            y = row * 16
            draw.rectangle([x, y, x + 14, y + 14], fill=None, outline=C['sw_dark'], width=1)
    # Inner highlights
    draw.line([(1, 1), (14, 1)], fill=C['sw_3'], width=1)
    draw.line([(1, 1), (1, 14)], fill=C['sw_3'], width=1)
    draw.line([(17, 1), (30, 1)], fill=C['sw_3'], width=1)
    draw.line([(17, 17), (30, 17)], fill=C['sw_3'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_plaza(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['plaza_1'])
    draw = ImageDraw.Draw(img)
    # Diagonal tile pattern
    for i in range(0, TILE, 8):
        draw.line([(i, 0), (0, i)], fill=C['plaza_line'], width=1)
        draw.line([(i, TILE - 1), (TILE - 1, i)], fill=C['plaza_line'], width=1)
    # Fill alternating
    for row in range(4):
        for col in range(4):
            x = col * 8
            y = row * 8
            if (row + col) % 2 == 0:
                draw.rectangle([x + 1, y + 1, x + 6, y + 6], fill=C['plaza_2'])
            else:
                draw.rectangle([x + 1, y + 1, x + 6, y + 6], fill=C['plaza_3'])
    paste_tile(atlas, tile_id, img)


def draw_floor(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['wood_1'])
    draw = ImageDraw.Draw(img)
    # Wood plank lines
    for i in range(0, TILE, 8):
        draw.line([(0, i), (TILE - 1, i)], fill=C['wood_dark'], width=1)
    # Wood grain
    random.seed(SEED + tile_id)
    for _ in range(8):
        y = random.randint(1, TILE - 2)
        x1 = random.randint(0, 10)
        x2 = random.randint(20, TILE - 1)
        draw.line([(x1, y), (x2, y)], fill=C['wood_2'], width=1)
    # Highlights
    for i in range(1, TILE, 8):
        draw.line([(1, i), (TILE - 2, i)], fill=C['wood_3'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_tree(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Shadow
    draw.ellipse([6, 26, 25, 31], fill=(0, 0, 0, 50))
    # Trunk
    draw.rectangle([14, 20, 17, 28], fill=C['trunk_1'])
    draw.rectangle([14, 20, 14, 28], fill=C['trunk_2'])
    # Tree crown - layered circles
    draw.ellipse([4, 2, 27, 24], fill=C['leaves_2'])
    draw.ellipse([6, 4, 25, 22], fill=C['leaves_1'])
    draw.ellipse([9, 6, 22, 19], fill=C['leaves_3'])
    # Highlights
    draw.ellipse([8, 5, 14, 11], fill=C['leaves_light'])
    draw.point((10, 7), fill=C['leaves_light'])
    draw.point((18, 9), fill=C['leaves_light'])
    # Dark spots
    draw.point((20, 16), fill=C['leaves_2'])
    draw.point((12, 17), fill=C['leaves_2'])
    draw.point((22, 7), fill=C['leaves_2'])
    paste_tile(atlas, tile_id, img)


def draw_bush(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Shadow
    draw.ellipse([4, 26, 27, 30], fill=(0, 0, 0, 40))
    # Bush body
    draw.ellipse([3, 10, 28, 28], fill=C['bush_2'])
    draw.ellipse([5, 8, 26, 26], fill=C['bush_1'])
    draw.ellipse([8, 10, 20, 22], fill=C['bush_3'])
    # Highlights
    draw.point((10, 12), fill=C['leaves_light'])
    draw.point((16, 11), fill=C['leaves_light'])
    draw.point((20, 14), fill=C['leaves_light'])
    paste_tile(atlas, tile_id, img)


def draw_door(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['brick_1'])
    draw = ImageDraw.Draw(img)
    # Door frame
    draw.rectangle([6, 4, 25, 31], fill=C['door_1'])
    draw.rectangle([8, 6, 23, 31], fill=C['door_2'])
    # Door panels
    draw.rectangle([10, 9, 21, 18], fill=C['door_3'])
    draw.rectangle([10, 20, 21, 29], fill=C['door_3'])
    # Top arch
    draw.arc([6, 2, 25, 12], 180, 360, fill=C['door_1'], width=2)
    # Door handle
    draw.point((20, 24), fill=C['door_handle'])
    draw.point((20, 25), fill=C['door_handle'])
    # Top border
    draw.rectangle([0, 0, TILE - 1, 3], fill=C['brick_2'])
    paste_tile(atlas, tile_id, img)


def draw_window(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['brick_1'])
    draw = ImageDraw.Draw(img)
    # Brick around window
    for row in range(5):
        y = row * 6 + 1
        offset = (row % 2) * 4
        for col in range(5):
            x = col * 8 - offset
            if x < 0:
                x = 0
            if x >= TILE:
                continue
            w = min(7, TILE - x - 1)
            color = C['brick_2'] if (row + col) % 2 == 0 else C['brick_1']
            draw.rectangle([x, y, x + w, y + 4], fill=color)
    # Window
    draw.rectangle([8, 8, 23, 23], fill=C['win_frame'])
    draw.rectangle([9, 9, 22, 22], fill=C['win_glass'])
    # Window cross
    draw.line([(9, 15), (22, 15)], fill=C['win_frame'], width=1)
    draw.line([(15, 9), (15, 22)], fill=C['win_frame'], width=1)
    # Reflection
    draw.line([(11, 11), (14, 11)], fill=C['win_light'], width=1)
    draw.line([(11, 11), (11, 14)], fill=C['win_light'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_roof(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['roof_1'])
    draw = ImageDraw.Draw(img)
    # Roof tiles
    for row in range(4):
        y = row * 8
        offset = (row % 2) * 4
        for col in range(8):
            x = col * 4 - offset
            if x < 0:
                x = 0
            if x >= TILE:
                continue
            color = C['roof_2'] if (row + col) % 2 == 0 else C['roof_1']
            draw.rectangle([x, y, x + 3, y + 6], fill=color)
            # Tile curve
            draw.arc([x, y + 3, x + 4, y + 7], 0, 180, fill=C['roof_3'], width=1)
        draw.line([(0, y + 7), (TILE - 1, y + 7)], fill=C['roof_dark'], width=1)
    # Top edge
    draw.rectangle([0, 0, TILE - 1, 1], fill=C['roof_dark'])
    paste_tile(atlas, tile_id, img)


def draw_streetlight(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Shadow
    draw.ellipse([10, 28, 22, 31], fill=(0, 0, 0, 50))
    # Pole
    draw.rectangle([14, 6, 17, 30], fill=C['pole_1'])
    draw.rectangle([14, 6, 14, 30], fill=C['pole_2'])
    # Lamp head
    draw.rectangle([10, 3, 21, 8], fill=C['pole_1'])
    draw.rectangle([11, 4, 20, 7], fill=C['lamp_1'])
    draw.rectangle([12, 5, 19, 6], fill=C['lamp_2'])
    # Light glow
    draw.ellipse([6, 0, 25, 12], fill=(255, 230, 120, 30))
    draw.ellipse([9, 2, 22, 10], fill=(255, 230, 120, 40))
    paste_tile(atlas, tile_id, img)


def draw_planter(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Shadow
    draw.ellipse([2, 28, 29, 31], fill=(0, 0, 0, 40))
    # Planter box
    draw.rectangle([3, 14, 28, 28], fill=C['planter_1'])
    draw.rectangle([3, 14, 28, 16], fill=C['planter_2'])
    draw.rectangle([3, 26, 28, 28], fill=C['planter_2'])
    # Soil
    draw.rectangle([5, 15, 26, 18], fill=(80, 55, 35))
    # Flowers
    random.seed(SEED + tile_id)
    flowers = [C['flower_r'], C['flower_y'], C['flower_p']]
    for _ in range(5):
        x = random.randint(6, 25)
        y = random.randint(8, 14)
        c = random.choice(flowers)
        draw.rectangle([x, y, x + 2, y + 2], fill=c)
        draw.point((x + 1, y + 1), fill=(255, 255, 200))
        # Stem
        stem_y = y + 3
        if stem_y < 18:
            draw.point((x + 1, stem_y), fill=C['bush_1'])
    paste_tile(atlas, tile_id, img)


def draw_bridge(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['water_1'])
    draw = ImageDraw.Draw(img)
    noise_pixels(img, [C['water_2'], C['water_3']], 0.15, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Bridge planks
    draw.rectangle([0, 4, TILE - 1, 27], fill=C['bridge_1'])
    for i in range(4, 28, 4):
        draw.line([(0, i), (TILE - 1, i)], fill=C['bridge_2'], width=1)
    # Plank highlights
    for i in range(5, 27, 4):
        draw.line([(1, i), (TILE - 2, i)], fill=C['bridge_3'], width=1)
    # Railings
    draw.rectangle([0, 2, TILE - 1, 4], fill=C['bridge_2'])
    draw.rectangle([0, 27, TILE - 1, 29], fill=C['bridge_2'])
    paste_tile(atlas, tile_id, img)


def draw_sand(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['sand_1'])
    noise_pixels(img, [C['sand_2'], C['sand_3']], 0.25, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Sand ripples
    for y in range(6, TILE, 8):
        offset = (y // 8) * 2
        for x in range(0, TILE, 4):
            draw.point(((x + offset) % TILE, y), fill=C['sand_2'])
    paste_tile(atlas, tile_id, img)


def draw_parking(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), C['park_1'])
    noise_pixels(img, [C['park_2']], 0.1, SEED + tile_id)
    draw = ImageDraw.Draw(img)
    # Parking space lines
    draw.line([(4, 0), (4, TILE - 1)], fill=C['park_line'], width=1)
    draw.line([(27, 0), (27, TILE - 1)], fill=C['park_line'], width=1)
    draw.line([(4, 2), (27, 2)], fill=C['park_line'], width=1)
    draw.line([(4, TILE - 3), (27, TILE - 3)], fill=C['park_line'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_fence(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Ground shadow
    draw.rectangle([0, 28, TILE - 1, 30], fill=(0, 0, 0, 30))
    # Fence posts
    for x in [4, 14, 24]:
        draw.rectangle([x, 8, x + 3, 28], fill=C['fence_2'])
        draw.rectangle([x, 8, x, 28], fill=C['fence_1'])
        draw.point((x + 1, 8), fill=C['fence_3'])
    # Horizontal bars
    draw.rectangle([2, 12, 29, 14], fill=C['fence_1'])
    draw.rectangle([2, 22, 29, 24], fill=C['fence_1'])
    draw.line([(2, 12), (29, 12)], fill=C['fence_3'], width=1)
    draw.line([(2, 22), (29, 22)], fill=C['fence_3'], width=1)
    paste_tile(atlas, tile_id, img)


def draw_trashbin(tile_id, atlas):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Shadow
    draw.ellipse([8, 28, 24, 31], fill=(0, 0, 0, 50))
    # Bin body
    draw.rectangle([10, 10, 21, 28], fill=C['bin_1'])
    draw.rectangle([10, 10, 11, 28], fill=C['bin_2'])
    draw.rectangle([20, 10, 21, 28], fill=C['bin_2'])
    # Bin lid
    draw.rectangle([8, 7, 23, 11], fill=C['bin_3'])
    draw.rectangle([8, 7, 23, 8], fill=C['bin_2'])
    # Handle
    draw.rectangle([13, 5, 18, 7], fill=C['bin_2'])
    # Stripes
    draw.line([(12, 16), (19, 16)], fill=C['bin_3'], width=1)
    draw.line([(12, 20), (19, 20)], fill=C['bin_3'], width=1)
    paste_tile(atlas, tile_id, img)


# ============================================================
# BUILD ATLAS
# ============================================================
def main():
    atlas = Image.new('RGBA', (ATLAS_W, ATLAS_H), (0, 0, 0, 0))

    # Row 0: Ground types
    draw_grass(0, atlas)          # 0: grass (walkable)
    draw_wall(1, atlas)           # 1: wall (solid)
    draw_brick_bldg(2, atlas)     # 2: brick building (solid)
    draw_water(3, atlas)          # 3: water (solid)
    draw_road_plain(4, atlas)     # 4: road plain (walkable)
    draw_sidewalk(5, atlas)       # 5: sidewalk (walkable)
    draw_plaza(6, atlas)          # 6: plaza tiles (walkable)
    draw_floor(7, atlas)          # 7: indoor floor (walkable)

    # Row 1: Objects and decorations
    draw_tree(8, atlas)           # 8: tree (solid)
    draw_bush(9, atlas)           # 9: bush (solid)
    draw_door(10, atlas)          # 10: door (walkable)
    draw_window(11, atlas)        # 11: window (solid)
    draw_roof(12, atlas)          # 12: roof (solid)
    draw_streetlight(13, atlas)   # 13: streetlight (solid)
    draw_planter(14, atlas)       # 14: planter (solid)
    draw_bridge(15, atlas)        # 15: bridge (walkable)

    # Row 2: More ground types and city elements
    draw_sand(16, atlas)          # 16: sand (walkable)
    draw_parking(17, atlas)       # 17: parking lot (walkable)
    draw_fence(18, atlas)         # 18: fence (solid)
    draw_trashbin(19, atlas)      # 19: trash bin (solid)
    draw_glass_bldg(20, atlas)    # 20: glass building (solid)
    draw_cement_bldg(21, atlas)   # 21: cement building (solid)
    draw_road_h(22, atlas)        # 22: road horizontal (walkable)
    draw_road_v(23, atlas)        # 23: road vertical (walkable)

    # Save
    out_path = r'C:\Users\Administrator\WorkBuddy\晖晖的小游戏\assets\tilesets\city_tileset.png'
    atlas.save(out_path, 'PNG')
    print(f'Tileset saved to: {out_path}')
    print(f'Atlas size: {ATLAS_W}x{ATLAS_H} ({COLS}x{ROWS} tiles, {TILE}px each)')

    # Also save a 4x upscaled version for preview
    big = atlas.resize((ATLAS_W * 4, ATLAS_H * 4), Image.NEAREST)
    preview_path = r'C:\Users\Administrator\WorkBuddy\晖晖的小游戏\assets\tilesets\city_tileset_preview.png'
    big.save(preview_path, 'PNG')
    print(f'Preview saved to: {preview_path}')


if __name__ == '__main__':
    main()
