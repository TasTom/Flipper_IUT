"""Build the original Industries bumper boss at the VPE table's metre scale."""
import math
from collections import defaultdict
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
EXPORT = ROOT / "Assets/Resources/Industries/Automate.fbx"
MM = .001
bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


iron = material("Iron", (.061, .087, .090), .52, .41)
steel = material("Steel", (.39, .46, .46), .68, .31)
enamel = material("Enamel", (.21, .29, .16), .36, .38)
brass = material("Brass", (.64, .32, .09), .62, .35)
rubber = material("Rubber", (.016, .022, .023), .12, .58)
inlay = material("Hazard", (.95, .55, .055), .18, .44)
glow = material("Signal", (.92, .055, .012), .11, .27)


def mm(v):
    return Vector(v) * MM


def pivot(name, pos=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.location = mm(pos)
    return obj


base = pivot("Chassis")
head = pivot("Head", (0, -2, 35))
groups = defaultdict(list)


def finish(obj, mat, group, bevel):
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Machined edges", "BEVEL")
        mod.width = bevel * MM
        mod.segments = 2
        mod.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = obj.modifiers.new("Corner normals", "WEIGHTED_NORMAL")
        mod.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    groups[group, mat.name].append(obj)
    return obj


def box(pos, size, mat, group=base, bevel=1., angles=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=mm(pos))
    obj = bpy.context.object
    obj.scale = mm(size)
    obj.rotation_euler = tuple(map(math.radians, angles))
    return finish(obj, mat, group, bevel)


def cylinder(pos, radius, depth, mat, group=base, segments=24, rotation=None, bevel=.6):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius*MM, depth=depth*MM, location=mm(pos))
    obj = bpy.context.object
    for face in obj.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    if rotation:
        obj.rotation_euler = rotation
    return finish(obj, mat, group, bevel)


def shaft(start, end, radius, mat, group=base, segments=12):
    a, b = Vector(start), Vector(end)
    length = (b-a).length
    return cylinder((a+b)*.5, radius, length, mat, group, segments,
                    (b-a).to_track_quat("Z", "Y").to_euler(), .32)


def ring_section(start, size, inner, outer, bottom, top, mat):
    segments = max(2, round(size / 10))
    verts = []
    for i in range(segments+1):
        a = math.radians(start + size*i/segments)
        verts.extend(mm((r*math.cos(a), r*math.sin(a), z))
                     for r, z in ((inner, bottom), (outer, bottom), (outer, top), (inner, top)))
    faces = [(3, 2, 1, 0)]
    for i in range(segments):
        k = 4*i
        faces.extend((k+j, k+(j+1)%4, k+(j+1)%4+4, k+j+4) for j in range(4))
    faces.append(tuple(4*segments+i for i in range(4)))
    mesh = bpy.data.meshes.new("ImpactGuard")
    mesh.from_pydata(verts, [], faces)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("ImpactGuard", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for selected in bpy.context.selected_objects:
        selected.select_set(selected == obj)
    finish(obj, mat, base, min(.35, (top-bottom)*.18))


cylinder((0, 0, 2.7), 26, 5.4, iron, segments=36)
cylinder((0, 0, 8), 23.1, 6.7, rubber, segments=36)
cylinder((0, 0, 13), 20.7, 10.3, brass, segments=36)
cylinder((0, 0, 20), 17.2, 12, iron)
for slot in range(12):
    angle = slot * 30 + 2
    ring_section(angle, 26, 21.5, 27, 6, 15, brass if slot % 3 == 0 else steel)
    ring_section(angle + 4, 15, 24, 26, 14.9, 15.8, inlay if slot % 3 == 0 else rubber)

box((0, -1, 26), (32, 30, 14), enamel, bevel=3.1)
box((0, 12.7, 26), (28, 8, 10), brass, bevel=2.2)
for j in range(5):
    box(((j-2)*4.5, 16.1, 28), (2.3, 2.3, 6), iron, bevel=.4, angles=(0, 19, 0))
for side in (-1, 1):
    x = side * 20.7
    shaft((side*14, 4, 13), (x, 3, 27), 5.9, iron)
    shaft((side*16, 4, 15), (x, 3, 28), 2.8, steel)
    shaft((side*15, -4, 28), (side*25, -4, 28), 7, brass, segments=16)
    shaft((side*23, -4, 28), (side*27, -4, 28), 5.1, rubber, segments=12)
    shaft((side*26, -4, 28), (side*27.5, -4, 28), 2.1, inlay, segments=8)
    for y in (-11, 11):
        cylinder((side*15.4, y, 32.7), 1.6, 1.2, steel, segments=8, bevel=.22)

cylinder((0, -2, 34), 8.2, 7.7, steel, head, segments=16)
box((0, -2, 41), (32, 30, 17), enamel, head, bevel=4)
box((0, -5, 48.4), (25, 21, 6), steel, head, bevel=2.6)
box((0, 8, 44), (29, 12, 10), iron, head, bevel=2.5, angles=(-19, 0, 0))
box((0, 9, 38), (22, 13, 5), brass, head, bevel=1.5)
for side in (-1, 1):
    box((side*7, 13.4, 45.2), (11, 3.8, 4.4), glow, head, bevel=.85,
        angles=(-30, side*13, 0))
    box((side*8.6, 12.1, 48), (13, 6, 3.3), steel, head, bevel=.7,
        angles=(-20, side*14, side*5))
    shaft((side*14, -4, 42), (side*18, -4, 42), 4.4, brass, head)
for j in range(3):
    box(((j-1)*4, -6, 51.4), (2, 9, 1.4), iron, head, bevel=.4)
box((0, -12, 50.6), (5, 4, 3), glow, head, bevel=.8)

triangles = 0
for (group, mat), objects in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = group.name + "_" + mat
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.parent = group
    obj.matrix_parent_inverse = group.matrix_world.inverted()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    assert all(edge.is_manifold for edge in bm.edges), obj.name
    assert all(face.calc_area() > 1e-12 for face in bm.faces), obj.name
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    triangles += len(bm.faces)
    bm.to_mesh(obj.data)
    bm.free()

assert triangles < 14000, triangles
EXPORT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(EXPORT), axis_forward="-Z", axis_up="Y",
    apply_unit_scale=True, use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
    object_types={"EMPTY", "MESH"})
print(f"Automate: {triangles} triangles, {EXPORT.stat().st_size} bytes")
