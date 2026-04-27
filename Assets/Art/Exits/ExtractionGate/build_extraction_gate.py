import math
import os

import bpy
from mathutils import Vector


OUT_DIR = r"D:\UnityProjects\VR\CIS5680-VR-Game\Assets\Art\Exits\ExtractionGate"
BLEND_PATH = os.path.join(OUT_DIR, "ExtractionGate.blend")
FBX_PATH = os.path.join(OUT_DIR, "ExtractionGate.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "ExtractionGate_preview.png")


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


def add_bevel(obj, amount=0.035, segments=3, weighted=True):
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
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1000
    scene.render.film_transparent = False

    model_col = bpy.data.collections.new("ExtractionGate_Model")
    fx_col = bpy.data.collections.new("Unity_Dynamic_Emissive_FX")
    locator_col = bpy.data.collections.new("Locator_Proxy")
    for col in (model_col, fx_col, locator_col):
        scene.collection.children.link(col)

    root = bpy.data.objects.new("ExtractionGate_Root_PivotGroundCenter", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.36
    root.location = (0, 0, 0)
    scene.collection.objects.link(root)

    mat_frame = make_principled_mat("M_Exit_DarkGunmetal_Beveled", (0.012, 0.015, 0.018, 1), 0.88, 0.32)
    mat_edge = make_principled_mat("M_Exit_BlackMetal_EdgeCaps", (0.003, 0.004, 0.005, 1), 0.92, 0.42)
    mat_panel = make_principled_mat("M_Exit_CharcoalInsetPanels", (0.028, 0.032, 0.036, 1), 0.78, 0.54)
    mat_cold = make_principled_mat(
        "M_Dynamic_ColdWhiteBlue_Emissive",
        (0.70, 0.92, 1.0, 1),
        0.0,
        0.12,
        (0.56, 0.88, 1.0, 1),
        4.8,
    )
    mat_energy = make_principled_mat(
        "M_Dynamic_ExtractionPlane_TransparentBlueWhite",
        (0.52, 0.86, 1.0, 0.26),
        0.0,
        0.08,
        (0.44, 0.82, 1.0, 1),
        1.20,
        alpha=0.26,
    )
    mat_energy_edge = make_principled_mat(
        "M_Dynamic_ExtractionPlane_EdgeGlow",
        (0.85, 0.96, 1.0, 1),
        0.0,
        0.10,
        (0.78, 0.94, 1.0, 1),
        5.8,
    )
    mat_dim_blue = make_principled_mat(
        "M_Dim_BlueExitGuideMarks",
        (0.06, 0.25, 0.35, 1),
        0.0,
        0.22,
        (0.06, 0.36, 0.50, 1),
        1.0,
    )
    mat_status_gold = make_principled_mat(
        "M_Status_WarmGoldTinyLights",
        (1.0, 0.72, 0.24, 1),
        0.0,
        0.20,
        (1.0, 0.48, 0.09, 1),
        1.25,
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

    def cyl_y(name, loc, radius, depth, mat, vertices=40, bevel=0.0, collection=model_col):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=vertices,
            radius=radius,
            depth=depth,
            location=loc,
            rotation=(math.radians(90), 0, 0),
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

    def vertical_plane(name, x0, x1, z0, z1, y, mat, collection=fx_col, bevel=0.0):
        verts = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
        obj = make_mesh_obj(name, verts, [[0, 1, 2, 3]], mat, collection, bevel=bevel)
        return obj

    def chevron(name, y_center, z, width=0.66, depth=0.20, thickness=0.050):
        # Simple two-piece forward chevron on the base, pointing to +Y.
        left = cube_obj(name + "_LeftStroke", (-width * 0.17, y_center, z), (width * 0.48, thickness, 0.018), mat_cold, bevel=0.006, collection=fx_col)
        right = cube_obj(name + "_RightStroke", (width * 0.17, y_center, z), (width * 0.48, thickness, 0.018), mat_cold, bevel=0.006, collection=fx_col)
        left.rotation_euler[2] = math.radians(24)
        right.rotation_euler[2] = math.radians(-24)
        left["UnityDynamicEmissive"] = "Pulse or chase forward toward extraction direction (+Y)"
        right["UnityDynamicEmissive"] = "Pulse or chase forward toward extraction direction (+Y)"

    # Low hexagonal evacuation pad. Wider/deeper than the entrance threshold, but still human scale.
    base_points = [(-1.08, -0.48), (1.08, -0.48), (1.24, 0.0), (1.08, 0.48), (-1.08, 0.48), (-1.24, 0.0)]
    prism_from_points("ExitBase_LowHexPad_DarkGunmetal", base_points, 0.0, 0.12, mat_frame, model_col, bevel=0.055)
    inset_points = [(-0.84, -0.33), (0.84, -0.33), (0.98, 0.0), (0.84, 0.33), (-0.84, 0.33), (-0.98, 0.0)]
    prism_from_points("ExitBase_RecessedScanPlate_Charcoal", inset_points, 0.122, 0.152, mat_panel, model_col, bevel=0.025)
    cube_obj("ExitBase_RearHardStop_BlackMetal", (0, 0.455, 0.175), (1.58, 0.085, 0.09), mat_edge, bevel=0.025)
    cube_obj("ExitBase_FrontApproachLip_BlackMetal", (0, -0.455, 0.158), (1.72, 0.060, 0.065), mat_edge, bevel=0.018)

    # Twin vertical posts define the exit silhouette and distinguish it from the rounded entry gate.
    post_height = 2.56
    post_z = 0.14 + post_height / 2
    post_x = 1.06
    cube_obj("ExitPillar_Left_DarkGunmetal", (-post_x, 0.0, post_z), (0.30, 0.54, post_height), mat_frame, bevel=0.060)
    cube_obj("ExitPillar_Right_DarkGunmetal", (post_x, 0.0, post_z), (0.30, 0.54, post_height), mat_frame, bevel=0.060)
    cube_obj("ExitPillar_Left_InnerBlackRail", (-0.875, -0.005, 1.34), (0.055, 0.46, 2.12), mat_edge, bevel=0.022)
    cube_obj("ExitPillar_Right_InnerBlackRail", (0.875, -0.005, 1.34), (0.055, 0.46, 2.12), mat_edge, bevel=0.022)
    cube_obj("ExitTop_StatusBridge_DarkGunmetal", (0.0, 0.0, 2.68), (1.74, 0.46, 0.22), mat_frame, bevel=0.050)
    cube_obj("ExitTop_BlackCap_ServiceHousing", (0.0, -0.035, 2.79), (1.34, 0.34, 0.12), mat_edge, bevel=0.030)

    # Central high-tech extraction scan plane: vertical and calm, no swirl/core portal.
    panel = vertical_plane("ExtractionEnergyPlane_TransparentVerticalScanSheet", -0.72, 0.72, 0.35, 2.38, -0.018, mat_energy, fx_col)
    panel["UnityDynamicEmissive"] = "Drive alpha/emission up when exit is unlocked or during evacuation scan"
    for x, name in [(-0.742, "Left"), (0.742, "Right")]:
        obj = cube_obj(f"FX_ExtractionPlane_{name}EdgeGlow", (x, -0.020, 1.365), (0.026, 0.018, 2.03), mat_energy_edge, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Pulse with the central extraction scan sheet"
    for z, name in [(0.35, "Bottom"), (2.38, "Top")]:
        obj = cube_obj(f"FX_ExtractionPlane_{name}EdgeGlow", (0.0, -0.020, z), (1.48, 0.018, 0.026), mat_energy_edge, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Pulse with the central extraction scan sheet"
    scan_line = cube_obj("FX_ExtractionPlane_MovingVerticalScanLine", (0.0, -0.032, 1.36), (0.052, 0.014, 1.86), mat_cold, bevel=0.004, collection=fx_col)
    scan_line["UnityDynamicEmissive"] = "Animate X position or UV offset across the scan plane"
    for z, name in [(0.86, "LowerBand"), (1.86, "UpperBand")]:
        obj = cube_obj(f"FX_ExtractionPlane_{name}_HorizontalPulseBand", (0.0, -0.034, z), (1.12, 0.014, 0.028), mat_cold, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Sweep upward/downward during extraction completion"

    # Pillar and base guide lights. Sparse, brighter/cleaner than the entry calibration lines.
    for side, x in [("Left", -0.895), ("Right", 0.895)]:
        for i, z in enumerate([0.52, 1.03, 1.54, 2.05], start=1):
            obj = cube_obj(f"FX_ExitPillar_{side}_ColdLightSegment_{i:02d}", (x, -0.292, z), (0.060, 0.018, 0.22), mat_cold, bevel=0.006, collection=fx_col)
            obj["UnityDynamicEmissive"] = "Chase upward or pulse when the exit locator is revealed by sonar"
    for i, y in enumerate([-0.235, 0.045, 0.300], start=1):
        chevron(f"FX_BaseChevron_{i:02d}_TowardExtraction", y, 0.167)
    for i, x in enumerate([-0.55, 0.0, 0.55], start=1):
        obj = cube_obj(f"FX_BaseExitGuideLine_{i:02d}_DimBlue", (x, 0.04, 0.170), (0.024, 0.58, 0.012), mat_dim_blue, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Optional low-intensity response to sonar ping"

    # Small top emitters and tiny warm status lights, deliberately not coin-like.
    cyl_y("ExitScanner_TopEmitterHousing_BlackMetal", (0.0, -0.272, 2.67), 0.112, 0.070, mat_edge, vertices=36, bevel=0.006)
    cyl_y("ExitScanner_TopEmitterLens_ColdWhiteBlue", (0.0, -0.324, 2.67), 0.074, 0.025, mat_cold, vertices=36, bevel=0.004, collection=fx_col)["UnityDynamicEmissive"] = "Flash once when extraction activates"
    cube_obj("FX_ExitTop_StatusLight_Left_WarmGoldTiny", (-0.62, -0.244, 2.745), (0.110, 0.016, 0.026), mat_status_gold, bevel=0.004, collection=fx_col)
    cube_obj("FX_ExitTop_StatusLight_Right_WarmGoldTiny", (0.62, -0.244, 2.745), (0.110, 0.016, 0.026), mat_status_gold, bevel=0.004, collection=fx_col)
    for side, x in [("Left", -1.06), ("Right", 1.06)]:
        cube_obj(f"ExitPillar_{side}_OuterArmorPlate", (x, -0.292, 1.30), (0.195, 0.026, 1.64), mat_panel, bevel=0.018)
        for z in (0.24, 2.48):
            cyl_y(f"ExitPillar_{side}_Bolt_{z:.2f}_BlackMetal", (x, -0.321, z), 0.032, 0.018, mat_edge, vertices=24, bevel=0.003)

    # Locator proxy: simplified two-post plus top span, hidden from render.
    def add_box_verts(verts, faces, xmin, xmax, ymin, ymax, zmin, zmax):
        start = len(verts)
        verts.extend(
            [
                (xmin, ymin, zmin),
                (xmax, ymin, zmin),
                (xmax, ymax, zmin),
                (xmin, ymax, zmin),
                (xmin, ymin, zmax),
                (xmax, ymin, zmax),
                (xmax, ymax, zmax),
                (xmin, ymax, zmax),
            ]
        )
        faces.extend(
            [
                [start + 0, start + 1, start + 2, start + 3],
                [start + 4, start + 7, start + 6, start + 5],
                [start + 0, start + 4, start + 5, start + 1],
                [start + 1, start + 5, start + 6, start + 2],
                [start + 2, start + 6, start + 7, start + 3],
                [start + 3, start + 7, start + 4, start + 0],
            ]
        )

    proxy_verts, proxy_faces = [], []
    add_box_verts(proxy_verts, proxy_faces, -1.18, -0.90, -0.45, 0.45, 0.00, 2.66)
    add_box_verts(proxy_verts, proxy_faces, 0.90, 1.18, -0.45, 0.45, 0.00, 2.66)
    add_box_verts(proxy_verts, proxy_faces, -1.18, 1.18, -0.45, 0.45, 2.56, 2.82)
    proxy = make_mesh_obj("LocatorMarkerProxy", proxy_verts, proxy_faces, mat_proxy, locator_col)
    proxy.display_type = "WIRE"
    proxy.show_wire = True
    proxy.hide_render = True
    proxy["UnityLocatorPurpose"] = "Simplified extraction exit gate outline; use for random maze locator/spawn alignment, not visual rendering."
    proxy["ApproxDimensionsMeters"] = "W2.4 H2.8 D0.9"

    root["AssetName"] = "Extraction Gate"
    root["UnityScale"] = "1 Blender unit = 1 meter"
    root["ExitDirection"] = "+Y in Blender source; FBX exported with Unity axis conversion"
    root["DynamicEmissiveObjectsPrefix"] = "FX_ plus ExtractionEnergyPlane_TransparentVerticalScanSheet"

    # Preview-only camera/lights.
    bpy.ops.object.light_add(type="AREA", location=(0.0, -3.2, 3.25))
    key = active_obj()
    key.name = "Preview_KeyAreaLight_NotExported"
    key.data.energy = 470
    key.data.size = 4.4
    bpy.ops.object.light_add(type="POINT", location=(0.0, -0.55, 1.65))
    blue_light = active_obj()
    blue_light.name = "Preview_ColdEnergyLight_NotExported"
    blue_light.data.color = (0.58, 0.88, 1.0)
    blue_light.data.energy = 135
    blue_light.data.shadow_soft_size = 1.8
    bpy.ops.object.camera_add(location=(3.35, -4.05, 1.85))
    cam = active_obj()
    cam.name = "PreviewCamera"
    target = Vector((0.0, 0.0, 1.34))
    direction = target - Vector(cam.location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 34
    cam.data.dof.use_dof = True
    cam.data.dof.focus_object = panel
    cam.data.dof.aperture_fstop = 8.0
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.004, 0.005, 0.007)

    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["CreatedFor"] = "Unity VR random maze extraction exit gate"

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

    fx_count = len([obj for obj in bpy.data.objects if obj.name.startswith("FX_")])
    print("CREATED_BLEND=" + BLEND_PATH)
    print("CREATED_FBX=" + FBX_PATH)
    print("CREATED_PREVIEW=" + PREVIEW_PATH)
    print("OBJECT_COUNT=" + str(len(bpy.data.objects)))
    print("FX_OBJECT_COUNT=" + str(fx_count))
    print("LOCATOR_PRESENT=" + str("LocatorMarkerProxy" in bpy.data.objects))


if __name__ == "__main__":
    main()
