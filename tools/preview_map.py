"""
Render a district map to PNG for preview using the generated tileset.
Output: assets/tilesets/<district>_preview.png
"""
import json
from PIL import Image

TILE = 32
COLS = 8


def load_tileset(path):
    img = Image.open(path).convert('RGBA')
    return img


def draw_tile(canvas, tileset, tile_id, x, y):
    if tile_id < 0 or tile_id >= 24:
        return
    col = tile_id % COLS
    row = tile_id // COLS
    tile = tileset.crop((col * TILE, row * TILE, (col + 1) * TILE, (row + 1) * TILE))
    canvas.paste(tile, (x, y), tile)


def render_district(district_path, tileset_path, output_path):
    with open(district_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    tileset = load_tileset(tileset_path)
    w = data['width'] * TILE
    h = data['height'] * TILE
    canvas = Image.new('RGBA', (w, h), (20, 20, 30, 255))
    for y, row in enumerate(data['tiles']):
        for x, tile_id in enumerate(row):
            draw_tile(canvas, tileset, tile_id, x * TILE, y * TILE)
    # Draw NPC markers
    for npc in data.get('npcs', []):
        nx, ny = npc['x'] * TILE, npc['y'] * TILE
        marker = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
        px = [nx + 8, nx + 24, nx + 16, nx + 8]
        py = [ny + 8, ny + 8, ny + 28, ny + 8]
        from PIL import ImageDraw
        d = ImageDraw.Draw(marker)
        d.polygon([(8, 8), (24, 8), (16, 28)], fill=(255, 80, 80, 200))
        canvas.paste(marker, (nx, ny), marker)
    # Draw exit markers
    for exit in data.get('exits', []):
        ex, ey = exit['x'] * TILE, exit['y'] * TILE
        marker = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
        from PIL import ImageDraw
        d = ImageDraw.Draw(marker)
        d.rectangle([4, 4, 27, 27], outline=(93, 202, 165, 220), width=2)
        canvas.paste(marker, (ex, ey), marker)
    # Upscale 2x for visibility
    canvas = canvas.resize((w * 2, h * 2), Image.NEAREST)
    canvas.save(output_path, 'PNG')
    print(f'Saved preview: {output_path}')


def main():
    base = r'C:\Users\Administrator\WorkBuddy\晖晖的小游戏'
    tileset = f'{base}\\assets\\tilesets\\city_tileset.png'
    render_district(f'{base}\\src\\data\\districts\\jinyong.json', tileset, f'{base}\\assets\\tilesets\\jinyong_preview.png')
    render_district(f'{base}\\src\\data\\districts\\jiuxu.json', tileset, f'{base}\\assets\\tilesets\\jiuxu_preview.png')


if __name__ == '__main__':
    main()
