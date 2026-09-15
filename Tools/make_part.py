"""End-to-end local part pipeline: Krea 2 image -> normalized PNG -> Hunyuan3D mesh -> decimate -> Unity.

Chains the stages so a new pinball part is one command:

    python Tools/make_part.py --name bumper --subject "a pinball bumper cap, ..."
    python Tools/make_part.py --all              # regenerate the built-in part set

Stages:
  1. Krea 2 Turbo (ComfyUI on 8189) renders a 1024x1024 product shot.
  2. normalize_part_image clips the background to pure white and re-centres the subject.
     Skipping this makes Modly's FastAPI bridge die mid-inference on soft-shadow renders.
  3. Modly (Hunyuan3D 2 Mini on 8765) converts the normalized PNG to GLB.
  4. decimate_glb quadric-decimates it to --faces triangles. Hunyuan emits ~0.5-1M tris per prop,
     which is 80x more than a pinball table can afford. Modly's mesh-optimizer processor cannot
     be driven over the API (its /process-runs endpoint 404s in 0.4.2), so pymeshlab does it.
  5. The GLB is copied into Assets/Models/Parts/ so glTFast imports it on Unity's next refresh.

Prerequisites (both servers must be running):
  ComfyUI   : python C:\\Users\\<you>\\ComfyUI\\main.py --port 8189 --listen 127.0.0.1
  Modly     : %LOCALAPPDATA%\\Programs\\Modly\\Modly.exe
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)

COMFY = os.environ.get("COMFY_SERVER", "http://127.0.0.1:8189")
MODLY = "http://127.0.0.1:8765"
MODLY_CLI = os.path.join(HERE, "modly-cli", "agent.py")

RAW_DIR = os.path.join(HERE, "comfyui", "out")
NORM_DIR = os.path.join(RAW_DIR, "normalized")
MESH_DIR = os.path.join(HERE, "modly-output")
UNITY_DIR = os.path.join(REPO, "Assets", "Models", "Parts")

# pymeshlab + trimesh ship inside Modly's own venv; neither is in the system interpreter.
MODLY_VENV_PY = os.path.join(
    os.path.expanduser("~"), "Documents", "Modly", "dependencies", "venv", "Scripts", "python.exe")

STYLE = ("clean product render of {subject}, isolated on pure white background, centered, "
         "orthographic view, soft even studio lighting, no shadow, high detail, sharp focus")
NEGATIVE = ("blurry, low quality, jpeg artifacts, text, watermark, signature, background clutter, "
            "cast shadow, multiple objects, cropped, frame, border, photo of a room")

PARTS = [
    ("bumper", "a pinball bumper cap, round red mushroom-shaped plastic bumper with a white circular top"),
    ("slingshot", "a pinball slingshot kicker, orange triangular plastic bumper with a black rubber band"),
    ("drop_target", "a pinball drop target, flat yellow rectangular plastic target plate with a black border"),
    ("plunger_knob", "a pinball plunger knob, polished chrome metal shooter rod handle"),
    ("ramp_segment", "a pinball wireform metal ramp segment, curved steel guide rail with chrome finish"),
    ("lane_guide", "a pinball lane guide rail, straight chrome metal ball guide with rolled edges"),
    ("post_rubber", "a pinball rubber post, small black rubber ring mounted on a chrome metal post"),
    ("flipper_bat", "a pinball flipper bat, orange plastic tapered paddle, three-quarter view from above "
                    "and to the side"),
]


def reachable(url, timeout=5):
    try:
        with urllib.request.urlopen(url, timeout=timeout) as r:
            return r.status == 200
    except Exception:
        return False


def require_servers(with_modly=True):
    if not reachable(f"{COMFY}/system_stats"):
        sys.exit(f"ComfyUI unreachable on {COMFY}. Start it on port 8189 (see module docstring).")
    if with_modly and not reachable(f"{MODLY}/health"):
        sys.exit(f"Modly unreachable on {MODLY}. Launch Modly and wait ~25 s.")


def free_comfy_vram():
    """Drop Krea 2's ~8.8 GB so Hunyuan3D has room on a 12 GB card."""
    body = json.dumps({"unload_models": True, "free_memory": True}).encode()
    req = urllib.request.Request(f"{COMFY}/free", data=body,
                                 headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=15):
            print("  freed ComfyUI VRAM")
    except Exception as exc:
        print(f"  warning: could not free VRAM ({exc})")


def krea_image(name, subject, seed, size, raw):
    prompt = subject if raw else STYLE.format(subject=subject)
    print(f"[1/4] Krea 2 render: {name}")
    cmd = [sys.executable, os.path.join(HERE, "comfyui", "krea2_generate.py"),
           "--name", name, "--prompt", prompt, "--seed", str(seed), "--size", str(size),
           "--server", COMFY]
    if raw:
        cmd.append("--raw")
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        print(res.stdout[-1500:])
        print(res.stderr[-1500:])
        sys.exit(f"image generation failed for {name}")
    out = os.path.join(RAW_DIR, f"{name}.png")
    print(f"  -> {out}")
    return out


def normalize(src, size):
    print(f"[2/4] normalize: {os.path.basename(src)}")
    cmd = [sys.executable, os.path.join(HERE, "normalize_part_image.py"), src,
           "--out-dir", NORM_DIR, "--size", str(size)]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        print(res.stdout[-1200:])
        print(res.stderr[-1200:])
        sys.exit("normalization failed")
    dst = os.path.join(NORM_DIR, os.path.basename(src))
    print(f"  -> {dst}")
    return dst


def mesh_from_image(name, normalized, fmt):
    print(f"[3/5] Hunyuan3D mesh: {name}")
    os.makedirs(MESH_DIR, exist_ok=True)
    out = os.path.join(MESH_DIR, f"krea_{name}.glb")
    cmd = [sys.executable, MODLY_CLI, "generate", "--image", normalized, "--output", out,
           "--no-texture", "--remesh", "quad", "--format", fmt]
    res = subprocess.run(cmd, capture_output=True, text=True)
    payload = res.stdout
    if '"status": "done"' not in payload or not os.path.exists(out):
        tail = payload[-1500:] or res.stderr[-1500:]
        sys.exit(f"mesh conversion failed for {name}:\n{tail}")
    print(f"  -> {out} ({os.path.getsize(out) / 1024 ** 2:.1f} MB)")
    return out


def decimate(name, mesh, faces):
    """Reduce the AI mesh to a game-ready triangle budget."""
    print(f"[4/5] decimate: {name} -> {faces} tris")
    if not os.path.exists(MODLY_VENV_PY):
        print(f"  warning: {MODLY_VENV_PY} missing (pymeshlab lives there); keeping the dense mesh")
        return mesh
    out_dir = os.path.join(MESH_DIR, "decimated")
    os.makedirs(out_dir, exist_ok=True)
    cmd = [MODLY_VENV_PY, os.path.join(HERE, "decimate_glb.py"), mesh,
           "--faces", str(faces), "--out-dir", out_dir]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        print(res.stdout[-1200:])
        print(res.stderr[-1200:])
        print("  warning: decimation failed; keeping the dense mesh")
        return mesh
    dst = os.path.join(out_dir, os.path.basename(mesh))
    print("  " + next((ln.strip() for ln in res.stdout.splitlines() if name in ln), dst))
    return dst if os.path.exists(dst) else mesh


def copy_to_unity(name, mesh):
    print(f"[5/5] import: {os.path.basename(mesh)} -> Assets/Models/Parts/")
    os.makedirs(UNITY_DIR, exist_ok=True)
    dest = os.path.join(UNITY_DIR, f"Krea_{name}.glb")
    shutil.copyfile(mesh, dest)
    print(f"  -> {dest}")
    return dest


def build_one(name, subject, seed, size, raw, fmt, faces):
    print(f"\n=== {name} ===")
    raw_png = krea_image(name, subject, seed, size, raw)
    free_comfy_vram()
    norm_png = normalize(raw_png, size)
    mesh = mesh_from_image(name, norm_png, fmt)
    mesh = decimate(name, mesh, faces)
    copy_to_unity(name, mesh)
    return mesh


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name")
    ap.add_argument("--subject", help="part description; omit with --all")
    ap.add_argument("--seed", type=int, default=777)
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--format", default="glb", choices=["glb", "obj", "stl", "ply"])
    ap.add_argument("--faces", type=int, default=8000,
                    help="triangle budget per part after decimation (0 = keep the dense AI mesh)")
    ap.add_argument("--raw", action="store_true", help="use --subject verbatim (no product-render style)")
    ap.add_argument("--all", action="store_true", help="run the built-in pinball part set")
    ap.add_argument("--skip-image", metavar="PNG",
                    help="reuse an existing PNG and run only normalize -> mesh -> decimate -> import")
    args = ap.parse_args()

    if args.skip_image:
        require_servers(with_modly=True)
        name = args.name or os.path.splitext(os.path.basename(args.skip_image))[0]
        norm_png = normalize(args.skip_image, args.size)
        mesh = mesh_from_image(name, norm_png, args.format)
        mesh = decimate(name, mesh, args.faces)
        copy_to_unity(name, mesh)
        return

    if args.all:
        require_servers(with_modly=False)
        done, failed = [], []
        for i, (name, subject) in enumerate(PARTS):
            try:
                build_one(name, subject, args.seed + i, args.size, args.raw, args.format, args.faces)
                done.append(name)
            except SystemExit as exc:
                print(f"  FAILED: {exc}")
                failed.append(name)
                require_servers(with_modly=False)  # Modly may have died; keep the loop honest
        print("\n=== summary ===")
        for n in done:
            print(f"  {n:<14} OK")
        for n in failed:
            print(f"  {n:<14} FAILED")
        sys.exit(1 if failed else 0)

    if not args.name or not args.subject:
        sys.exit("provide --name and --subject, or use --all / --skip-image")
    require_servers(with_modly=False)
    build_one(args.name, args.subject, args.seed, args.size, args.raw, args.format, args.faces)


if __name__ == "__main__":
    main()
