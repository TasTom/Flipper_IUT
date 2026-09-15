"""Search Hugging Face for repos containing a given filename (exact match on rfilename)."""
import json
import sys
import urllib.parse
import urllib.request

filename = sys.argv[1]
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 8

query = urllib.parse.quote(filename)
url = f"https://huggingface.co/api/models?search={query}&limit=60&full=true"
with urllib.request.urlopen(url) as r:
    models = json.load(r)

hits = 0
for m in models:
    for s in m.get("siblings", []) or []:
        name = s.get("rfilename", "")
        if name.endswith(filename):
            size = s.get("size")
            size_gb = f"{size / 1024 ** 3:.2f} GB" if size else "?"
            print(f"{m['id']}  {name}  {size_gb}  downloads={m.get('downloads')}")
            hits += 1
            break
    if hits >= limit:
        break

if hits == 0:
    print("no exact match; candidate repos:")
    for m in models[:limit]:
        print(" ", m["id"])
