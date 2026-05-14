"""
Nearest-neighbor upscale for pixel-art tree PNGs (no blur).
Default: 4x for small sprites (max side <= 160), 2x otherwise.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: pip install Pillow", file=sys.stderr)
    sys.exit(1)


def pick_scale(width: int, height: int, small_max_side: int, small_scale: int, large_scale: int) -> int:
    m = max(width, height)
    return small_scale if m <= small_max_side else large_scale


def upscale_file(src: Path, dst: Path, scale: int) -> None:
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    nw, nh = w * scale, h * scale
    out = im.resize((nw, nh), Image.Resampling.NEAREST)
    dst.parent.mkdir(parents=True, exist_ok=True)
    out.save(dst, format="PNG")


def main() -> None:
    p = argparse.ArgumentParser(description="Upscale Tree*.png with nearest neighbor.")
    p.add_argument(
        "--input",
        type=Path,
        default=Path(r"C:\Users\layla\.cursor\projects\d-GitHub-GADS-7331-POE-Part-2\assets"),
        help="Folder containing Tree*.png",
    )
    p.add_argument(
        "--output",
        type=Path,
        default=Path(r"d:\GitHub\GADS 7331 POE Part 2\Back To The Forge\Assets\Sprites\Trees_Upscaled"),
        help="Output folder for upscaled PNGs",
    )
    p.add_argument(
        "--uniform-scale",
        type=int,
        default=None,
        metavar="N",
        help="If set, upscale every file by exactly N (integer nearest-neighbor). Overrides auto scale.",
    )
    p.add_argument("--small-max-side", type=int, default=160, help="Max side to use small-scale multiplier")
    p.add_argument("--small-scale", type=int, default=4, help="Scale for small sprites (e.g. 96px trees)")
    p.add_argument("--large-scale", type=int, default=2, help="Scale for larger tree sprites")
    args = p.parse_args()

    if not args.input.is_dir():
        print(f"Input folder not found: {args.input}", file=sys.stderr)
        sys.exit(1)

    files = sorted(args.input.glob("Tree*.png"))
    if not files:
        print(f"No Tree*.png in {args.input}", file=sys.stderr)
        sys.exit(1)

    for src in files:
        with Image.open(src) as im:
            w, h = im.size
        if args.uniform_scale is not None:
            scale = args.uniform_scale
            if scale < 1:
                print("--uniform-scale must be >= 1", file=sys.stderr)
                sys.exit(1)
        else:
            scale = pick_scale(w, h, args.small_max_side, args.small_scale, args.large_scale)
        name = f"{src.stem}_x{scale}{src.suffix}"
        dst = args.output / name
        upscale_file(src, dst, scale)
        print(f"{src.name}  {w}x{h}  ->  {dst.name}  ({scale}x)")


if __name__ == "__main__":
    main()
