"""Normalize a rendered part image so image-to-3D pipelines (Hunyuan3D via Modly) accept it.

The generators emit near-white gradients, soft contact shadows and loose framing. Hunyuan's
background removal can fail on those. This isolates the subject, clips the background to pure
white, tightens the crop and re-centres it with a margin.

Usage:
  python normalize_part_image.py <in.png> [<in2.png> ...] [--out-dir DIR] [--size 1024]
"""
import argparse
import os
import sys

from PIL import Image, ImageFilter


def normalize(img, size, bg_threshold=246, margin_ratio=0.08):
    img = img.convert("RGB")

    # Distance from pure white per pixel: keeps the subject, drops gradients and soft shadows.
    gray = img.convert("L")
    mask = gray.point(lambda v: 255 if v < bg_threshold else 0)

    # Close small holes (highlights inside the subject) so the bbox is solid.
    mask = mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))

    bbox = mask.getbbox()
    if bbox is None:
        raise ValueError("no subject found: image looks uniformly white")

    subject = img.crop(bbox)
    w, h = subject.size
    side = max(w, h)
    margin = int(side * margin_ratio)
    canvas_side = side + 2 * margin

    # Paste onto pure white, centred, scaled to the requested square size.
    canvas = Image.new("RGB", (canvas_side, canvas_side), (255, 255, 255))
    canvas.paste(subject, ((canvas_side - w) // 2, (canvas_side - h) // 2))
    if canvas_side != size:
        canvas = canvas.resize((size, size), Image.LANCZOS)
    return canvas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("images", nargs="+")
    ap.add_argument("--out-dir", default=None, help="default: <image dir>/normalized")
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--threshold", type=int, default=246)
    ap.add_argument("--margin", type=float, default=0.08)
    args = ap.parse_args()

    for path in args.images:
        if not os.path.isfile(path):
            print(f"skip (missing): {path}")
            continue
        out_dir = args.out_dir or os.path.join(os.path.dirname(os.path.abspath(path)), "normalized")
        os.makedirs(out_dir, exist_ok=True)
        dest = os.path.join(out_dir, os.path.basename(path))

        with Image.open(path) as img:
            before = img.size
            out = normalize(img, args.size, args.threshold, args.margin)
        out.save(dest)
        print(f"{os.path.basename(path)}: {before} -> {out.size} -> {dest}")


if __name__ == "__main__":
    main()
