import math
import os

import bpy
from mathutils import Vector


OUT_DIR = r"D:\UnityProjects\VR\CIS5680-VR-Game\Assets\Art\Traps\SciFiFloorTrap"
BLEND_PATH = os.path.join(OUT_DIR, "SciFiFloorTrap.blend")
FBX_PATH = os.path.join(OUT_DIR, "SciFiFloorTrap.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "SciFiFloorTrap_preview.png")


def active_obj():
    return bpy.context.view_layer.objects.active


def make_principled_mat(
    name,
    base=(1, 1, 1, 1),
    metallic=0.0,
    roughness=0.5,
    emission=None,
    emission_strength=0.0,
    alpha=1.0,
):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = base
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        def set_input(label, value):
            socket = bsdf.inputs.get(label)
            if socket is not None:
                socket.default_value = value

        set_input("Base Color", base)
        set_input("Metallic", metallic)
        set_input("Roughness", roughness)
        if emission is not None:
            set_input("Emission Color", emission)
            set_input("Emission Strength", emission_strength)
        set_input("Alpha", alpha)
    if alpha < 1.0:
        mat.blend_method = "BLEND"
        mat.use_screen_refraction = True
        mat.show_transparent_back = True
    return mat


def link_to_collection(obj, collection, root):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    collection.objects.link(obj)
    obj.parent = root
    return obj


def add_bevel(obj, amount=0.02, segments=3, weighted=True):
    bevel = obj.modifiers.new("BEVEL_game_readable_edges", "BEVEL")
    bevel.width = amount
    bevel.segments = segments
    bevel.affect = "EDGES"
    bevel.harden_normals = True
    if weighted:
        obj.modifiers.new("WEIGHTED_NORMAL_unity_friendly", "WEIGHTED_NORMAL")
    return obj


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.film_transparent = False

    model_col = bpy.data.collections.new("SciFiFloorTrap_Model")
    fx_col = bpy.data.collections.new("Unity_Dynamic_Emissive_FX")
    flash_col = bpy.data.collections.new("DamageFlashEmitter_HiddenPlanes")
    locator_col = bpy.data.collections.new("Locator_Proxy")
    for col in (model_col, fx_col, flash_col, locator_col):
        scene.collection.children.link(col)

    root = bpy.data.objects.new("SciFiFloorTrap_Root_PivotGroundCenter", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.22
    root.location = (0, 0, 0)
    scene.collection.objects.link(root)

    mat_base = make_principled_mat("M_Trap_BlackGunmetal_Beveled", (0.010, 0.012, 0.014, 1), 0.88, 0.34)
    mat_panel = make_principled_mat("M_Trap_DarkInsetPanels", (0.022, 0.025, 0.027, 1), 0.78, 0.54)
    mat_edge = make_principled_mat("M_Trap_BlackEdgeCaps", (0.003, 0.004, 0.005, 1), 0.92, 0.44)
    mat_red_orange = make_principled_mat(
        "M_Dynamic_RedOrangeWarning_Emissive",
        (1.0, 0.075, 0.010, 1),
        0.0,
        0.18,
        (1.0, 0.08, 0.01, 1),
        4.4,
    )
    mat_deep_red = make_principled_mat(
        "M_Dynamic_DeepRedEnergySlit_Emissive",
        (0.92, 0.025, 0.005, 1),
        0.0,
        0.14,
        (1.0, 0.035, 0.006, 1),
        5.8,
    )
    mat_amber = make_principled_mat(
        "M_WarningMarks_DarkAmber",
        (0.95, 0.38, 0.06, 1),
        0.0,
        0.24,
        (1.0, 0.24, 0.035, 1),
        0.85,
    )
    mat_flash = make_principled_mat(
        "M_DamageFlashEmitter_TransparentRedOrange",
        (1.0, 0.20, 0.02, 0.14),
        0.0,
        0.08,
        (1.0, 0.16, 0.02, 1),
        2.0,
        alpha=0.14,
    )
    mat_proxy = make_principled_mat(
        "M_LocatorProxy_TransparentYellow_NotForRender",
        (1.0, 0.86, 0.05, 0.22),
        0.0,
        0.5,
        (1.0, 0.68, 0.0, 1),
        0.25,
        alpha=0.22,
    )

    def cube_obj(name, loc, dims, mat, bevel=0.0, collection=model_col):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
        obj = active_obj()
        obj.name = name
        obj.data.name = name + "Mesh"
        obj.dimensions = dims
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.materials.append(mat)
        if bevel > 0:
            add_bevel(obj, bevel, 3)
        return link_to_collection(obj, collection, root)

    def make_mesh_obj(name, verts, faces, mat, collection=model_col, bevel=0.0, smooth=False):
        mesh = bpy.data.meshes.new(name + "Mesh")
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        mesh.materials.append(mat)
        collection.objects.link(obj)
        obj.parent = root
        if smooth:
            for poly in mesh.polygons:
                poly.use_smooth = True
        if bevel > 0:
            add_bevel(obj, bevel, 3)
        return obj

    def prism_from_points(name, points, zmin, zmax, mat, collection=model_col, bevel=0.0):
        verts = [(x, y, zmin) for x, y in points] + [(x, y, zmax) for x, y in points]
        n = len(points)
        faces = [list(range(n - 1, -1, -1)), list(range(n, 2 * n))]
        for i in range(n):
            faces.append([i, (i + 1) % n, n + (i + 1) % n, n + i])
        return make_mesh_obj(name, verts, faces, mat, collection, bevel=bevel)

    def strip_between(name, p0, p1, z, width, height, mat, collection, bevel=0.004):
        x0, y0 = p0
        x1, y1 = p1
        dx = x1 - x0
        dy = y1 - y0
        length = math.hypot(dx, dy)
        angle = math.atan2(dy, dx)
        obj = cube_obj(name, ((x0 + x1) / 2, (y0 + y1) / 2, z), (length, width, height), mat, bevel=bevel, collection=collection)
        obj.rotation_euler[2] = angle
        obj["UnityDynamicEmissive"] = "Pulse brighter when sonar reveals the trap or when trap is armed"
        return obj

    # 1.10m octagonal low floor module, embedded feel, pivot at ground center.
    oct_pts = [
        (-0.55, -0.32), (-0.32, -0.55), (0.32, -0.55), (0.55, -0.32),
        (0.55, 0.32), (0.32, 0.55), (-0.32, 0.55), (-0.55, 0.32),
    ]
    inset_pts = [
        (-0.43, -0.25), (-0.25, -0.43), (0.25, -0.43), (0.43, -0.25),
        (0.43, 0.25), (0.25, 0.43), (-0.25, 0.43), (-0.43, 0.25),
    ]
    proxy_pts = [
        (-0.56, -0.33), (-0.33, -0.56), (0.33, -0.56), (0.56, -0.33),
        (0.56, 0.33), (0.33, 0.56), (-0.33, 0.56), (-0.56, 0.33),
    ]

    base = prism_from_points("TrapBase_BlackGunmetal", oct_pts, 0.0, 0.080, mat_base, model_col, bevel=0.035)
    base["UnityCollisionHint"] = "Low octagonal hazard pad; approximate collider can use box/cylinder or LocatorMarkerProxy."
    prism_from_points("TrapBase_DarkInsetTopPlate", inset_pts, 0.083, 0.104, mat_panel, model_col, bevel=0.016)
    cube_obj("TrapBase_CentralBlackCutSlotInset", (0, 0, 0.111), (0.82, 0.115, 0.020), mat_edge, bevel=0.010)

    # Independent red-orange perimeter frame sections, deliberately sparse and script-controllable.
    for i in range(len(oct_pts)):
        p0 = oct_pts[i]
        p1 = oct_pts[(i + 1) % len(oct_pts)]
        strip_between(f"OuterWarningFrame_RedOrangeEmissive_{i+1:02d}", p0, p1, 0.116, 0.030, 0.018, mat_red_orange, fx_col)

    # Central hazard energy slits: cutting-plane language, no spikes/gore.
    for i, y in enumerate([-0.050, 0.0, 0.050], start=1):
        slit = cube_obj(f"CentralEnergySlits_RedOrangeEmissive_{i:02d}", (0, y, 0.126), (0.72, 0.018, 0.016), mat_deep_red, bevel=0.004, collection=fx_col)
        slit["UnityDynamicEmissive"] = "Boost briefly during damage tick; sonar ping can add a warning flare"
    for i, x in enumerate([-0.31, 0.31], start=1):
        cap = cube_obj(f"CentralEnergySlits_EndCapGlow_{i:02d}", (x, 0, 0.128), (0.035, 0.145, 0.018), mat_red_orange, bevel=0.004, collection=fx_col)
        cap["UnityDynamicEmissive"] = "Pairs with central slits for short red-orange damage flash"

    # Minimal warning marks: triangular caution and two diagonal slash bars.
    tri = make_mesh_obj(
        "WarningMarks_DarkInsetOrAmber_Triangle",
        [(-0.095, 0.235, 0.130), (0.095, 0.235, 0.130), (0.0, 0.360, 0.130)],
        [[0, 1, 2]],
        mat_amber,
        fx_col,
        bevel=0.002,
    )
    tri["UnityDynamicEmissive"] = "Optional low amber state indicator; keep dim to avoid coin color confusion"
    for i, (x, y, rot) in enumerate([(-0.255, -0.255, 42), (0.255, -0.255, -42)], start=1):
        mark = cube_obj(f"WarningMarks_DarkInsetOrAmber_DiagonalSlash_{i:02d}", (x, y, 0.129), (0.210, 0.026, 0.014), mat_amber, bevel=0.004, collection=fx_col)
        mark.rotation_euler[2] = math.radians(rot)
        mark["UnityDynamicEmissive"] = "Optional low amber caution accent"

    # Hidden/default-transparent flash planes for Unity short-lived trigger effects.
    flash_specs = [
        ("DamageFlashEmitter_HiddenPlane_CentralCutWide", 0.0, 0.0, 0.154, 0.90, 0.15, 0),
        ("DamageFlashEmitter_HiddenPlane_DiagonalCutA", 0.0, 0.0, 0.158, 0.88, 0.11, 36),
        ("DamageFlashEmitter_HiddenPlane_DiagonalCutB", 0.0, 0.0, 0.162, 0.88, 0.11, -36),
    ]
    for name, x, y, z, sx, sy, rot in flash_specs:
        plane = cube_obj(name, (x, y, z), (sx, sy, 0.006), mat_flash, bevel=0.003, collection=flash_col)
        plane.rotation_euler[2] = math.radians(rot)
        plane.hide_render = True
        plane["DefaultUnityState"] = "Disabled or alpha 0; enable briefly on trap damage/activation"
        plane["UnityDynamicEmissive"] = "Short red-orange flash plane for energy slice feedback"

    # Locator proxy: low octagon with simplified cut-slot silhouette.
    proxy = prism_from_points("LocatorMarkerProxy", proxy_pts, 0.0, 0.055, mat_proxy, locator_col, bevel=0.0)
    proxy.display_type = "WIRE"
    proxy.show_wire = True
    proxy.hide_render = True
    proxy["UnityLocatorPurpose"] = "Simplified octagonal floor trap footprint plus central slot guidance; hide for final rendering."
    proxy["ApproxDimensionsMeters"] = "W1.12 D1.12 H0.16 visual, locator footprint about 1.12m"
    strip = cube_obj("LocatorMarkerProxy_CentralCutSlot", (0, 0, 0.070), (0.78, 0.12, 0.018), mat_proxy, bevel=0.0, collection=locator_col)
    strip.display_type = "WIRE"
    strip.show_wire = True
    strip.hide_render = True
    strip["UnityLocatorPurpose"] = "Simplified central cutting slot marker for trap orientation."

    root["AssetName"] = "Sci-Fi Floor Trap - Red Orange Cutting Plate"
    root["UnityScale"] = "1 Blender unit = 1 meter"
    root["DynamicEmissiveObjects"] = "OuterWarningFrame_*, CentralEnergySlits_*, DamageFlashEmitter_*"
    root["SonarResponse"] = "Temporarily boost red-orange frame and slit emission when sonar pulse reveals trap."

    # Preview-only lighting/camera.
    bpy.ops.object.light_add(type="AREA", location=(0, -2.1, 2.0))
    key = active_obj()
    key.name = "Preview_KeyAreaLight_NotExported"
    key.data.energy = 330
    key.data.size = 3.0
    bpy.ops.object.light_add(type="POINT", location=(0, -0.35, 0.50))
    red_light = active_obj()
    red_light.name = "Preview_RedOrangeTrapGlow_NotExported"
    red_light.data.color = (1.0, 0.18, 0.03)
    red_light.data.energy = 80
    red_light.data.shadow_soft_size = 1.0
    bpy.ops.object.camera_add(location=(1.18, -1.52, 0.98))
    cam = active_obj()
    cam.name = "PreviewCamera"
    target = Vector((0, 0, 0.07))
    direction = target - Vector(cam.location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 44
    cam.data.dof.use_dof = True
    cam.data.dof.focus_object = base
    cam.data.dof.aperture_fstop = 8.0
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.004, 0.0045, 0.006)

    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["CreatedFor"] = "Unity VR random maze sci-fi floor trap"

    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
        scene.eevee.taa_render_samples = 64
    except Exception:
        pass
    try:
        scene.view_settings.view_transform = "Filmic"
        scene.view_settings.look = "Medium High Contrast"
        scene.view_settings.exposure = 0
        scene.view_settings.gamma = 1
    except Exception:
        pass

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )

    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    fx_count = len([obj for obj in bpy.data.objects if obj.name.startswith(("OuterWarningFrame_", "CentralEnergySlits_", "DamageFlashEmitter_", "WarningMarks_"))])
    print("CREATED_BLEND=" + BLEND_PATH)
    print("CREATED_FBX=" + FBX_PATH)
    print("CREATED_PREVIEW=" + PREVIEW_PATH)
    print("OBJECT_COUNT=" + str(len(bpy.data.objects)))
    print("DYNAMIC_OBJECT_COUNT=" + str(fx_count))
    print("LOCATOR_PRESENT=" + str("LocatorMarkerProxy" in bpy.data.objects))


if __name__ == "__main__":
    main()
