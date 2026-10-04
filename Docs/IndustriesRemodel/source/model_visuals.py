"""Fitted visual carters and an industrial gantry; native VPE collisions stay in Unity.
Uses measured surface meshes exported by the live Editor. Real metres, beveled solids.
Run Blender --background --factory-startup --python this_file.py.
"""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[3]
GUIDES=ROOT/'Docs/IndustriesRemodel/source/panel-guides.json'
OUT=ROOT/'Assets/Models/Industries/WorkshopCarters.fbx'
OUT.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1
def point(v): return Vector((-v[0],-v[2],v[1]))
def mat(name):
    m=bpy.data.materials.new(name);m.diffuse_color=(.1,.2,.22,1);return m
PAINT=mat('WorkshopPanel');BRASS=mat('WorkshopCopper');STEEL=mat('WorkshopSteel')
def bevel(obj,width=.001):
    mod=obj.modifiers.new('Real softened edges','BEVEL');mod.width=width;mod.segments=2
    mod.limit_method='ANGLE'
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
def cube(name,center,size,material,parent=None,width=.001):
    bpy.ops.mesh.primitive_cube_add(size=1,location=point(center));o=bpy.context.object;o.name=name
    o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(material);bevel(o,width)
    if parent:o.parent=parent
    return o
def cylinder(name,center,radius,depth,material,parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=depth,location=point(center));o=bpy.context.object;o.name=name
    o.data.materials.append(material);bevel(o,.0003)
    if parent:o.parent=parent
    return o
def group(name):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);return o
def project_uv(obj):
    uv=obj.data.uv_layers.new(name='PlayfieldProjection')
    for poly in obj.data.polygons:
        for index in poly.loop_indices:
            v=obj.matrix_world@obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv=(-v.x/.5138,1-v.y/1.1669)
def tube(name,a,b,r,material,parent):
    aa,bb=point(a),point(b);delta=bb-aa
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=delta.length,location=(aa+bb)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();o.data.materials.append(material);o.parent=parent
    return o

for spec in json.loads(GUIDES.read_text()):
    root=group('Carter_'+spec['name'])
    vs=[point(v) for v in spec['vertices']]
    triangles=[spec['triangles'][i:i+3] for i in range(0,len(spec['triangles']),3)]
    mesh=bpy.data.meshes.new(spec['name']+'_fitted');mesh.from_pydata(vs,[],triangles);mesh.update()
    o=bpy.data.objects.new('EnamelShell',mesh);bpy.context.collection.objects.link(o);o.parent=root
    o.data.materials.append(PAINT)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00002)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    # Make upward-facing top before thickening downwards, within the existing footprint.
    for face in bm.faces:
        if face.normal.z<0:face.normal_flip()
    boundary=[e for e in bm.edges if e.is_boundary]
    edge_points=[(e.verts[0].co.copy(),e.verts[1].co.copy()) for e in boundary]
    bm.to_mesh(mesh);bm.free()
    mod=o.modifiers.new('3mm enamel carter','SOLIDIFY');mod.thickness=.003;mod.offset=-1
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    bevel(o,.0008);project_uv(o)
    # Copper perimeter and low hex fasteners, all inside the original surface polygon.
    count=0
    for a,b in edge_points:
        ua=(-a.x,a.z,-a.y);ub=(-b.x,b.z,-b.y)
        ua=(ua[0],ua[1]+.0004,ua[2]);ub=(ub[0],ub[1]+.0004,ub[2])
        tube('CopperSeam',ua,ub,.00065,BRASS,root)
        if (b-a).length>.035:
            for k in [.18,.82]:
                v=a.lerp(b,k);c=(-v.x,v.z+.0008,-v.y)
                cylinder('HexFastener',c,.0018,.0012,STEEL,root);count+=1
    # Join by material per carter to keep a few draw calls, while individual carters stay editable.
    for material in [PAINT,BRASS,STEEL]:
        children=[x for x in root.children if x.type=='MESH' and x.data.materials[0]==material]
        if not children:continue
        bpy.ops.object.select_all(action='DESELECT')
        for c in children:c.select_set(True)
        bpy.context.view_layer.update()
        bpy.context.view_layer.objects.active=children[0]
        if len(children)>1:bpy.ops.object.join()
        children[0].name=material.name

# A real truss at the back of the table establishes a factory silhouette.
# Beam underside is above the native ramp maximum + a 27mm ball.
# Pillars sit on the outer cabinet rails, outside the playable lanes.
root=group('FactoryGantry')
for x in [-.012,.528]:
    cube('Foot', (x,.033,-.20),(.027,.006,.032),BRASS,root)
    cube('Upright',(x,.087,-.20),(.012,.108,.018),STEEL,root)
    for y in [.065,.112]:cube('CopperBand',(x,y,-.20),(.014,.004,.020),BRASS,root,.0003)
cube('LowerChord',(.258,.115,-.20),(.552,.008,.022),STEEL,root)
cube('UpperChord',(.258,.140,-.20),(.572,.009,.024),STEEL,root)
for i in range(10):
    x=-.012+i*.054
    tube('TrussBrace',(x,.118,-.20),(x+.054,.137,-.20),.002,BRASS,root)
    tube('TrussBrace',(x,.137,-.20),(x+.054,.118,-.20),.002,BRASS,root)
cube('EnamelSign',(.258,.133,-.213),(.22,.032,.003),PAINT,root)
for material in [PAINT,BRASS,STEEL]:
    children=[x for x in root.children if x.type=='MESH' and x.data.materials[0]==material]
    bpy.ops.object.select_all(action='DESELECT')
    for c in children:c.select_set(True)
    bpy.context.view_layer.update()
    bpy.context.view_layer.objects.active=children[0]
    if len(children)>1:bpy.ops.object.join()
    children[0].name=material.name

# Normals and units checked on final baked meshes; no scale on descendants.
triangles=0
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH':continue
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
for root in [o for o in bpy.context.scene.objects if o.type=='EMPTY']:
    vertices=[o.matrix_world@v.co for o in root.children if o.type=='MESH' for v in o.data.vertices]
    dims=[max(v[i] for v in vertices)-min(v[i] for v in vertices) for i in range(3)]
    print(root.name,'Blender size', [round(x,5) for x in dims])
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,add_leaf_bones=False)
print(f'WorkshopCarters: 8 fitted carters + gantry, {triangles} triangles; metres; visual-only.')
