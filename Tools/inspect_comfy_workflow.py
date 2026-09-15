"""Inspect a ComfyUI workflow JSON (API or UI format) to list loader nodes and model files."""
import json
import sys
import urllib.request

url = sys.argv[1]
with urllib.request.urlopen(url) as r:
    raw = r.read()

data = json.loads(raw)
print(f"bytes={len(raw)} type={type(data).__name__}")
if isinstance(data, dict):
    print("top keys:", list(data)[:12])

nodes = []
if isinstance(data, dict) and isinstance(data.get("nodes"), list):
    for n in data["nodes"]:
        nodes.append((n.get("id"), n.get("type"), n.get("widgets_values"), n.get("properties", {})))
elif isinstance(data, dict):
    for node_id, node in data.items():
        if isinstance(node, dict) and node.get("class_type"):
            nodes.append((node_id, node["class_type"], node.get("inputs"), None))

INTEREST = ("loader", "clip", "vae", "sampler", "model", "encode", "lora", "scheduler", "latent")

print(f"nodes={len(nodes)}")
for nid, ntype, widgets, props in nodes:
    if ntype and any(k in ntype.lower() for k in INTEREST):
        extra = ""
        if isinstance(props, dict) and props.get("models"):
            extra = " models=" + json.dumps(props["models"], ensure_ascii=False)
        print(f"{nid} {ntype} widgets={json.dumps(widgets, ensure_ascii=False)[:300]}{extra}")

