import math
import os

import bpy
from mathutils import Vector


OUT_DIR = r"D:\UnityProjects\VR\CIS5680-VR-Game\Assets\Art\Refills\EnergyRecoveryPad"
BLEND_PATH = os.path.join(OUT_DIR, "EnergyRecoveryPad.blend")
FBX_PATH = os.path.join(OUT_DIR, "EnergyRecoveryPad.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "EnergyRecoveryPad_preview.png")


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


def add_bevel(obj, amount=0.025, segments=3, weighted=True):
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

    model_col = bpy.data.collections.new("EnergyRecoveryPad_Model")
    fx_col = bpy.data.collections.new("Unity_Dynamic_Emissive_FX")
    locator_col = bpy.data.collections.new("Locator_Proxy")
    for col in (model_col, fx_col, locator_col):
        scene.collection.children.link(col)

    root = bpy.data.objects.new("EnergyRecoveryPad_Root_PivotGroundCenter", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.22
    root.location = (0, 0, 0)
    scene.collection.objects.link(root)

    mat_base = make_principled_mat("M_Pad_DarkGunmetal_Beveled", (0.012, 0.015, 0.017, 1), 0.86, 0.34)
    mat_panel = make_principled_mat("M_Pad_CharcoalInsetPanels", (0.024, 0.029, 0.032, 1), 0.78, 0.54)
    mat_edge = make_principled_mat("M_Pad_BlackEdgeCaps", (0.003, 0.004, 0.005, 1), 0.92, 0.44)
    mat_cyan = make_principled_mat(
        "M_Dynamic_CyanEnergyCore_Emissive",
        (0.0, 0.82, 1.0, 1),
        0.0,
        0.10,
        (0.0, 0.86, 1.0, 1),
        5.0,
    )
    mat_bluewhite = make_principled_mat(
        "M_Dynamic_BlueWhiteStatus_Emissive",
        (0.72, 0.94, 1.0, 1),
        0.0,
        0.12,
        (0.62, 0.91, 1.0, 1),
        3.6,
    )
    mat_dim_cyan = make_principled_mat(
        "M_Dim_CyanChargeGuides",
        (0.0, 0.42, 0.55, 1),
        0.0,
        0.24,
        (0.0, 0.50, 0.68, 1),
        0.95,
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

    def cyl_z(name, loc, radius, depth, mat, vertices=48, bevel=0.0, collection=model_col):
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
        obj = active_obj()
        obj.name = name
        obj.data.name = name + "Mesh"
        obj.data.materials.append(mat)
        bpy.ops.object.shade_smooth()
        if bevel > 0:
            add_bevel(obj, bevel, 2)
        return link_to_collection(obj, collection, root)

    def cyl_x(name, loc, radius, depth, mat, vertices=32, bevel=0.0, collection=model_col):
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

    def arc_strip(name, r_outer, r_inner, start_deg, end_deg, z, mat, collection=fx_col):
        verts = []
        steps = 18
        for i in range(steps + 1):
            t = math.radians(start_deg + (end_deg - start_deg) * i / steps)
            verts.append((r_outer * math.cos(t), r_outer * math.sin(t), z))
            verts.append((r_inner * math.cos(t), r_inner * math.sin(t), z))
        faces = []
        for i in range(steps):
            faces.append([i * 2, (i + 1) * 2, (i + 1) * 2 + 1, i * 2 + 1])
        obj = make_mesh_obj(name, verts, faces, mat, collection, bevel=0.002, smooth=True)
        obj["UnityDynamicEmissive"] = "Pulse/sequence as a charge coil when player approaches or refills energy"
        return obj

    # Low rounded-octagon pad, roughly 1.15m x 0.88m and under 0.25m tall.
    base_pts = [
        (-0.50, -0.44), (0.50, -0.44), (0.58, -0.34), (0.58, 0.34),
        (0.50, 0.44), (-0.50, 0.44), (-0.58, 0.34), (-0.58, -0.34),
    ]
    inset_pts = [
        (-0.39, -0.31), (0.39, -0.31), (0.47, -0.22), (0.47, 0.22),
        (0.39, 0.31), (-0.39, 0.31), (-0.47, 0.22), (-0.47, -0.22),
    ]
    proxy_pts = [
        (-0.59, -0.45), (0.59, -0.45), (0.66, -0.36), (0.66, 0.36),
        (0.59, 0.45), (-0.59, 0.45), (-0.66, 0.36), (-0.66, -0.36),
    ]

    base = prism_from_points("PadBase_DarkGunmetal", base_pts, 0.0, 0.085, mat_base, model_col, bevel=0.035)
    base["UnityCollisionHint"] = "Low energy refill pad; box/capsule footprint or LocatorMarkerProxy is enough."
    prism_from_points("PadBase_RecessedTopPlate_Charcoal", inset_pts, 0.088, 0.112, mat_panel, model_col, bevel=0.018)
    cube_obj("PadBase_FrontDockLip_BlackMetal", (0, -0.405, 0.130), (0.78, 0.050, 0.052), mat_edge, bevel=0.014)
    cube_obj("PadBase_RearServiceLip_BlackMetal", (0, 0.405, 0.130), (0.78, 0.050, 0.052), mat_edge, bevel=0.014)

    # Central energy cell: low cyan dock core, readable as recharge rather than hazard.
    cell_base = cyl_z("EnergyCell_CyanCore_Housing_BlackMetal", (0, 0, 0.135), 0.185, 0.050, mat_edge, vertices=56, bevel=0.008)
    cell = cyl_z("EnergyCell_CyanCore", (0, 0, 0.176), 0.135, 0.060, mat_cyan, vertices=56, bevel=0.006, collection=fx_col)
    cell["UnityDynamicEmissive"] = "Idle cyan breathing; brighten when player is inside refill range"
    cyl_z("EnergyCell_CyanCore_InnerBlueWhiteDot", (0, 0, 0.214), 0.055, 0.010, mat_bluewhite, vertices=40, bevel=0.002, collection=fx_col)["UnityDynamicEmissive"] = "Small highlight pulse at refill completion"

    # Charge coils: two calm cyan segmented rings plus side capacitors.
    for i, (start, end) in enumerate([(18, 72), (108, 162), (198, 252), (288, 342)], start=1):
        arc_strip(f"ChargeCoils_CyanEmissive_OuterArc_{i:02d}", 0.355, 0.315, start, end, 0.126, mat_cyan)
    for i, (start, end) in enumerate([(35, 80), (100, 145), (215, 260), (280, 325)], start=1):
        arc_strip(f"ChargeCoils_CyanEmissive_InnerArc_{i:02d}", 0.255, 0.225, start, end, 0.129, mat_dim_cyan)
    for i, x in enumerate([-0.41, 0.41], start=1):
        cap = cyl_x(f"ChargeCoils_CyanEmissive_SideCapacitor_{i:02d}", (x, 0, 0.154), 0.036, 0.135, mat_cyan, vertices=32, bevel=0.004, collection=fx_col)
        cap["UnityDynamicEmissive"] = "Fills/brightens in sync with charge arcs"

    # Status ticks should be controllable in sequence by Unity.
    for i, x in enumerate([-0.36, -0.18, 0.18, 0.36], start=1):
        tick = cube_obj(f"StatusTicks_BlueWhiteEmissive_Front_{i:02d}", (x, -0.345, 0.143), (0.092, 0.018, 0.014), mat_bluewhite, bevel=0.004, collection=fx_col)
        tick["UnityDynamicEmissive"] = "Sequential fill tick while restoring energy"
    for i, x in enumerate([-0.36, -0.18, 0.18, 0.36], start=1):
        tick = cube_obj(f"StatusTicks_BlueWhiteEmissive_Rear_{i:02d}", (x, 0.345, 0.143), (0.092, 0.018, 0.014), mat_bluewhite, bevel=0.004, collection=fx_col)
        tick["UnityDynamicEmissive"] = "Low idle status; optional mirror of front charge ticks"

    # A non-magical refill glyph: battery outline plus three short sonar/energy bars.
    glyph_objs = []
    glyph_objs.append(cube_obj("TopGlyph_EnergySymbol_BatteryOutline_Top", (0.0, 0.215, 0.141), (0.275, 0.018, 0.012), mat_bluewhite, bevel=0.003, collection=fx_col))
    glyph_objs.append(cube_obj("TopGlyph_EnergySymbol_BatteryOutline_Bottom", (0.0, 0.105, 0.141), (0.275, 0.018, 0.012), mat_bluewhite, bevel=0.003, collection=fx_col))
    glyph_objs.append(cube_obj("TopGlyph_EnergySymbol_BatteryOutline_Left", (-0.138, 0.160, 0.141), (0.018, 0.128, 0.012), mat_bluewhite, bevel=0.003, collection=fx_col))
    glyph_objs.append(cube_obj("TopGlyph_EnergySymbol_BatteryOutline_Right", (0.138, 0.160, 0.141), (0.018, 0.128, 0.012), mat_bluewhite, bevel=0.003, collection=fx_col))
    glyph_objs.append(cube_obj("TopGlyph_EnergySymbol_BatteryNub", (0.175, 0.160, 0.141), (0.048, 0.052, 0.012), mat_bluewhite, bevel=0.003, collection=fx_col))
    for i, y in enumerate([-0.205, -0.160, -0.115], start=1):
        bar = cube_obj(f"TopGlyph_EnergySymbol_SonarChargeBar_{i:02d}", (0.0, y, 0.141), (0.145 + 0.060 * i, 0.016, 0.012), mat_cyan, bevel=0.003, collection=fx_col)
        glyph_objs.append(bar)
    for obj in glyph_objs:
        obj["UnityDynamicEmissive"] = "Briefly brightens on sonar reveal; can pulse gently while pad is available"

    # Subtle dim guide lines on the tread plate.
    for i, x in enumerate([-0.265, 0.265], start=1):
        guide = cube_obj(f"StatusTicks_BlueWhiteEmissive_SideGuide_{i:02d}", (x, 0, 0.137), (0.020, 0.460, 0.010), mat_dim_cyan, bevel=0.003, collection=fx_col)
        guide["UnityDynamicEmissive"] = "Optional low-intensity sonar response line"

    # Locator proxy: simplified pad footprint plus central cell marker.
    proxy = prism_from_points("LocatorMarkerProxy", proxy_pts, 0.0, 0.060, mat_proxy, locator_col, bevel=0.0)
    proxy.display_type = "WIRE"
    proxy.show_wire = True
    proxy.hide_render = True
    proxy["UnityLocatorPurpose"] = "Simplified energy recovery pad footprint; hide for final rendering."
    proxy["ApproxDimensionsMeters"] = "W1.32 D0.90 H0.23 visual, locator footprint about 1.32m by 0.90m"
    proxy_cell = cyl_z("LocatorMarkerProxy_EnergyCore", (0, 0, 0.074), 0.165, 0.028, mat_proxy, vertices=32, bevel=0.0, collection=locator_col)
    proxy_cell.display_type = "WIRE"
    proxy_cell.show_wire = True
    proxy_cell.hide_render = True
    proxy_cell["UnityLocatorPurpose"] = "Simplified center refill core marker."

    root["AssetName"] = "Energy Recovery Pad"
    root["UnityScale"] = "1 Blender unit = 1 meter"
    root["DynamicEmissiveObjects"] = "EnergyCell_CyanCore*, ChargeCoils_*, StatusTicks_*, TopGlyph_*"
    root["SonarResponse"] = "Temporarily boost cyan/blue-white emission when sonar pulse reveals the refill pad."

    # Preview-only lighting/camera.
    bpy.ops.object.light_add(type="AREA", location=(0, -2.0, 2.0))
    key = active_obj()
    key.name = "Preview_KeyAreaLight_NotExported"
    key.data.energy = 340
    key.data.size = 3.2
    bpy.ops.object.light_add(type="POINT", location=(0, -0.25, 0.62))
    cyan_light = active_obj()
    cyan_light.name = "Preview_CyanRechargeGlow_NotExported"
    cyan_light.data.color = (0.0, 0.82, 1.0)
    cyan_light.data.energy = 90
    cyan_light.data.shadow_soft_size = 1.0
    bpy.ops.object.camera_add(location=(1.18, -1.56, 0.90))
    cam = active_obj()
    cam.name = "PreviewCamera"
    target = Vector((0, 0, 0.09))
    direction = target - Vector(cam.location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 44
    cam.data.dof.use_dof = True
    cam.data.dof.focus_object = cell_base
    cam.data.dof.aperture_fstop = 8.0
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.004, 0.005, 0.007)

    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["CreatedFor"] = "Unity VR random maze energy recovery refill pad"

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

    dynamic_prefixes = ("EnergyCell_CyanCore", "ChargeCoils_", "StatusTicks_", "TopGlyph_")
    dynamic_count = len([obj for obj in bpy.data.objects if obj.name.startswith(dynamic_prefixes)])
    print("CREATED_BLEND=" + BLEND_PATH)
    print("CREATED_FBX=" + FBX_PATH)
    print("CREATED_PREVIEW=" + PREVIEW_PATH)
    print("OBJECT_COUNT=" + str(len(bpy.data.objects)))
    print("DYNAMIC_OBJECT_COUNT=" + str(dynamic_count))
    print("LOCATOR_PRESENT=" + str("LocatorMarkerProxy" in bpy.data.objects))


if __name__ == "__main__":
    main()
