import math
import os

import bpy
from mathutils import Vector


OUT_DIR = r"D:\UnityProjects\VR\CIS5680-VR-Game\Assets\Art\Entrances\SonarCalibrationGate"
BLEND_PATH = os.path.join(OUT_DIR, "SonarCalibrationGate.blend")
FBX_PATH = os.path.join(OUT_DIR, "SonarCalibrationGate.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "SonarCalibrationGate_preview.png")


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
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.film_transparent = False

    model_col = bpy.data.collections.new("SonarCalibrationGate_Model")
    fx_col = bpy.data.collections.new("Unity_Dynamic_Emissive_FX")
    locator_col = bpy.data.collections.new("Locator_Proxy")
    for col in (model_col, fx_col, locator_col):
        scene.collection.children.link(col)

    root = bpy.data.objects.new("SonarCalibrationGate_Root_PivotGroundCenter", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.35
    root.location = (0, 0, 0)
    scene.collection.objects.link(root)

    mat_frame = make_principled_mat("M_Gate_DarkGunmetal_Beveled", (0.015, 0.018, 0.020, 1), 0.85, 0.31)
    mat_edge = make_principled_mat("M_Gate_BlackMetal_EdgeCaps", (0.004, 0.005, 0.006, 1), 0.9, 0.42)
    mat_panel = make_principled_mat("M_Gate_CharcoalInsetPanels", (0.025, 0.030, 0.034, 1), 0.75, 0.55)
    mat_cyan_flow = make_principled_mat(
        "M_Dynamic_CyanFlow_Emissive",
        (0.0, 0.85, 1.0, 1),
        0.0,
        0.18,
        (0.0, 0.95, 1.0, 1),
        3.8,
    )
    mat_cyan_core = make_principled_mat(
        "M_Dynamic_ScannerCore_Cyan_Emissive",
        (0.03, 0.75, 1.0, 1),
        0.0,
        0.08,
        (0.0, 0.75, 1.0, 1),
        6.0,
    )
    mat_dim_cyan = make_principled_mat(
        "M_Dim_CyanCalibrationMarks",
        (0.0, 0.45, 0.62, 1),
        0.0,
        0.25,
        (0.0, 0.52, 0.7, 1),
        1.1,
    )
    mat_amber = make_principled_mat(
        "M_Dim_WarmAmber_ServiceLights",
        (1.0, 0.68, 0.18, 1),
        0.0,
        0.22,
        (1.0, 0.48, 0.08, 1),
        1.35,
    )
    mat_proxy = make_principled_mat(
        "M_LocatorProxy_TransparentYellow_NotForRender",
        (1.0, 0.86, 0.05, 0.22),
        0.0,
        0.5,
        (1.0, 0.72, 0.0, 1),
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

    def sphere_obj(name, loc, radius, mat, segments=32, rings=16, collection=model_col):
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=segments,
            ring_count=rings,
            radius=radius,
            location=loc,
        )
        obj = active_obj()
        obj.name = name
        obj.data.name = name + "Mesh"
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

    outer_w = 2.20
    outer_r = outer_w / 2.0
    frame_thick = 0.34
    inner_r = outer_r - frame_thick
    arch_center_z = 1.28
    depth = 0.45
    yf = -depth / 2.0
    yb = depth / 2.0

    seg = 56
    verts = []
    for i in range(seg + 1):
        theta = math.pi - (math.pi * i / seg)
        for radius in (outer_r, inner_r):
            verts.append((radius * math.cos(theta), yf, arch_center_z + radius * math.sin(theta)))
            verts.append((radius * math.cos(theta), yb, arch_center_z + radius * math.sin(theta)))

    def idx(i, ri, side):
        return i * 4 + ri * 2 + side

    faces = []
    for i in range(seg):
        faces.append([idx(i, 0, 0), idx(i + 1, 0, 0), idx(i + 1, 1, 0), idx(i, 1, 0)])
        faces.append([idx(i, 0, 1), idx(i, 1, 1), idx(i + 1, 1, 1), idx(i + 1, 0, 1)])
        faces.append([idx(i, 0, 0), idx(i, 0, 1), idx(i + 1, 0, 1), idx(i + 1, 0, 0)])
        faces.append([idx(i, 1, 0), idx(i + 1, 1, 0), idx(i + 1, 1, 1), idx(i, 1, 1)])
    faces.append([idx(0, 0, 0), idx(0, 1, 0), idx(0, 1, 1), idx(0, 0, 1)])
    faces.append([idx(seg, 0, 0), idx(seg, 0, 1), idx(seg, 1, 1), idx(seg, 1, 0)])
    make_mesh_obj("GateFrame_TopSemiArch_DarkGunmetal", verts, faces, mat_frame, model_col, bevel=0.045, smooth=True)

    cube_obj("GateFrame_LeftPost_DarkGunmetal", (-(outer_r - frame_thick / 2), 0, arch_center_z / 2), (frame_thick, depth, arch_center_z), mat_frame, bevel=0.055)
    cube_obj("GateFrame_RightPost_DarkGunmetal", ((outer_r - frame_thick / 2), 0, arch_center_z / 2), (frame_thick, depth, arch_center_z), mat_frame, bevel=0.055)
    cube_obj("StartThreshold_Platform_Beveled", (0, 0.0, 0.055), (2.34, 0.95, 0.11), mat_frame, bevel=0.055)
    cube_obj("StartThreshold_MazeDirection_LowGuideLip", (0, 0.385, 0.135), (2.02, 0.085, 0.09), mat_edge, bevel=0.025)
    cube_obj("StartThreshold_ApproachSide_LowGuideLip", (0, -0.405, 0.125), (1.72, 0.065, 0.07), mat_panel, bevel=0.022)

    cube_obj("FramePanel_LeftFront_CharcoalInset", (-(outer_r - frame_thick / 2), yf - 0.016, 0.70), (0.22, 0.026, 0.90), mat_panel, bevel=0.018)
    cube_obj("FramePanel_RightFront_CharcoalInset", ((outer_r - frame_thick / 2), yf - 0.016, 0.70), (0.22, 0.026, 0.90), mat_panel, bevel=0.018)
    cube_obj("FramePanel_TopFront_ServicePlate", (0, yf - 0.018, 2.235), (0.62, 0.030, 0.135), mat_panel, bevel=0.020)

    for i, x in enumerate([-0.78, -0.39, 0.0, 0.39, 0.78], start=1):
        obj = cube_obj(f"FX_CalibrationFloorLine_{i:02d}_CyanDim", (x, 0.035, 0.118), (0.026, 0.64, 0.012), mat_dim_cyan, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Optional: pulse with sonar reveal intensity"
    for i, y in enumerate([-0.235, 0.075, 0.315], start=1):
        obj = cube_obj(f"FX_CalibrationCrossLine_{i:02d}_CyanDim", (0, y, 0.121), (1.55, 0.020, 0.012), mat_dim_cyan, bevel=0.004, collection=fx_col)
        obj["UnityDynamicEmissive"] = "Optional: pulse with sonar reveal intensity"

    def floor_arrow(name, x0, y0, z0, length=0.28, width=0.18):
        half = width / 2
        tail = y0 - length / 2
        head = y0 + length / 2
        neck = head - length * 0.34
        shaft_half = half * 0.43
        pts = [(-shaft_half, tail), (shaft_half, tail), (shaft_half, neck), (half, neck), (0, head), (-half, neck), (-shaft_half, neck)]
        verts = [(x0 + x, y, z0) for x, y in pts]
        obj = make_mesh_obj(name, verts, [[0, 1, 2, 3, 4, 5, 6]], mat_cyan_flow, fx_col, bevel=0.002)
        obj["UnityDynamicEmissive"] = "Animate material offset/intensity toward +Y maze direction"
        return obj

    def side_arrow(name, x0, z0, face_side, y0=-0.025, length=0.30, height=0.13):
        tail = y0 - length / 2
        head = y0 + length / 2
        neck = head - length * 0.34
        half = height / 2
        shaft_half = half * 0.42
        pts = [(tail, -shaft_half), (neck, -shaft_half), (neck, -half), (head, 0), (neck, half), (neck, shaft_half), (tail, shaft_half)]
        eps = 0.006 if face_side == "left" else -0.006
        verts = [(x0 + eps, y, z0 + z) for y, z in pts]
        obj = make_mesh_obj(name, verts, [[0, 1, 2, 3, 4, 5, 6]], mat_cyan_flow, fx_col, bevel=0.0015)
        obj["UnityDynamicEmissive"] = "Animate material offset/intensity toward +Y maze direction"
        return obj

    for i, y in enumerate([-0.255, 0.045, 0.305], start=1):
        floor_arrow(f"FX_FlowArrow_Floor_{i:02d}_TowardMaze", 0.0, y, 0.128)
    for i, z in enumerate([0.42, 0.77, 1.12], start=1):
        side_arrow(f"FX_FlowArrow_LeftPost_{i:02d}_TowardMaze", -inner_r, z, "left")
        side_arrow(f"FX_FlowArrow_RightPost_{i:02d}_TowardMaze", inner_r, z, "right")

    def arc_strip(name, theta0, theta1, r0, r1, y):
        verts = []
        for theta in (theta0, theta1):
            for radius in (r0, r1):
                verts.append((radius * math.cos(theta), y, arch_center_z + radius * math.sin(theta)))
        obj = make_mesh_obj(name, verts, [[0, 2, 3, 1]], mat_cyan_flow, fx_col, bevel=0.0015)
        obj["UnityDynamicEmissive"] = "Short arc segment: sequence/scroll along arch into maze calibration state"
        return obj

    for n, deg in enumerate([34, 52, 70, 90, 110, 128, 146], start=1):
        arc_strip(
            f"FX_FlowStrip_InnerArch_{n:02d}_Cyan",
            math.radians(deg - 4.5),
            math.radians(deg + 4.5),
            inner_r + 0.018,
            inner_r + 0.070,
            yf - 0.018,
        )

    for n, x in enumerate([-0.56, 0.56], start=1):
        cube_obj(f"FX_ServiceAmberTick_Top_{n:02d}", (x, yf - 0.022, 2.235), (0.10, 0.018, 0.026), mat_amber, bevel=0.005, collection=fx_col)
    for n, x in enumerate([-0.82, 0.82], start=1):
        cube_obj(f"FX_ServiceAmberTick_Base_{n:02d}", (x, -0.405, 0.162), (0.14, 0.018, 0.022), mat_amber, bevel=0.004, collection=fx_col)

    scanner_z = 2.22
    lens = cyl_y("ScannerCore_Lens_CyanEmissive", (0, yf - 0.105, scanner_z), 0.104, 0.032, mat_cyan_core, vertices=48, bevel=0.004)
    cyl_y("ScannerCore_TopHousing_BlackMetal", (0, yf - 0.040, scanner_z), 0.155, 0.115, mat_edge, vertices=48, bevel=0.012)
    inner_glow = sphere_obj("ScannerCore_InnerGlow_CyanSmallSphere", (0, yf - 0.126, scanner_z), 0.050, mat_cyan_core, segments=32, rings=12, collection=fx_col)
    inner_glow["UnityDynamicEmissive"] = "Pulse on calibration start or sonar reveal"
    slit = cube_obj("ScannerCore_HorizontalScanSlit_Cyan", (0, yf - 0.132, scanner_z - 0.155), (0.42, 0.016, 0.026), mat_cyan_core, bevel=0.004, collection=fx_col)
    slit["UnityDynamicEmissive"] = "Sweep intensity left/right during calibration"
    cube_obj("ScannerCore_LeftMicroAntenna", (-0.245, yf - 0.045, scanner_z + 0.020), (0.085, 0.050, 0.030), mat_edge, bevel=0.010)
    cube_obj("ScannerCore_RightMicroAntenna", (0.245, yf - 0.045, scanner_z + 0.020), (0.085, 0.050, 0.030), mat_edge, bevel=0.010)

    bolt_locs = [
        (-0.94, yf - 0.030, 0.20),
        (-0.94, yf - 0.030, 1.08),
        (0.94, yf - 0.030, 0.20),
        (0.94, yf - 0.030, 1.08),
        (-0.36, yf - 0.032, 2.238),
        (0.36, yf - 0.032, 2.238),
    ]
    for i, loc in enumerate(bolt_locs, start=1):
        cyl_y(f"FrameBolt_Front_{i:02d}_BlackMetal", loc, 0.034, 0.020, mat_edge, vertices=24, bevel=0.003)

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
    add_box_verts(proxy_verts, proxy_faces, -1.10, -0.82, -0.225, 0.225, 0.00, 2.12)
    add_box_verts(proxy_verts, proxy_faces, 0.82, 1.10, -0.225, 0.225, 0.00, 2.12)
    add_box_verts(proxy_verts, proxy_faces, -1.10, 1.10, -0.225, 0.225, 2.10, 2.40)
    proxy = make_mesh_obj("LocatorMarkerProxy", proxy_verts, proxy_faces, mat_proxy, locator_col, bevel=0.0)
    proxy.display_type = "WIRE"
    proxy.show_wire = True
    proxy.hide_render = True
    proxy["UnityLocatorPurpose"] = "Simplified entrance gate outline; use for random maze locator/spawn alignment, not visual rendering. Pivot is root ground center."
    proxy["ApproxDimensionsMeters"] = "W2.2 H2.4 D0.45"

    root["AssetName"] = "Sonar Calibration Gate"
    root["UnityScale"] = "1 Blender unit = 1 meter"
    root["MazeDirection"] = "+Y in Blender source; FBX exported with Unity axis conversion"
    root["DynamicEmissiveObjectsPrefix"] = "FX_"

    bpy.ops.object.light_add(type="AREA", location=(0, -2.8, 3.0))
    key = active_obj()
    key.name = "Preview_KeyAreaLight_NotExported"
    key.data.energy = 420
    key.data.size = 4.0
    bpy.ops.object.light_add(type="POINT", location=(0, -0.55, 2.18))
    cyan_light = active_obj()
    cyan_light.name = "Preview_CyanCoreLight_NotExported"
    cyan_light.data.color = (0.0, 0.82, 1.0)
    cyan_light.data.energy = 105
    cyan_light.data.shadow_soft_size = 1.2
    bpy.ops.object.camera_add(location=(2.75, -3.15, 1.72))
    cam = active_obj()
    cam.name = "PreviewCamera"
    target = Vector((0, 0.0, 1.15))
    direction = target - Vector(cam.location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 42
    cam.data.dof.use_dof = True
    cam.data.dof.focus_object = lens
    cam.data.dof.aperture_fstop = 7.5
    scene.camera = cam

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.005, 0.006, 0.008)

    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj["CreatedFor"] = "Unity VR random maze sonar calibration entrance"

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
