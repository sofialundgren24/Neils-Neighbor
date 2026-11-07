"""Bygger fas 0-testassets i Blender enligt GDD:ns 3D-specifikation och exporterar FBX till Unity.

  - SM_House_Cottage        4 renoveringsfaser (Phase0_Decayed .. Phase3_Decorated)
  - SM_Flower_Rose_Pink     3 tillväxtsteg (Stage0_Sprout, Stage1_Growing, Stage2_Bloom)
  - SM_Flower_Tulip_Yellow  3 tillväxtsteg
  - SM_Flower_Lavender_Purple 3 tillväxtsteg
  - SM_Tree_Blossom_Pink    ett stadium

Allt följer specen: 1 enhet = 1 m, pivot bottom-center på z = 0, UV mot T_Palette_256,
vertexfärg R = AO, G = vind, B = målarmask, avfasade kanter, ett material per objekt.

Körs (headless):
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup -P Tools/blender/make_test_assets.py
Valfritt: lägg till  -- --preview  för att också rendera en förhandsbild till ArtSource/preview.png
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "palette"))
from palette_uv import uv_for  # noqa: E402

INCOMING = ROOT / "Assets" / "_Game" / "Art" / "Incoming"
SOURCE = ROOT / "ArtSource"
PALETTE_PNG = ROOT / "Assets" / "_Game" / "Art" / "Textures" / "T_Palette_256.png"


def clamp01(x):
    return max(0.0, min(1.0, x))


def lerp(a, b, t):
    return a + (b - a) * t


# --------------------------------------------------------------------------- byggstenar

class Variant:
    """Samlar delar (en mesh per del) och slår ihop dem till ETT objekt med variantens namn."""

    def __init__(self, name, ao_fn, wind_fn=None):
        self.name = name
        self.ao_fn = ao_fn
        self.wind_fn = wind_fn or (lambda co: 0.0)
        self.parts = []

    def _finish(self, bm, color, paint, smooth, bevel):
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1,
                            affect='EDGES', profile=0.5, clamp_overlap=True)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        uv_layer = bm.loops.layers.uv.new("UVMap")
        col_layer = bm.loops.layers.float_color.new("Color")
        for f in bm.faces:
            f.smooth = smooth
            nz = f.normal.z
            along = 0.8 if nz > 0.5 else (0.25 if nz < -0.5 else 0.55)
            uv = uv_for(color, along)
            for loop in f.loops:
                co = loop.vert.co
                loop[uv_layer].uv = uv
                loop[col_layer] = (self.ao_fn(co), self.wind_fn(co), 1.0 if paint else 0.0, 1.0)
        me = bpy.data.meshes.new(f"{self.name}_part")
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new(me.name, me)
        bpy.context.scene.collection.objects.link(obj)
        self.parts.append(obj)
        return obj

    @staticmethod
    def _matrix(loc, rot, scale):
        rx, ry, rz = (math.radians(a) for a in rot)
        return (Matrix.Translation(Vector(loc))
                @ Matrix.Rotation(rz, 4, 'Z') @ Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X')
                @ Matrix.Diagonal((*scale, 1.0)))

    def box(self, loc, size, color, rot=(0, 0, 0), bevel=0.03, paint=False):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.transform(bm, matrix=self._matrix(loc, rot, size), verts=bm.verts)
        return self._finish(bm, color, paint, False, bevel)

    def cylinder(self, loc, r1, r2, height, color, segments=6, rot=(0, 0, 0), caps=True, bevel=0.0, smooth=False):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=segments,
                              radius1=r1, radius2=r2, depth=height)
        # create_cone är centrerad – flytta så att basen ligger i loc.
        bmesh.ops.translate(bm, vec=Vector((0, 0, height / 2)), verts=bm.verts)
        bmesh.ops.transform(bm, matrix=self._matrix(loc, rot, (1, 1, 1)), verts=bm.verts)
        return self._finish(bm, color, False, smooth, bevel)

    def blob(self, loc, scale, color, rot=(0, 0, 0), detail=1, smooth=True):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=detail, radius=1.0)
        bmesh.ops.transform(bm, matrix=self._matrix(loc, rot, scale), verts=bm.verts)
        return self._finish(bm, color, False, smooth, 0.0)

    def gable(self, center, width, depth, eave, ridge, color, paint=False):
        """Gavelprisma under taket: bas width (x) x depth (y) på höjd eave, nock på höjd ridge längs x."""
        bm = bmesh.new()
        cx, cy = center
        hw, hd = width / 2, depth / 2
        v = [bm.verts.new(p) for p in (
            (cx - hw, cy - hd, eave), (cx - hw, cy + hd, eave), (cx - hw, cy, ridge),
            (cx + hw, cy - hd, eave), (cx + hw, cy + hd, eave), (cx + hw, cy, ridge))]
        for idx in ((0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (0, 3, 5, 2), (1, 2, 5, 4)):
            bm.faces.new([v[i] for i in idx])
        return self._finish(bm, color, paint, False, 0.0)

    def build(self):
        objs = self.parts
        with bpy.context.temp_override(active_object=objs[0], object=objs[0],
                                       selected_objects=objs, selected_editable_objects=objs):
            bpy.ops.object.join()
        obj = objs[0]
        obj.name = self.name
        obj.data.name = self.name
        attrs = obj.data.color_attributes
        if "Color" in attrs:
            attrs.active_color = attrs["Color"]
            if hasattr(attrs, "default_color_name"):
                attrs.default_color_name = "Color"
        self.parts = []
        return obj


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def export(asset_name, objects):
    INCOMING.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o in objects)
    bpy.context.view_layer.objects.active = objects[0]
    path = INCOMING / f"{asset_name}.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
        mesh_smooth_type='FACE', colors_type='LINEAR', add_leaf_bones=False, bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / f"{asset_name}.blend"), copy=True)
    counts = ", ".join(f"{o.name}: {tri_count(o)} tris" for o in objects)
    print(f"[NG] {asset_name}.fbx  ({counts})")


# --------------------------------------------------------------------------- huset

HOUSE_W, HOUSE_D, EAVE, RIDGE = 4.4, 4.0, 3.0, 4.6
FRONT_Y = -HOUSE_D / 2


def house_ao(co):
    return lerp(0.62, 1.0, clamp01(co.z / 0.9))


def house_shell(v, painted, roof_complete=True):
    v.box((0, 0, 0.15), (HOUSE_W + 0.4, HOUSE_D + 0.4, 0.3), "stone", bevel=0.04)
    v.box((0, 0, 0.3 + (EAVE - 0.3) / 2), (HOUSE_W, HOUSE_D, EAVE - 0.3), "facade_cream", paint=painted)
    v.gable((0, 0), HOUSE_W, HOUSE_D, EAVE, RIDGE, "facade_cream", paint=painted)

    # Taket: två avfasade plattor längs takfallet, med 0,35 m utsprång vid takfoten.
    overhang, thick = 0.35, 0.16
    k = (RIDGE - EAVE) / (HOUSE_D / 2)          # lutning
    angle = math.atan(k)
    run = HOUSE_D / 2 + overhang                 # horisontell längd nock -> takfot
    foot_z = EAVE - k * overhang
    length = math.hypot(run, RIDGE - foot_z)
    lift = (thick / 2) / math.cos(angle)         # plattan vilar ovanpå takfallet
    for side in (-1, 1):
        # Förfallen: framsidan (−y) har ett hål – bara övre 45 % av plattan finns kvar.
        frac = 0.45 if (not roof_complete and side == -1) else 1.0
        t = frac / 2                             # mittpunkt räknat från nocken
        y = side * run * t
        z = RIDGE - (RIDGE - foot_z) * t + lift
        v.box((0, y, z), (HOUSE_W + 0.5, length * frac, thick), "roof_red",
              rot=(math.degrees(side * -angle), 0, 0))
    v.box((0, 0, RIDGE + 0.05), (HOUSE_W + 0.55, 0.22, 0.14), "roof_red", bevel=0.04)


def windows(v, glass=True):
    spots = [((-1.3, FRONT_Y), 0), ((1.3, FRONT_Y), 0),
             ((-HOUSE_W / 2, -0.8), 90), ((-HOUSE_W / 2, 0.9), 90)]
    for (x, y), rot in spots:
        z = 1.85
        if rot == 0:
            v.box((x, y - 0.03, z), (1.0, 0.08, 1.0), "white_trim")
            if glass:
                v.box((x, y - 0.06, z), (0.78, 0.04, 0.78), "glass", bevel=0.01)
        else:
            v.box((x - 0.03, y, z), (0.08, 1.0, 1.0), "white_trim")
            if glass:
                v.box((x - 0.06, y, z), (0.04, 0.78, 0.78), "glass", bevel=0.01)


def door(v, color="roof_blue"):
    v.box((0, FRONT_Y - 0.03, 1.1), (1.05, 0.08, 1.95), "white_trim")
    v.box((0, FRONT_Y - 0.07, 1.05), (0.85, 0.06, 1.75), color)
    v.box((0.28, FRONT_Y - 0.12, 1.0), (0.08, 0.06, 0.08), "flower_yellow", bevel=0.01)


def build_house():
    objs = []

    # Phase0_Decayed: hål i taket, igenspikade fönster, lutande skorsten, skräp.
    v = Variant("Phase0_Decayed", house_ao)
    house_shell(v, painted=False, roof_complete=False)
    windows(v, glass=False)
    for x in (-1.3, 1.3):
        for k, rz in enumerate((-18, 14)):
            v.box((x, FRONT_Y - 0.1 - k * 0.03, 1.85), (1.1, 0.05, 0.16), "wood_dark", rot=(0, rz, 0), bevel=0.01)
    v.box((0, FRONT_Y - 0.06, 1.05), (0.85, 0.06, 1.75), "wood_dark")
    v.box((1.25, 0.6, 4.1), (0.5, 0.5, 1.3), "stone", rot=(0, 12, 0))
    for i, (x, y, s) in enumerate(((1.8, -2.6, 0.35), (2.2, -2.4, 0.25), (-2.0, -2.7, 0.3))):
        v.box((x, y, s / 2), (s * 1.4, s, s), "decay_dust", rot=(0, 0, i * 25))
    objs.append(v.build())

    # Phase1_Repaired: helt tak, glas, dörr, rak skorsten.
    def repaired(name, painted):
        v = Variant(name, house_ao)
        house_shell(v, painted=painted)
        windows(v)
        door(v)
        v.box((1.25, 0.6, 4.2), (0.5, 0.5, 1.4), "stone")
        return v

    objs.append(repaired("Phase1_Repaired", painted=False).build())

    # Phase2_Painted: fasaden får målarmask + fönsterluckor.
    v = repaired("Phase2_Painted", painted=True)
    for x in (-1.3, 1.3):
        for side in (-1, 1):
            v.box((x + side * 0.68, FRONT_Y - 0.06, 1.85), (0.3, 0.06, 1.0), "roof_green")
    objs.append(v.build())

    # Phase3_Decorated: blomlådor, markis, trappsteg, lyktor, buskar.
    v = repaired("Phase3_Decorated", painted=True)
    for x in (-1.3, 1.3):
        for side in (-1, 1):
            v.box((x + side * 0.68, FRONT_Y - 0.06, 1.85), (0.3, 0.06, 1.0), "roof_green")
        v.box((x, FRONT_Y - 0.22, 1.25), (1.05, 0.3, 0.22), "wood", bevel=0.02)
        for i, col in enumerate(("flower_pink", "flower_yellow", "flower_pink", "flower_purple")):
            v.blob((x - 0.36 + i * 0.24, FRONT_Y - 0.24, 1.42), (0.13, 0.13, 0.11), col)
    v.box((0, FRONT_Y - 0.55, 2.3), (1.5, 0.9, 0.08), "roof_red", rot=(-20, 0, 0), bevel=0.02)
    v.box((0, FRONT_Y - 0.4, 0.12), (1.4, 0.6, 0.24), "stone", bevel=0.04)
    for x in (-0.95, 0.95):
        v.cylinder((x, FRONT_Y - 0.55, 0.0), 0.04, 0.04, 1.2, "trim_dark")
        v.blob((x, FRONT_Y - 0.55, 1.3), (0.11, 0.11, 0.14), "flower_yellow")
    for x in (-1.9, 1.9):
        v.blob((x, FRONT_Y - 0.55, 0.35), (0.45, 0.4, 0.38), "leaf_blue")
        v.blob((x + 0.15, FRONT_Y - 0.65, 0.62), (0.08, 0.08, 0.08), "flower_pink")
    objs.append(v.build())

    export("SM_House_Cottage", objs)


# --------------------------------------------------------------------------- blommor

def plant_ao(height):
    return lambda co: lerp(0.6, 1.0, clamp01(co.z / max(0.05, height * 0.6)))


def plant_wind(height):
    return lambda co: clamp01(co.z / max(0.05, height))


def leaves(v, height, color="grass", count=2, length=0.09):
    for i in range(count):
        a = 360 / count * i + 20
        r = math.radians(a)
        v.blob((math.cos(r) * length * 0.7, math.sin(r) * length * 0.7, height),
               (length, length * 0.35, 0.015), color, rot=(0, -25, a), smooth=False)


def sprout(name, leaf="grass"):
    h = 0.08
    v = Variant(name, plant_ao(h), plant_wind(h))
    v.cylinder((0, 0, 0), 0.012, 0.01, h, "leaf_blue", segments=4, caps=False)
    leaves(v, h, leaf, count=2, length=0.05)
    v.cylinder((0, 0, -0.01), 0.07, 0.06, 0.02, "soil", segments=6)
    return v.build()


def flower(name, height, head, stage):
    """stage 1 = knopp, stage 2 = blom."""
    v = Variant(name, plant_ao(height), plant_wind(height))
    v.cylinder((0, 0, -0.01), 0.08, 0.07, 0.02, "soil", segments=6)
    v.cylinder((0, 0, 0), 0.014, 0.011, height, "leaf_blue", segments=4, caps=False)
    leaves(v, height * 0.35, "grass", count=2, length=0.09)
    if stage == 1:
        v.blob((0, 0, height + 0.025), (0.025, 0.025, 0.04), "leaf_yellow", smooth=False)
    else:
        head(v, height)
    return v.build()


def rose_head(v, h):
    v.cylinder((0, 0, h - 0.01), 0.025, 0.075, 0.06, "flower_pink", segments=6, caps=False)
    v.blob((0, 0, h + 0.04), (0.045, 0.045, 0.035), "flower_red", smooth=False)


def tulip_head(v, h):
    v.cylinder((0, 0, h - 0.01), 0.022, 0.045, 0.085, "flower_yellow", segments=6, caps=True)
    v.blob((0, 0, h + 0.06), (0.018, 0.018, 0.012), "flower_orange", smooth=False)


def lavender_head(v, h):
    for i, (dx, dy, tilt) in enumerate(((0, 0, 0), (0.035, 0.02, 12), (-0.03, 0.025, -12))):
        hh = h * (1.0 - i * 0.1)
        if i > 0:  # mittaxet är huvudstjälken – ingen extra cylinder (håller budgeten < 200 tris)
            v.cylinder((dx, dy, h * 0.5), 0.006, 0.006, hh * 0.5, "leaf_blue", segments=3, caps=False, rot=(tilt, 0, 0))
        for k in range(2):
            v.blob((dx, dy + math.sin(math.radians(tilt)) * hh * 0.5, hh + k * 0.05), (0.016, 0.016, 0.03),
                   "flower_purple" if k == 0 else "flower_violet", smooth=False)


def build_flower(asset, height, head):
    objs = [
        sprout("Stage0_Sprout"),
        flower("Stage1_Growing", height * 0.75, head, stage=1),
        flower("Stage2_Bloom", height, head, stage=2),
    ]
    export(asset, objs)


# --------------------------------------------------------------------------- träd

def build_tree():
    def tree_ao(co):
        return lerp(0.65, 1.0, clamp01(co.z / 1.2)) if co.z < 1.3 else lerp(0.78, 1.0, clamp01((co.z - 1.4) / 1.4))

    def tree_wind(co):
        return 0.0 if co.z < 1.4 else 0.35 * clamp01((co.z - 1.4) / 1.6)

    v = Variant("Tree_Blossom", tree_ao, tree_wind)
    v.cylinder((0, 0, 0), 0.17, 0.11, 1.7, "trunk", segments=6, bevel=0.0)
    v.cylinder((0.05, 0, 1.2), 0.07, 0.045, 0.75, "trunk", segments=5, rot=(0, 38, 0))
    v.cylinder((-0.05, 0.02, 1.3), 0.06, 0.04, 0.6, "trunk", segments=5, rot=(-30, -25, 0))
    crowns = [((0, 0, 2.35), (0.95, 0.9, 0.75), "flower_blossom"),
              ((0.55, 0.15, 2.05), (0.6, 0.6, 0.5), "flower_pink"),
              ((-0.5, 0.3, 2.1), (0.6, 0.55, 0.5), "flower_blossom"),
              ((0.05, -0.45, 2.0), (0.55, 0.5, 0.45), "flower_pink")]
    for loc, scale, col in crowns:
        v.blob(loc, scale, col, detail=2)
    for i in range(6):
        a = math.radians(i * 60 + 15)
        v.blob((math.cos(a) * 0.9, math.sin(a) * 0.9, 0.01), (0.12, 0.09, 0.012), "flower_blossom", smooth=False)
    obj = v.build()
    export("SM_Tree_Blossom_Pink", [obj])


# --------------------------------------------------------------------------- förhandsbild

def render_preview():
    """Bygger om allt i en scen sida vid sida och renderar med Workbench (palett-textur, platt studioljus)."""
    clear_scene()
    img = bpy.data.images.load(str(PALETTE_PNG))
    mat = bpy.data.materials.new("Palette")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = 'Closest'
    bsdf = nodes.get("Principled BSDF")
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nodes.active = tex

    for f in sorted(INCOMING.glob("SM_*.fbx")):
        bpy.ops.import_scene.fbx(filepath=str(f))
    layout = {"Phase0_Decayed": (-7.5, 0), "Phase1_Repaired": (-2.5, 0), "Phase2_Painted": (2.5, 0),
              "Phase3_Decorated": (7.5, 0), "Tree_Blossom": (10.5, 4)}
    flower_row = 0
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
            continue
        o.data.materials.clear()
        o.data.materials.append(mat)
        base = o.name.split(".")[0]
        if base in layout:
            x, y = layout[base]
            o.location = (x, y, 0)
        elif base.startswith("Stage"):
            stage = int(base[5])
            o.location = (-6 + flower_row // 3 * 0.9 + stage * 0.28, -4.5, 0)
            o.scale = (2.5, 2.5, 2.5)
            flower_row += 1
    ground = bpy.data.meshes.new("Ground")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=14)
    bm.to_mesh(ground)
    g = bpy.data.objects.new("Ground", ground)
    bpy.context.scene.collection.objects.link(g)
    gmat = bpy.data.materials.new("Grass")
    gmat.diffuse_color = (0.07, 0.54, 0.03, 1)
    ground.materials.append(gmat)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 24
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    # Samma vinkel som spelet: 30° ner, 45° runt.
    cam.rotation_euler = (math.radians(60), 0, math.radians(-45))
    direction = cam.rotation_euler.to_matrix() @ Vector((0, 0, 1))
    cam.location = Vector((1.5, 0, 1.5)) + direction * 40
    scene = bpy.context.scene
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.render.filepath = str(SOURCE / "preview.png")
    bpy.ops.render.render(write_still=True)
    print(f"[NG] Förhandsbild: {SOURCE / 'preview.png'}")


def main():
    clear_scene()
    build_house()
    clear_scene()
    build_flower("SM_Flower_Rose_Pink", 0.36, rose_head)
    clear_scene()
    build_flower("SM_Flower_Tulip_Yellow", 0.32, tulip_head)
    clear_scene()
    build_flower("SM_Flower_Lavender_Purple", 0.3, lavender_head)
    clear_scene()
    build_tree()
    if "--preview" in sys.argv:
        render_preview()


main()
