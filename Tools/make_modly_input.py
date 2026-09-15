"""Generate clean white-background source images for Modly image-to-3D (no API key needed)."""
import math
import os

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow requis: python -m pip install pillow") from exc

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "modly-input")
os.makedirs(OUT, exist_ok=True)


def save(name, draw):
    img = Image.new("RGB", (512, 512), "white")
    d = ImageDraw.Draw(img)
    draw(d)
    path = os.path.join(OUT, name)
    img.save(path)
    print(f"{path} OK")


def bumper(d):
    d.ellipse([106, 106, 406, 406], fill=(200, 20, 20), outline=(120, 0, 0), width=10)
    d.ellipse([206, 206, 306, 306], fill=(255, 255, 255))


def slingshot(d):
    d.polygon([(156, 56), (356, 56), (306, 456), (206, 456)], fill=(255, 140, 0))


def target(d):
    d.rectangle([156, 106, 356, 406], fill=(255, 220, 0), outline=(0, 0, 0), width=10)


def plunger_tip(d):
    d.ellipse([156, 156, 356, 356], fill=(180, 180, 180), outline=(60, 60, 60), width=10)
    d.ellipse([226, 226, 286, 286], fill=(255, 255, 255))


save("bumper.png", bumper)
save("slingshot.png", slingshot)
save("target.png", target)
save("plunger_tip.png", plunger_tip)
