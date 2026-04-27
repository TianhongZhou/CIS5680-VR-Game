import math
import os

import bpy
from mathutils import Vector


OUT_DIR = r"D:\UnityProjects\VR\CIS5680-VR-Game\Assets\Art\Refills\HealthRecoveryStation"
BLEND_PATH = os.path.join(OUT_DIR, "HealthRecoveryStation.blend")
FBX_PATH = os.path.join(OUT_DIR, "HealthRecoveryStation.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "HealthRecoveryStation_preview.png")


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

    model_col = bpy.data.collections.new("HealthRecoveryStation_Model")
    fx_col = bpy.data.collections.new("Unity_Dynamic_Emissive_FX")
    locator_col = bpy.data.collections.new("Locator_Proxy")
    for col in (model_col, fx_col, locator_col):
        scene.collection.children.link(col)

    root = bpy.data.objects.new("HealthRecoveryStation_Root_PivotGroundCenter", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.22
    root.location = (0, 0, 0)
    scene.collection.objects.link(root)

    mat_base = make_principled_mat("M_HealthStation_DarkGunmetal_Beveled", (0.012, 0.015, 0.016, 1), 0.86, 0.34)
    mat_panel = make_principled_mat("M_HealthStation_CharcoalInsetPanels", (0.023, 0.029, 0.028, 1), 0.76, 0.54)
    mat_edge = make_principled_mat("M_HealthStation_BlackEdgeCaps", (0.003, 0.004, 0.0045, 1), 0.92, 0.44)
    mat_mint = make_principled_mat(
        "M_Dynamic_MintHealing_Emissive",
        (0.36, 1.0, 0.72, 1),
        0.0,
        0.12,
        (0.30, 1.0, 0.68, 1),
        4.2,
    )
    mat_mint_white = make_principled_mat(
        "M_Dynamic_MintWhiteCore_Emissive",
        (0.78, 1.0, 0.90, 1),
        0.0,
        0.09,
        (0.70, 1.0, 0.88, 1),
        5.2,
    )
    mat_soft_green = make_principled_mat(
        "M_Dynamic_SoftGreenStatus_Emissive",
        (0.28, 0.95, 0.48, 1),
        0.0,
        0.18,
        (0.22, 0.90, 0.38, 1),
        2.2,
    )
    mat_dim_mint = make_principled_mat(
        "M_Dim_MintBioGuideMarks",
        (0.08, 0.46, 0.33, 1),
        0.0,
        0.28,
        (0.06, 0.55, 0.36, 1),
        0.85,
    )
    mat_capsule = make_principled_mat(
        "M_LifeSupportCapsule_TransparentMintGlass",
        (0.42, 1.0, 0.76, 0.24),
        0.0,
        0.04,
        (0.20, 0.75, 0.50, 1),
        0.55,
        alpha=0.24,
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

    def sphere_obj(name, loc, scale, mat, segments=32, rings=16, collection=model_col):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=loc)
        obj = active_obj()
        obj.name = name
        obj.data.name = name + "Mesh"
        obj.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.materials.append(mat)
        bpy.ops.object.shade_smooth()
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
        obj["UnityDynamicEmissive"] = "Softly pulses during idle; brightens when healing is active"
        return obj

    def strip_between(name, p0, p1, z, width, height, mat, collection=fx_col, bevel=0.003):
        x0, y0 = p0
        x1, y1 = p1
        dx = x1 - x0
        dy = y1 - y0
        length = math.hypot(dx, dy)
        angle = math.atan2(dy, dx)
        obj = cube_obj(name, ((x0 + x1) / 2, (y0 + y1) / 2, z), (length, width, height), mat, bevel=bevel, collection=collection)
        obj.rotation_euler[2] = angle
        obj["UnityDynamicEmissive"] = "Heartbeat glyph segment; flash softly during health restoration"
        return obj

    # Low dock footprint with a small transparent life-support core.
    base_pts = [
        (-0.50, -0.42), (0.50, -0.42), (0.60, -0.30), (0.60, 0.26),
        (0.44, 0.42), (-0.44, 0.42), (-0.60, 0.26), (-0.60, -0.30),
    ]
    inset_pts = [
        (-0.39, -0.30), (0.39, -0.30), (0.48, -0.20), (0.48, 0.18),
        (0.34, 0.30), (-0.34, 0.30), (-0.48, 0.18), (-0.48, -0.20),
    ]
    proxy_pts = [
        (-0.61, -0.43), (0.61, -0.43), (0.68, -0.33), (0.68, 0.31),
        (0.50, 0.47), (-0.50, 0.47), (-0.68, 0.31), (-0.68, -0.33),
    ]

    base = prism_from_points("StationBase_DarkGunmetal", base_pts, 0.0, 0.090, mat_base, model_col, bevel=0.036)
    base["UnityCollisionHint"] = "Low health recovery dock; locator or simple box/capsule footprint is enough."
    prism_from_points("StationBase_RecessedTopPlate_Charcoal", inset_pts, 0.093, 0.118, mat_panel, model_col, bevel=0.018)
    cube_obj("StationBase_FrontSoftApproachLip_BlackMetal", (0, -0.385, 0.135), (0.78, 0.050, 0.050), mat_edge, bevel=0.014)
    cube_obj("StationBase_RearServiceSpine_BlackMetal", (0, 0.355, 0.145), (0.76, 0.070, 0.075), mat_edge, bevel=0.018)

    # Transparent life-support capsule and glowing healing core.
    cyl_z("LifeSupportCapsule_BaseClamp_BlackMetal", (0, 0.025, 0.150), 0.185, 0.055, mat_edge, vertices=56, bevel=0.008)
    cyl_z("LifeSupportCapsule_Transparent_Tube", (0, 0.025, 0.350), 0.150, 0.360, mat_capsule, vertices=64, bevel=0.004, collection=model_col)
    sphere_obj("LifeSupportCapsule_Transparent_TopDome", (0, 0.025, 0.535), (0.150, 0.150, 0.055), mat_capsule, segments=40, rings=12, collection=model_col)
    sphere_obj("LifeSupportCapsule_Transparent_BottomDome", (0, 0.025, 0.165), (0.150, 0.150, 0.040), mat_capsule, segments=40, rings=12, collection=model_col)
    cyl_z("LifeSupportCapsule_TopClamp_BlackMetal", (0, 0.025, 0.550), 0.155, 0.030, mat_edge, vertices=56, bevel=0.006)
    for i, x in enumerate([-0.125, 0.125], start=1):
        rail = cube_obj(f"LifeSupportCapsule_SideRail_{i:02d}_BlackMetal", (x, 0.025, 0.350), (0.020, 0.030, 0.345), mat_edge, bevel=0.005)
        rail.rotation_euler[2] = math.radians(0)

    core = cyl_z("HealingCore_MintWhiteEmissive", (0, 0.025, 0.350), 0.065, 0.330, mat_mint_white, vertices=48, bevel=0.005, collection=fx_col)
    core["UnityDynamicEmissive"] = "Idle breathing; stronger glow while player receives health"
    sphere_obj("HealingCore_MintWhiteEmissive_PulseOrb", (0, 0.025, 0.350), (0.090, 0.090, 0.090), mat_mint, segments=32, rings=16, collection=fx_col)["UnityDynamicEmissive"] = "Short soft flare at heal tick or completion"

    # Bio pulse rings are broken technical rings, not a magical ground circle.
    for i, (start, end) in enumerate([(20, 78), (112, 168), (202, 258), (292, 340)], start=1):
        arc_strip(f"BioPulseRing_MintEmissive_OuterSegment_{i:02d}", 0.330, 0.295, start, end, 0.132, mat_mint)
    for i, (start, end) in enumerate([(45, 82), (98, 135), (225, 262), (278, 315)], start=1):
        arc_strip(f"BioPulseRing_MintEmissive_InnerSegment_{i:02d}", 0.245, 0.218, start, end, 0.136, mat_dim_mint)

    # Minimal heartbeat waveform on the front pad, no realistic red cross.
    heartbeat_points = [(-0.315, -0.245), (-0.210, -0.245), (-0.165, -0.200), (-0.105, -0.285), (-0.030, -0.225), (0.055, -0.245), (0.315, -0.245)]
    for i in range(len(heartbeat_points) - 1):
        strip_between(
            f"HeartbeatGlyph_MintEmissive_Segment_{i+1:02d}",
            heartbeat_points[i],
            heartbeat_points[i + 1],
            0.142,
            0.020,
            0.012,
            mat_mint,
            fx_col,
        )

    # Soft status ticks: green/mint, not cyan energy-pad ticks.
    for i, x in enumerate([-0.36, -0.18, 0.18, 0.36], start=1):
        tick = cube_obj(f"StatusTicks_SoftGreenEmissive_Front_{i:02d}", (x, -0.345, 0.145), (0.086, 0.018, 0.014), mat_soft_green, bevel=0.004, collection=fx_col)
        tick["UnityDynamicEmissive"] = "Sequential health refill progress tick"
    for i, y in enumerate([-0.135, 0.055, 0.245], start=1):
        tick_l = cube_obj(f"StatusTicks_SoftGreenEmissive_Left_{i:02d}", (-0.505, y, 0.135), (0.018, 0.084, 0.012), mat_soft_green, bevel=0.004, collection=fx_col)
        tick_r = cube_obj(f"StatusTicks_SoftGreenEmissive_Right_{i:02d}", (0.505, y, 0.135), (0.018, 0.084, 0.012), mat_soft_green, bevel=0.004, collection=fx_col)
        tick_l["UnityDynamicEmissive"] = "Subtle side outline on sonar reveal"
        tick_r["UnityDynamicEmissive"] = "Subtle side outline on sonar reveal"

    # Tiny white-green service indicator on the back spine.
    for i, x in enumerate([-0.22, 0.0, 0.22], start=1):
        obj = cube_obj(f"StatusTicks_SoftGreenEmissive_RearSpine_{i:02d}", (x, 0.392, 0.190), (0.080, 0.018, 0.014), mat_mint_white, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Available/ready status indicator"

    # Locator proxy: simplified base footprint and treatment core outline.
    proxy = prism_from_points("LocatorMarkerProxy", proxy_pts, 0.0, 0.065, mat_proxy, locator_col, bevel=0.0)
    proxy.display_type = "WIRE"
    proxy.show_wire = True
    proxy.hide_render = True
    proxy["UnityLocatorPurpose"] = "Simplified health recovery station footprint; hide for final rendering."
    proxy["ApproxDimensionsMeters"] = "W1.36 D0.90 H0.58 visual, locator footprint about 1.36m by 0.90m"
    proxy_core = cyl_z("LocatorMarkerProxy_HealingCore", (0, 0.025, 0.305), 0.170, 0.500, mat_proxy, vertices=32, bevel=0.0, collection=locator_col)
    proxy_core.display_type = "WIRE"
    proxy_core.show_wire = True
    proxy_core.hide_render = True
    proxy_core["UnityLocatorPurpose"] = "Simplified transparent healing core/capsule volume."

    root["AssetName"] = "Health Recovery Station"
    root["UnityScale"] = "1 Blender unit = 1 meter"
    root["DynamicEmissiveObjects"] = "HealingCore_*, BioPulseRing_*, HeartbeatGlyph_*, StatusTicks_*"
    root["SonarResponse"] = "Temporarily boost mint/soft-green outline and healing core when sonar pulse reveals the station."

    # Preview-only lighting/camera.
    bpy.ops.object.light_add(type="AREA", location=(0, -2.1, 2.15))
    key = active_obj()
    key.name = "Preview_KeyAreaLight_NotExported"
    key.data.energy = 340
    key.data.size = 3.2
    bpy.ops.object.light_add(type="POINT", location=(0, -0.20, 0.58))
    mint_light = active_obj()
    mint_light.name = "Preview_MintHealingGlow_NotExported"
    mint_light.data.color = (0.36, 1.0, 0.68)
    mint_light.data.energy = 95
    mint_light.data.shadow_soft_size = 1.1
    bpy.ops.object.camera_add(location=(1.18, -1.60, 0.96))
    cam = active_obj()
    cam.name = "PreviewCamera"
    target = Vector((0, 0.02, 0.20))
    direction = target - Vector(cam.location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 43
    cam.data.dof.use_dof = True
    cam.data.dof.focus_object = core
    cam.data.dof.aperture_fstop = 8.0
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.004, 0.005, 0.006)

    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["CreatedFor"] = "Unity VR random maze health recovery station"

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

    dynamic_prefixes = ("HealingCore_", "BioPulseRing_", "HeartbeatGlyph_", "StatusTicks_")
    dynamic_count = len([obj for obj in bpy.data.objects if obj.name.startswith(dynamic_prefixes)])
    print("CREATED_BLEND=" + BLEND_PATH)
    print("CREATED_FBX=" + FBX_PATH)
    print("CREATED_PREVIEW=" + PREVIEW_PATH)
    print("OBJECT_COUNT=" + str(len(bpy.data.objects)))
    print("DYNAMIC_OBJECT_COUNT=" + str(dynamic_count))
    print("LOCATOR_PRESENT=" + str("LocatorMarkerProxy" in bpy.data.objects))


if __name__ == "__main__":
    main()
