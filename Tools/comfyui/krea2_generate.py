"""Generate pinball part images locally with Krea 2 Turbo (GGUF) via the ComfyUI API.

Reads the workflow template next to this file, injects prompt/seed/size, submits it,
and copies each rendered image to Tools/comfyui/out/<name>.png.

Usage:
  python krea2_generate.py --name bumper --prompt "..." [--seed 123] [--size 1024]
  python krea2_generate.py --batch          # generate the built-in pinball part set
"""
import argparse
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
TEMPLATE = os.path.join(HERE, "krea2_workflow.json")
OUT_DIR = os.path.join(HERE, "out")

# Comfy Desktop owns 8188; the GGUF-capable standalone install listens on 8189.
SERVER = os.environ.get("COMFY_SERVER", "http://127.0.0.1:8189")

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
    ("flipper_bat", "a pinball flipper bat, orange plastic paddle with a tapered triangular shape"),
]


def api(path, payload=None, timeout=30):
    url = f"{SERVER}{path}"
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        body = r.read()
    return json.loads(body) if body else None


def build_workflow(prompt, negative, seed, size):
    with open(TEMPLATE, encoding="utf-8") as f:
        wf = json.load(f)
    wf["4"]["inputs"]["text"] = prompt
    wf["5"]["inputs"]["text"] = negative
    wf["6"]["inputs"]["width"] = size
    wf["6"]["inputs"]["height"] = size
    wf["7"]["inputs"]["seed"] = seed
    return wf


def wait_for_server(timeout=180):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            api("/system_stats", timeout=3)
            return True
        except Exception:
            time.sleep(2)
    return False


def run_one(name, subject, seed, size, negative, raw_prompt=False):
    prompt = subject if raw_prompt else STYLE.format(subject=subject)
    wf = build_workflow(prompt, negative, seed, size)

    print(f"[{name}] submitting (seed={seed}, {size}x{size})")
    resp = api("/prompt", {"prompt": wf}, timeout=60)
    prompt_id = resp.get("prompt_id")
    if not prompt_id:
        raise RuntimeError(f"no prompt_id in response: {resp}")
    print(f"[{name}] queued as {prompt_id}")

    started = time.time()
    last_note = 0
    while True:
        time.sleep(2)
        try:
            history = api(f"/history/{prompt_id}", timeout=15)
        except urllib.error.URLError as exc:
            print(f"[{name}] history poll failed: {exc}")
            continue

        entry = history.get(prompt_id)
        if entry is None:
            if time.time() - last_note > 20:
                print(f"[{name}] running... {time.time() - started:.0f}s")
                last_note = time.time()
            continue

        status = entry.get("status", {})
        if status.get("status_str") == "error" or not status.get("completed", True):
            print(f"[{name}] FAILED: {json.dumps(status)[:600]}")
            return None

        images = []
        for node in entry.get("outputs", {}).values():
            images.extend(node.get("images", []))
        if not images:
            print(f"[{name}] completed without images")
            return None

        os.makedirs(OUT_DIR, exist_ok=True)
        saved = []
        for i, img in enumerate(images):
            qs = urllib.parse.urlencode({
                "filename": img["filename"],
                "subfolder": img.get("subfolder", ""),
                "type": img.get("type", "output"),
            })
            with urllib.request.urlopen(f"{SERVER}/view?{qs}", timeout=60) as r:
                blob = r.read()
            dest = os.path.join(OUT_DIR, f"{name}.png" if i == 0 else f"{name}_{i}.png")
            with open(dest, "wb") as f:
                f.write(blob)
            saved.append(dest)
        elapsed = time.time() - started
        print(f"[{name}] done in {elapsed:.0f}s -> {saved[0]} ({len(blob) / 1024:.0f} KB)")
        return saved[0]


def main():
    global SERVER

    ap = argparse.ArgumentParser()
    ap.add_argument("--name")
    ap.add_argument("--prompt")
    ap.add_argument("--negative", default=NEGATIVE)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--server", default=SERVER, help=f"ComfyUI base URL (default {SERVER})")
    ap.add_argument("--raw", action="store_true",
                    help="use --prompt verbatim instead of wrapping it in the product-render style")
    ap.add_argument("--batch", action="store_true", help="generate the built-in pinball part set")
    args = ap.parse_args()

    SERVER = args.server

    if not wait_for_server():
        sys.exit("ComfyUI API unreachable on " + SERVER)

    if args.batch:
        base_seed = args.seed or 777
        results = {}
        for i, (name, subject) in enumerate(PARTS):
            try:
                results[name] = run_one(name, subject, base_seed + i, args.size, args.negative)
            except Exception as exc:
                print(f"[{name}] error: {exc}")
                results[name] = None
        print("\nsummary:")
        for name, path in results.items():
            print(f"  {name:<14} {'OK ' + path if path else 'FAILED'}")
        return

    if not args.name or not args.prompt:
        sys.exit("provide --name and --prompt, or use --batch")
    run_one(args.name, args.prompt, args.seed or 777, args.size, args.negative, raw_prompt=args.raw)


if __name__ == "__main__":
    main()
