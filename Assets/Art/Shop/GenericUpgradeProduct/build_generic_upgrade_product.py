import math
import os

import bpy
from mathutils import Vector


OUT_DIR = r"D:\UnityProjects\VR\CIS5680-VR-Game\Assets\Art\Shop\GenericUpgradeProduct"
BLEND_PATH = os.path.join(OUT_DIR, "GenericUpgradeProduct.blend")
FBX_PATH = os.path.join(OUT_DIR, "GenericUpgradeProduct.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "GenericUpgradeProduct_preview.png")


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


def add_bevel(obj, amount=0.015, segments=3, weighted=True):
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

    model_col = bpy.data.collections.new("GenericUpgradeProduct_Model")
    fx_col = bpy.data.collections.new("Unity_Dynamic_Emissive_FX")
    locator_col = bpy.data.collections.new("Locator_Proxy")
    for col in (model_col, fx_col, locator_col):
        scene.collection.children.link(col)

    root = bpy.data.objects.new("GenericUpgradeProduct_Root_PivotBottomCenter", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.12
    root.location = (0, 0, 0)
    scene.collection.objects.link(root)

    mat_shell = make_principled_mat("M_Product_DarkGunmetal_Beveled", (0.012, 0.015, 0.018, 1), 0.86, 0.34)
    mat_black = make_principled_mat("M_Product_BlackMetal_EdgeCaps", (0.003, 0.004, 0.005, 1), 0.92, 0.43)
    mat_panel = make_principled_mat("M_Product_CharcoalInsetPanels", (0.026, 0.030, 0.034, 1), 0.76, 0.55)
    mat_icon = make_principled_mat("M_IconPlate_Interchangeable_Matte", (0.055, 0.063, 0.070, 1), 0.70, 0.46)
    mat_core = make_principled_mat(
        "M_Dynamic_UpgradeCore_ColorSlot_Emissive",
        (0.08, 0.82, 1.0, 1),
        0.0,
        0.10,
        (0.04, 0.82, 1.0, 1),
        4.4,
    )
    mat_core_glass = make_principled_mat(
        "M_UpgradeCore_TransparentGlass",
        (0.34, 0.88, 1.0, 0.25),
        0.0,
        0.05,
        (0.08, 0.45, 0.65, 1),
        0.45,
        alpha=0.25,
    )
    mat_tick = make_principled_mat(
        "M_Dynamic_StatusTicks_BlueWhite_Emissive",
        (0.70, 0.93, 1.0, 1),
        0.0,
        0.13,
        (0.56, 0.88, 1.0, 1),
        3.1,
    )
    mat_proxy = make_principled_mat(
        "M_LocatorProxy_TransparentYellow_NotForRender",
        (1.0, 0.88, 0.05, 0.22),
        0.0,
        0.5,
        (1.0, 0.70, 0.0, 1),
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

    def cyl_x(name, loc, radius, depth, mat, vertices=40, bevel=0.0, collection=model_col):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=vertices,
            radius=radius,
            depth=depth,
            location=loc,
            rotation=(0, math.radians(90), 0),
        )
        obj = active_obj()
        obj.name = name
        obj.data.name = name + "Mesh"
        obj.data.materials.append(mat)
        bpy.ops.object.shade_smooth()
        if bevel > 0:
            add_bevel(obj, bevel, 2)
        return link_to_collection(obj, collection, root)

    def cyl_z(name, loc, radius, depth, mat, vertices=40, bevel=0.0, collection=model_col):
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
        obj = active_obj()
        obj.name = name
        obj.data.name = name + "Mesh"
        obj.data.materials.append(mat)
        bpy.ops.object.shade_smooth()
        if bevel > 0:
            add_bevel(obj, bevel, 2)
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

    # Compact reusable shop item: about 0.56m wide, 0.34m deep, 0.23m high.
    shell_pts = [
        (-0.250, -0.160), (0.250, -0.160), (0.285, -0.120), (0.285, 0.115),
        (0.235, 0.170), (-0.235, 0.170), (-0.285, 0.115), (-0.285, -0.120),
    ]
    inset_pts = [
        (-0.205, -0.110), (0.205, -0.110), (0.225, -0.085), (0.225, 0.085),
        (0.190, 0.120), (-0.190, 0.120), (-0.225, 0.085), (-0.225, -0.085),
    ]
    proxy_pts = [
        (-0.295, -0.175), (0.295, -0.175), (0.315, -0.145), (0.315, 0.145),
        (0.260, 0.185), (-0.260, 0.185), (-0.315, 0.145), (-0.315, -0.145),
    ]

    shell = prism_from_points("ProductShell_DarkGunmetal", shell_pts, 0.025, 0.125, mat_shell, model_col, bevel=0.024)
    shell["UnityUse"] = "Generic repeated shop upgrade product shell"
    prism_from_points("ProductShell_DarkGunmetal_TopArmorInset", inset_pts, 0.128, 0.148, mat_panel, model_col, bevel=0.010)
    cube_obj("ProductShell_DarkGunmetal_RearCap", (0, 0.164, 0.110), (0.410, 0.028, 0.070), mat_black, bevel=0.010)
    cube_obj("ProductShell_DarkGunmetal_LeftSideRail", (-0.285, 0.000, 0.105), (0.026, 0.230, 0.072), mat_black, bevel=0.010)
    cube_obj("ProductShell_DarkGunmetal_RightSideRail", (0.285, 0.000, 0.105), (0.026, 0.230, 0.072), mat_black, bevel=0.010)

    # Interchangeable core slot: default cyan, intended to be recolored per upgrade type in Unity.
    core = cyl_x("UpgradeCore_EmissiveColorSlot", (0.0, 0.015, 0.184), 0.046, 0.315, mat_core, vertices=48, bevel=0.004, collection=fx_col)
    core["UnityDynamicEmissive"] = "Idle breathing; change emission color per upgrade type; brighten on hover/selection"
    core["UnityColorSlot"] = "Primary upgrade type color"
    glass = cyl_x("UpgradeCore_EmissiveColorSlot_TransparentCapsule", (0.0, 0.015, 0.184), 0.062, 0.335, mat_core_glass, vertices=48, bevel=0.003, collection=model_col)
    glass["UnityUse"] = "Transparent cover over recolorable upgrade core"
    for i, x in enumerate([-0.188, 0.188], start=1):
        cap = cyl_z(f"UpgradeCore_EndClamp_BlackMetal_{i:02d}", (x, 0.015, 0.184), 0.070, 0.030, mat_black, vertices=40, bevel=0.005)
        cap.rotation_euler[1] = math.radians(90)

    # Front icon/nameplate: flat material slot for product-specific icon decals or material swaps.
    plate = cube_obj("IconPlate_Interchangeable", (0, -0.176, 0.104), (0.270, 0.030, 0.072), mat_icon, bevel=0.008)
    plate["UnityMaterialSlot"] = "Swap icon/label material per upgrade type"
    plate["UnityUse"] = "Small front icon/nameplate, not dynamic by default"
    notch = cube_obj("IconPlate_Interchangeable_GlowUnderline", (0, -0.193, 0.061), (0.220, 0.010, 0.012), mat_tick, bevel=0.003, collection=fx_col)
    notch["UnityDynamicEmissive"] = "Optional hover underline or affordability state"
    # A neutral placeholder icon made from three short bars, easy to replace later.
    for i, (x, z) in enumerate([(-0.055, 0.116), (0.000, 0.096), (0.055, 0.116)], start=1):
        bar = cube_obj(f"IconPlate_Interchangeable_PlaceholderBar_{i:02d}", (x, -0.195, z), (0.036, 0.010, 0.012), mat_tick, bevel=0.002, collection=fx_col)
        bar["UnityDynamicEmissive"] = "Placeholder icon accent; replace/swap per product if desired"

    # Status ticks: separate objects for hover, selected, purchased, or affordable states.
    tick_positions = [(-0.185, 0.134), (-0.062, 0.134), (0.062, 0.134), (0.185, 0.134)]
    for i, (x, y) in enumerate(tick_positions, start=1):
        tick = cube_obj(f"StatusTicks_Emissive_Top_{i:02d}", (x, y, 0.154), (0.066, 0.014, 0.010), mat_tick, bevel=0.0025, collection=fx_col)
        tick["UnityDynamicEmissive"] = "Sequential hover/selection ticks; dim or off after purchase"
    for i, x in enumerate([-0.230, 0.230], start=1):
        side_tick = cube_obj(f"StatusTicks_Emissive_Side_{i:02d}", (x, -0.052, 0.154), (0.016, 0.092, 0.010), mat_tick, bevel=0.0025, collection=fx_col)
        side_tick["UnityDynamicEmissive"] = "Small side accent, useful for selected state"

    # Contact feet let it sit believably on a shop display table.
    for i, (x, y) in enumerate([(-0.205, -0.108), (0.205, -0.108), (-0.205, 0.118), (0.205, 0.118)], start=1):
        foot = cube_obj(f"DisplayContactFeet_BlackMetal_{i:02d}", (x, y, 0.012), (0.090, 0.055, 0.024), mat_black, bevel=0.008)
        foot["UnityUse"] = "Small contact foot for display table placement"

    # Optional locator proxy: simple footprint and core volume, hidden from render.
    proxy = prism_from_points("LocatorMarkerProxy", proxy_pts, 0.0, 0.085, mat_proxy, locator_col, bevel=0.0)
    proxy.display_type = "WIRE"
    proxy.show_wire = True
    proxy.hide_render = True
    proxy["UnityLocatorPurpose"] = "Optional simple shop product footprint; hide for final rendering."
    proxy["ApproxDimensionsMeters"] = "W0.63 D0.36 H0.23 visual"
    proxy_core = cyl_x("LocatorMarkerProxy_CoreSlot", (0.0, 0.015, 0.145), 0.075, 0.350, mat_proxy, vertices=24, bevel=0.0, collection=locator_col)
    proxy_core.display_type = "WIRE"
    proxy_core.show_wire = True
    proxy_core.hide_render = True
    proxy_core["UnityLocatorPurpose"] = "Optional core slot marker for product orientation and hover bounds."

    root["AssetName"] = "Generic Upgrade Product"
    root["UnityScale"] = "1 Blender unit = 1 meter"
    root["DynamicEmissiveObjects"] = "UpgradeCore_EmissiveColorSlot, StatusTicks_*, IconPlate_*Glow/Placeholder"
    root["InteractionResponse"] = "Idle core breathing; hover/selected brightens core and status ticks; purchase can short-flash then dim."

    # Preview-only lighting/camera.
    bpy.ops.object.light_add(type="AREA", location=(0, -1.4, 1.25))
    key = active_obj()
    key.name = "Preview_KeyAreaLight_NotExported"
    key.data.energy = 310
    key.data.size = 2.4
    bpy.ops.object.light_add(type="POINT", location=(0, -0.18, 0.42))
    core_light = active_obj()
    core_light.name = "Preview_CoreGlow_NotExported"
    core_light.data.color = (0.08, 0.82, 1.0)
    core_light.data.energy = 45
    core_light.data.shadow_soft_size = 0.6
    bpy.ops.object.camera_add(location=(0.72, -0.92, 0.48))
    cam = active_obj()
    cam.name = "PreviewCamera"
    target = Vector((0, 0.0, 0.105))
    direction = target - Vector(cam.location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 55
    cam.data.dof.use_dof = True
    cam.data.dof.focus_object = core
    cam.data.dof.aperture_fstop = 8.0
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.004, 0.005, 0.007)

    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["CreatedFor"] = "Unity VR random maze shop generic upgrade product"

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

    dynamic_prefixes = ("UpgradeCore_EmissiveColorSlot", "StatusTicks_", "IconPlate_Interchangeable_Glow", "IconPlate_Interchangeable_Placeholder")
    dynamic_count = len([obj for obj in bpy.data.objects if obj.name.startswith(dynamic_prefixes)])
    print("CREATED_BLEND=" + BLEND_PATH)
    print("CREATED_FBX=" + FBX_PATH)
    print("CREATED_PREVIEW=" + PREVIEW_PATH)
    print("OBJECT_COUNT=" + str(len(bpy.data.objects)))
    print("DYNAMIC_OBJECT_COUNT=" + str(dynamic_count))
    print("LOCATOR_PRESENT=" + str("LocatorMarkerProxy" in bpy.data.objects))


if __name__ == "__main__":
    main()
