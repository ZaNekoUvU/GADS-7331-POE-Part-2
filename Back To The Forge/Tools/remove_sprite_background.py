from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

# Only flood through near-pure-black background pixels.
BACKGROUND_THRESHOLD = 15
# Extra dark pixels kept around the character silhouette.
OUTLINE_PAD = 3
ROOT = Path(r"d:\GitHub\GADS 7331 POE Part 2\Back To The Forge\Assets\Sprites")
TARGET_DIRS = [ROOT / "Mercenaries", ROOT / "Enemies"]


def is_background_black(r: int, g: int, b: int, a: int) -> bool:
    return a >= 16 and max(r, g, b) <= BACKGROUND_THRESHOLD


def touches_character(pixels, x: int, y: int, width: int, height: int) -> bool:
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if nx < 0 or ny < 0 or nx >= width or ny >= height:
            continue
        r, g, b, a = pixels[nx, ny]
        if a >= 16 and max(r, g, b) > BACKGROUND_THRESHOLD:
            return True
    return False


def flood_background_mask(pixels, width: int, height: int) -> list[list[bool]]:
    background = [[False] * width for _ in range(height)]
    queue: deque[tuple[int, int]] = deque()

    def try_seed(x: int, y: int) -> None:
        if background[y][x]:
            return
        r, g, b, a = pixels[x, y]
        if a < 16:
            background[y][x] = True
            queue.append((x, y))
            return
        if not is_background_black(r, g, b, a):
            return
        if touches_character(pixels, x, y, width, height):
            return
        background[y][x] = True
        queue.append((x, y))

    for x in range(width):
        try_seed(x, 0)
        try_seed(x, height - 1)
    for y in range(height):
        try_seed(0, y)
        try_seed(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= width or ny >= height or background[ny][nx]:
                continue
            r, g, b, a = pixels[nx, ny]
            if a < 16:
                background[ny][nx] = True
                queue.append((nx, ny))
                continue
            if not is_background_black(r, g, b, a):
                continue
            if touches_character(pixels, nx, ny, width, height):
                continue
            background[ny][nx] = True
            queue.append((nx, ny))

    return background


def pad_foreground(keep: list[list[bool]], width: int, height: int, radius: int) -> list[list[bool]]:
    padded = [row[:] for row in keep]
    for _ in range(radius):
        next_pad = [row[:] for row in padded]
        for y in range(height):
            for x in range(width):
                if padded[y][x]:
                    continue
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height:
                        continue
                    if padded[ny][nx]:
                        next_pad[y][x] = True
                        break
        padded = next_pad
    return padded


def remove_background(path: Path) -> bool:
    image = Image.open(path).convert("RGBA")
    width, height = image.size
    pixels = image.load()
    background = flood_background_mask(pixels, width, height)

    keep = [[not background[y][x] for x in range(width)] for y in range(height)]
    keep = pad_foreground(keep, width, height, OUTLINE_PAD)

    changed = 0
    for y in range(height):
        for x in range(width):
            if keep[y][x]:
                continue
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            pixels[x, y] = (r, g, b, 0)
            changed += 1

    if changed == 0:
        return False

    image.save(path)
    return True


def main() -> None:
    updated: list[str] = []
    for folder in TARGET_DIRS:
        if not folder.exists():
            continue
        for path in sorted(folder.rglob("*.png")):
            if remove_background(path):
                updated.append(str(path))

    print(f"Removed background from {len(updated)} sprites (outlines preserved)")
    for item in updated:
        print(f"  {item}")


if __name__ == "__main__":
    main()
