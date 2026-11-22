"""Export the five Industries tabletop mechanisms on editable pivots, in metres."""
from collections import defaultdict
from math import cos, pi, radians, sin
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Vector


OUT = Path(__file__).resolve().parents[2] / "Assets/Models/Industries/Kinetics"
MM = .001
COLORS = {
    "Steel": (.30, .38, .40), "Copper": (.66, .35, .12), "Panel": (.07, .14, .16),
    "Ivory": (.77, .64, .40), "Enamel": (.15, .30, .28), "Rubber": (.033, .037, .035),
}


def metres(value):
    return Vector(value) * MM


class Assembly:
    def __init__(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.materials = {}
        self.groups = defaultdict(list)
        for name, color in COLORS.items():
            mat = bpy.data.materials.new(name)
            mat.diffuse_color = (*color, 1)
            self.materials[name] = mat
        self.base = self.pivot("Housing", (0, 0, 0))

    def pivot(self, name, point):
        group = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(group)
        group.location = metres(point)
        return group

    def finish(self, obj, material, group, bevel, segments=2):
        obj.data.materials.append(self.materials[material])
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if bevel:
            mod = obj.modifiers.new("Machined corners", "BEVEL")
            mod.width, mod.segments = bevel * MM, segments
            mod.harden_normals = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
        self.groups[group or self.base, material].append(obj)

    def box(self, point, size, mat, group=None, bevel=.55, angle=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=metres(point))
        obj = bpy.context.object
        obj.scale = metres(size)
        obj.rotation_euler.z = radians(angle)
        self.finish(obj, mat, group, bevel)

    def tube(self, a, b, radius, mat, group=None, sides=16, bevel=.26):
        a, b = Vector(a), Vector(b)
        delta = b - a
        bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius*MM, depth=delta.length*MM,
                                          location=metres((a+b)*.5))
        obj = bpy.context.object
        obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
        for face in obj.data.polygons:
            face.use_smooth = len(face.vertices) == 4
        self.finish(obj, mat, group, bevel, 1)

    def baseplate(self, width, depth):
        self.box((0, 0, 1.35), (width, depth, 2.7), "Panel", bevel=1.15)
        self.box((0, 0, 2.66), (width-3, depth-3, .65), "Steel", bevel=.29)
        for x in (-width/2+3.6, width/2-3.6):
            for y in (-depth/2+3.4, depth/2-3.4):
                self.tube((x, y, 2.8), (x, y, 3.65), 1.12, "Copper", sides=8, bevel=.12)

    def gear(self, name, x, y, radius, teeth, top, mat):
        group = self.pivot(name, (x, y, top))
        steps = teeth * 4
        verts = []
        # Continuous toothed annulus. The central opening exposes the spokes.
        for i in range(steps):
            angle = 2*pi*i/steps
            edge = radius if i % 4 in (1, 2) else radius-1.45
            verts.extend(metres((x+r*cos(angle), y+r*sin(angle), z))
                         for r, z in ((radius*.65, top-2.4), (edge, top-2.4),
                                      (edge, top), (radius*.65, top)))
        faces = []
        for i in range(steps):
            a, b = 4*i, 4*((i+1)%steps)
            faces.extend((a+j, a+(j+1)%4, b+(j+1)%4, b+j) for j in range(4))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name+"_profile", mesh)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        self.finish(obj, mat, group, .16, 1)
        for i in range(5):
            angle = 2*pi*i/5
            dx, dy = cos(angle), sin(angle)
            self.box((x+dx*radius*.40, y+dy*radius*.40, top-1.2),
                     (radius*.64, 2.2, 2), mat, group, .27, i*72)
        self.tube((x, y, 3), (x, y, top+1.1), 2.85, "Panel", group)
        self.tube((x, y, top+.9), (x, y, top+1.65), 1.8, "Ivory", group, sides=10)
        self.box((x+radius*.82, y, top+.14), (1.95, 1.5, .52), "Ivory", group, .15)

    def export(self, name):
        tris = 0
        bpy.context.view_layer.update()
        for (group, material), parts in self.groups.items():
            bpy.ops.object.select_all(action="DESELECT")
            for part in parts:
                part.select_set(True)
            bpy.context.view_layer.objects.active = parts[0]
            if len(parts) > 1:
                bpy.ops.object.join()
            obj = bpy.context.object
            obj.name = f"{group.name}_{material}"
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            obj.parent = group
            obj.matrix_parent_inverse = group.matrix_world.inverted()
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
            assert all(e.is_manifold for e in bm.edges), obj.name
            assert all(f.calc_area() > 1e-13 for f in bm.faces), obj.name
            bmesh.ops.triangulate(bm, faces=list(bm.faces), ngon_method="EAR_CLIP")
            tris += len(bm.faces)
            bm.to_mesh(obj.data)
            bm.free()
        assert tris < 9000, (name, tris)
        OUT.mkdir(parents=True, exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=str(OUT/f"{name}.fbx"), axis_forward="-Z", axis_up="Y",
            apply_unit_scale=True, use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
            object_types={"EMPTY", "MESH"})
        print(f"{name}: {tris} triangles")


def flywheel():
    a = Assembly()
    a.baseplate(61, 34)
    a.box((0, 0, 4), (55, 25, 2.3), "Enamel", bevel=.8)
    a.gear("Drive", -12, 0, 13, 20, 9.8, "Copper")
    a.gear("Follower", 8.8, 0, 9, 14, 9.8, "Ivory")
    a.box((24, 0, 5), (6.5, 18, 4), "Panel", bevel=1.1)
    for y in (-4.8, 0, 4.8):
        a.box((24, y, 7.25), (3, 1.8, .55), "Copper")
    a.export("ApronFlywheel")


def pump():
    a = Assembly()
    a.baseplate(61, 34)
    for index, y in enumerate((-7.3, 7.3), start=1):
        a.box((0, y, 4.4), (49, 11, 3.2), "Panel")
        a.tube((-24, y, 8), (-8, y, 8), 4.6, "Copper")
        for x in (-24, -10, -6):
            a.tube((x, y, 8), (x+2.3, y, 8), 5.2, "Rubber")
        a.box((20.5, y, 7.3), (7, 9, 9.6), "Enamel", bevel=1.1)
        slide = a.pivot(f"Piston{index}", (2, y, 8))
        a.tube((-10, y, 8), (23, y, 8), 1.8, "Steel", slide)
        a.tube((1, y, 8), (5, y, 8), 3.5, "Ivory", slide)
        a.tube((7, y, 8), (11, y, 8), 2.6, "Copper", slide)
    a.export("ApronPump")


def spindle():
    a = Assembly()
    a.baseplate(26, 34)
    for index, y in enumerate((-7, 7), start=1):
        for x in (-9, 9):
            a.box((x, y, 6.2), (3.4, 8, 8.1), "Enamel", bevel=.8)
        turn = a.pivot(f"Spool{index}", (0, y, 10))
        a.tube((-11, y, 10), (11, y, 10), 1.8, "Steel", turn)
        a.tube((-6, y, 10), (6, y, 10), 3.65, "Ivory" if index==1 else "Copper", turn)
        for x in (-6, 6):
            a.tube((x-.75, y, 10), (x+.75, y, 10), 5.1, "Copper", turn)
        for x in (-3.8, -1.2, 1.4, 4):
            a.tube((x-.3, y, 10), (x+.3, y, 10), 3.95, "Rubber", turn, bevel=.12)
        a.box((.6, y, 13.9), (7.6, 1.4, .65), "Enamel", turn, .22)
    a.export("TextileSpindle")


def regulator():
    a = Assembly()
    a.baseplate(33, 33)
    a.tube((-5, 2, 3), (-5, 2, 9), 10.1, "Copper", sides=32)
    a.tube((-5, 2, 8.9), (-5, 2, 9.9), 8.45, "Panel", sides=32)
    a.tube((-5, 2, 9.85), (-5, 2, 10.3), 7.4, "Ivory", sides=32, bevel=.13)
    for i in range(9):
        angle = radians(-20+i*27.5)
        a.box((-5+5.85*cos(angle), 2+5.85*sin(angle), 10.5),
              (1.65, .62, .43), "Rubber", bevel=.13, angle=-20+i*27.5)
    arm = a.pivot("Needle", (-5, 2, 11))
    a.box((-2.6, 2, 11), (6.9, 1.22, .57), "Copper", arm, .22)
    a.tube((-5, 2, 10.6), (-5, 2, 11.55), 1.6, "Rubber", arm, sides=12)
    a.tube((9, -6, 3), (9, -6, 11), 4.25, "Enamel")
    a.tube((9, -6, 8), (9, 9, 8), 2.0, "Copper")
    relief = a.pivot("Relief", (9, -6, 10))
    a.tube((9, -6, 9), (9, -6, 13), 1.6, "Steel", relief)
    a.tube((9, -6, 12), (9, -6, 14.5), 3.4, "Copper", relief, sides=16)
    a.export("EnergyRegulator")


def conveyor():
    a = Assembly()
    a.baseplate(32, 64)
    for x in (-12, 12):
        a.box((x, 0, 5.2), (4.4, 56, 5.1), "Enamel", bevel=.85)
        a.box((x, 0, 8), (2.4, 55, 1.1), "Copper", bevel=.44)
    for y in range(-24, 25, 8):
        turn = a.pivot(f"Roller{(y+24)//8}", (0, y, 6.6))
        a.tube((-10.4, y, 6.6), (10.4, y, 6.6), 3.05, "Steel", turn, sides=16)
        for x in (-5, 5):
            a.tube((x-1.25, y, 6.6), (x+1.25, y, 6.6), 3.2, "Rubber", turn, sides=16)
        a.box((0, y, 9.59), (4.5, 1.08, .53), "Ivory", turn, .17)
    shuttle = a.pivot("Shuttle", (0, 0, 10))
    a.box((0, 0, 10.6), (17, 13, 2), "Copper", shuttle, .66)
    a.box((0, 0, 13.3), (12.2, 9, 4.4), "Panel", shuttle, 1)
    for x in (-3.2, 3.2):
        a.box((x, 0, 15.55), (2, 6, .55), "Ivory", shuttle, .2, 20)
    a.export("RampConveyor")


def furnace():
    a = Assembly()
    a.baseplate(35, 36)
    contour = [(-.78, -1), (.78, -1), (1, -.72), (1, .72),
               (.78, 1), (-.78, 1), (-1, .72), (-1, -.72)]

    def casing(name, rings, material, bevel):
        verts = [metres((x * w, y * d, z)) for w, d, z in rings for x, y in contour]
        faces = []
        sides = len(contour)
        for level in range(len(rings)):
            current = level * sides
            following = (level + 1) % len(rings) * sides
            for i in range(sides):
                j = (i + 1) % sides
                faces.append((current+i, current+j, following+j, following+i))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        a.finish(obj, material, None, bevel, 1)

    casing("Firebox", [(11.4, 11.8, 3), (14.8, 14.4, 11.5),
        (11.4, 11, 11.5), (8.4, 8.9, 3.7)], "Panel", .36)
    casing("CopperRim", [(14.4, 14, 11), (15.25, 14.75, 12.6),
        (11.65, 11.3, 12.6), (11.4, 11, 11)], "Copper", .25)
    casing("HeatShield", [(13.7, 13.3, 8.9), (14.9, 14.5, 9.1),
        (15.0, 14.6, 10), (14, 13.55, 10)], "Steel", .16)
    a.box((0, 0, 4.9), (21, 20, 1.7), "Rubber", bevel=.7)
    for x, y, angle in [(-6, -5, -18), (4.5, -6, 25), (-4, 5, 34), (5.3, 4.6, -47), (0, -.2, 13)]:
        a.box((x, y, 6.25), (7.3, 5.8, 2.5), "Rubber", bevel=.85, angle=angle)
    for x in (-9.3, 9.3):
        for y in (-10.5, 10.5):
            a.tube((x, y, 12.25), (x, y, 13.15), 1.02, "Steel", sides=8, bevel=.13)
    for j in range(-2, 3):
        a.box((j * 3.25, 12.85, 7.4), (1.65, 1.8, 2.9), "Rubber", bevel=.24)
    a.export("FoundryFurnace")


if "--furnace" in sys.argv:
    furnace()
else:
    for build in (flywheel, pump, spindle, regulator, conveyor):
        build()
