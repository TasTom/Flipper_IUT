"""Decimate generated GLB meshes down to a game-ready triangle budget.

Modly's processors (mesh-optimizer, mesh-remesher, ...) are only reachable from its GUI:
the canonical `/process-runs` API returns 404 in 0.4.2. So decimation happens here with
pymeshlab (quadric edge collapse), which ships in Modly's own venv:

    C:\\Users\\<you>\\Documents\\Modly\\dependencies\\venv\\Scripts\\python.exe decimate_glb.py <files...>

Hunyuan3D mini emits ~0.5-1M triangles per prop at octree resolution 380, which is far too
heavy for a pinball table. Each part is reduced to --faces (default 8000) while preserving
boundaries; typical result is a 10-60x reduction that is visually indistinguishable at
game scale.

Usage:
  python decimate_glb.py mesh.glb [more.glb ...] [--faces 8000] [--ratio 0.01] [--in-place]
"""
import argparse
import os
import shutil
import sys


def count_faces(path):
    """Triangle count without importing the mesh (reads the glTF JSON chunk)."""
    import json
    import struct
    with open(path, "rb") as f:
        magic, _, _ = struct.unpack("<4sII", f.read(12))
        if magic != b"glTF":
            return -1
        chunk_len, chunk_type = struct.unpack("<I4s", f.read(8))
        if chunk_type != b"JSON":
            return -1
        gltf = json.loads(f.read(chunk_len))
    total = 0
    for mesh in gltf.get("meshes", []):
        for prim in mesh.get("primitives", []):
            mode = prim.get("mode", 4)
            if mode != 4 or "indices" not in prim:
                continue
            total += gltf["accessors"][prim["indices"]]["count"] // 3
    return total


def decimate(src, dest, faces, ratio):
    """Quadric-decimate src and write a GLB to dest.

    pymeshlab cannot write glTF ("Unknown format for save: glb"), so the reduced mesh goes
    through a temporary PLY and trimesh performs the GLB export.
    """
    import pymeshlab
    import trimesh

    ms = pymeshlab.MeshSet()
    ms.load_new_mesh(src)
    before = ms.current_mesh().face_number()

    target = max(100, int(round(before * ratio))) if ratio else faces
    if before > target:
        ms.meshing_decimation_quadric_edge_collapse(
            targetfacenum=target,
            preserveboundary=True,
            planarquadric=True,
        )
    after = ms.current_mesh().face_number()

    tmp_ply = dest + ".tmp.ply"
    ms.save_current_mesh(tmp_ply, save_vertex_normal=True)

    mesh = trimesh.load(tmp_ply, process=False)
    mesh.export(dest, file_type="glb")
    os.remove(tmp_ply)
    return before, after


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("meshes", nargs="+")
    ap.add_argument("--faces", type=int, default=8000, help="target triangles per mesh")
    ap.add_argument("--ratio", type=float, default=0.0,
                    help="alternative to --faces: keep this fraction of faces (0 = use --faces)")
    ap.add_argument("--out-dir", default=None, help="default: <mesh dir>/decimated")
    ap.add_argument("--in-place", action="store_true", help="overwrite the source files")
    args = ap.parse_args()

    total_before = total_after = 0
    for path in args.meshes:
        if not os.path.isfile(path):
            print(f"skip (missing): {path}")
            continue

        if args.in_place:
            dest = path
            tmp = path + ".tmp.glb"
        else:
            out_dir = args.out_dir or os.path.join(os.path.dirname(os.path.abspath(path)), "decimated")
            os.makedirs(out_dir, exist_ok=True)
            dest = os.path.join(out_dir, os.path.basename(path))
            tmp = dest

        before, after = decimate(path, tmp, args.faces, args.ratio)
        if args.in_place and os.path.abspath(tmp) != os.path.abspath(path):
            shutil.move(tmp, path)

        total_before += before
        total_after += after
        size_kb = os.path.getsize(dest) / 1024
        print(f"{os.path.basename(path):<26} {before:>9,} -> {after:>7,} tris   "
              f"({before / max(after, 1):.1f}x)   {size_kb:.0f} KB   {dest}")

    if total_before:
        print(f"\ntotal {total_before:,} -> {total_after:,} triangles "
              f"({total_before / max(total_after, 1):.1f}x reduction)")


if __name__ == "__main__":
    main()
