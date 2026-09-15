"""Download Krea 2 Turbo runtime files into a ComfyUI models tree (resumable).

Files:
  unet/         krea2_turbo GGUF (default Q5_K_M, ~8.3 GB) - fits a 12 GB GPU
  text_encoders/qwen3vl_4b_fp8_scaled.safetensors (~4.9 GB)
  vae/          qwen_image_vae.safetensors (~0.24 GB)

Usage:
  python fetch_krea2.py [--comfy-root C:\\Users\\me\\ComfyUI] [--quant Q5_K_M] [--check]
"""
import argparse
import os
import shutil
import sys

from huggingface_hub import hf_hub_download

GGUF_REPO = "vantagewithai/Krea-2-Turbo-GGUF"
OFFICIAL_REPO = "Comfy-Org/Krea-2"

JOBS = {
    "Q2_K": ("unet", GGUF_REPO, "krea2_turbo-Q2_K.gguf", 4.55),
    "Q3_K_M": ("unet", GGUF_REPO, "krea2_turbo-Q3_K_M.gguf", 5.60),
    "Q4_K_M": ("unet", GGUF_REPO, "krea2_turbo-Q4_K_M.gguf", 6.97),
    "Q4_K_S": ("unet", GGUF_REPO, "krea2_turbo-Q4_K_S.gguf", 6.97),
    "Q5_K_M": ("unet", GGUF_REPO, "krea2_turbo-Q5_K_M.gguf", 8.26),
    "Q6_K": ("unet", GGUF_REPO, "krea2_turbo-Q6_K.gguf", 9.86),
}

SHARED = [
    ("text_encoders", OFFICIAL_REPO, "text_encoders/qwen3vl_4b_fp8_scaled.safetensors", 4.88),
    ("vae", OFFICIAL_REPO, "vae/qwen_image_vae.safetensors", 0.24),
]


def free_gb(path):
    return shutil.disk_usage(path).free / 1024 ** 3


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--comfy-root", default=os.path.expanduser("~/ComfyUI"))
    ap.add_argument("--quant", default="Q5_K_M", choices=sorted(JOBS))
    ap.add_argument("--check", action="store_true", help="report presence/sizes and exit")
    args = ap.parse_args()

    models_root = os.path.join(args.comfy_root, "models")
    if not os.path.isdir(models_root):
        sys.exit(f"models tree not found: {models_root}")

    subdir, repo, filename, size_gb = JOBS[args.quant]
    jobs = [(subdir, repo, filename, size_gb)] + SHARED
    total = sum(j[3] for j in jobs)

    print(f"ComfyUI: {args.comfy_root}")
    for sub, repo, name, size in jobs:
        target = os.path.join(models_root, sub, os.path.basename(name))
        state = f"{os.path.getsize(target) / 1024 ** 3:.2f} GB present" if os.path.exists(target) else "missing"
        print(f"  {sub}/{os.path.basename(name):<45} needs {size:>5.2f} GB  [{state}]")
    print(f"total required: {total:.2f} GB | free on volume: {free_gb(models_root):.1f} GB")

    if args.check:
        return
    if free_gb(models_root) < total:
        sys.exit("not enough free space")

    for sub, repo, name, size in jobs:
        dest_dir = os.path.join(models_root, sub)
        os.makedirs(dest_dir, exist_ok=True)
        print(f"\n>>> {repo} :: {name} -> {dest_dir}")
        path = hf_hub_download(repo_id=repo, filename=name, local_dir=dest_dir)
        moved = os.path.join(dest_dir, os.path.basename(name))
        if os.path.abspath(path) != os.path.abspath(moved) and os.path.exists(path):
            shutil.move(path, moved)
        print(f"    ok {moved} ({os.path.getsize(moved) / 1024 ** 3:.2f} GB)")

    print("\nall downloads complete")


if __name__ == "__main__":
    main()
