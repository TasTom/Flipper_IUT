"""Dump the glTF JSON chunk of a .glb file (structures only, no binary).

Usage: python Tools/inspect_glb.py <file.glb> [--mat]
"""
import json
import struct
import sys


def read_glb_json(path):
    with open(path, "rb") as fh:
        data = fh.read()
    magic, version, _length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        raise SystemExit(f"{path} is not a GLB (magic={magic!r})")
    _len, ctype = struct.unpack_from("<I4s", data, 12)
    if ctype != b"JSON":
        raise SystemExit(f"first chunk is {ctype!r}, expected JSON")
    return json.loads(data[20:20 + _len].decode("utf-8")), version


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    doc, version = read_glb_json(sys.argv[1])

    print(f"glTF {version}  generator={doc.get('asset', {}).get('generator')!r}")
    print(f"extensionsUsed: {doc.get('extensionsUsed')}")
    print(f"extensionsRequired: {doc.get('extensionsRequired')}")

    for key in ("scenes", "nodes", "meshes", "materials", "textures", "images",
                "samplers", "accessors", "bufferViews", "animations", "skins"):
        items = doc.get(key) or []
        print(f"{key:14} = {len(items)}")

    for img in doc.get("images") or []:
        print(f"  image: name={img.get('name')!r} mime={img.get('mimeType')} "
              f"uri={'<external>' if 'uri' in img else 'bufferView'} "
              f"bufferView={img.get('bufferView')}")
    for tex in doc.get("textures") or []:
        print(f"  texture: name={tex.get('name')!r} source={tex.get('source')}")

    if "--mat" in sys.argv:
        for mat in doc.get("materials") or []:
            pbr = mat.get("pbrMetallicRoughness", {})
            print(f"  material {mat.get('name')!r}: "
                  f"baseColorFactor={pbr.get('baseColorFactor')} "
                  f"baseColorTexture={pbr.get('baseColorTexture')} "
                  f"metallic={pbr.get('metallicFactor')} rough={pbr.get('roughnessFactor')}")


if __name__ == "__main__":
    main()
