import bpy
from mathutils import Vector
from pathlib import Path


ROOT = Path("/Users/joshuameisenbacher/SizeGuide2")
SOURCE_FBX = ROOT / "Assets" / "Generated" / "RepairedHoodies" / "Hoodie_M_Repaired3.fbx"
OUTPUT_DIRS = [
    ROOT / "Assets" / "Generated" / "RepairedHoodies",
    ROOT / "Assets" / "Resources" / "Generated" / "RepairedHoodies",
]

# Study control chart values in inches.
CHART = {
    "S": {"width": 18.0, "length": 28.0, "chest": 36.0},
    "M": {"width": 20.0, "length": 29.0, "chest": 40.0},
    "L": {"width": 22.0, "length": 30.0, "chest": 44.0},
    "XL": {"width": 24.0, "length": 31.0, "chest": 48.0},
}
BASE = CHART["M"]


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_medium():
    bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX), automatic_bone_orientation=True)
    objs = list(bpy.context.scene.objects)
    mesh = next(obj for obj in objs if obj.type == "MESH")
    return objs, mesh


def chart_profile(size_name):
    row = CHART[size_name]
    width_ratio = row["width"] / BASE["width"]
    length_ratio = row["length"] / BASE["length"]
    chest_ratio = row["chest"] / BASE["chest"]

    return {
        "width_ratio": width_ratio,
        "length_ratio": length_ratio,
        "depth_ratio": chest_ratio,
        "shoulder_ratio": 1.0 + (width_ratio - 1.0) * 0.88,
        "sleeve_width_ratio": 1.0 + (width_ratio - 1.0) * 0.82,
        "sleeve_length_ratio": 1.0 + (length_ratio - 1.0) * 0.72,
        "cuff_ratio": 1.0 + (width_ratio - 1.0) * 0.42,
        "hem_ratio": 1.0 + (width_ratio - 1.0) * 0.94,
    }


def deform_mesh(mesh_obj, size_name):
    profile = chart_profile(size_name)
    mesh = mesh_obj.data

    coords = [v.co.copy() for v in mesh.vertices]
    min_v = Vector((min(v.x for v in coords), min(v.y for v in coords), min(v.z for v in coords)))
    max_v = Vector((max(v.x for v in coords), max(v.y for v in coords), max(v.z for v in coords)))
    center = (min_v + max_v) * 0.5
    height = max(0.0001, max_v.y - min_v.y)

    # Keep the hem stable and scale vertical distances from the hem upward.
    hem_y = min_v.y

    for vertex in mesh.vertices:
        x = vertex.co.x
        y = vertex.co.y
        z = vertex.co.z

        y_norm = (y - hem_y) / height

        # Distinguish torso vs sleeve regions from horizontal spread.
        x_abs = abs(x)
        shoulder_start = 0.35 * max_v.x
        shoulder_end = 0.70 * max_v.x
        arm_t = 0.0
        if shoulder_end > shoulder_start:
            arm_t = max(0.0, min(1.0, (x_abs - shoulder_start) / (shoulder_end - shoulder_start)))

        torso_t = 1.0 - arm_t
        upper_torso = max(0.0, min(1.0, (y_norm - 0.55) / 0.25))
        hem_band = max(0.0, 1.0 - abs(y_norm - 0.10) / 0.12)
        cuff_band = arm_t * max(0.0, 1.0 - abs(y_norm - 0.48) / 0.10)

        width_ratio = (
            profile["width_ratio"] * torso_t
            + profile["shoulder_ratio"] * upper_torso * torso_t
            + profile["sleeve_width_ratio"] * arm_t
        )
        if hem_band > 0.0:
            width_ratio = width_ratio * (1.0 - hem_band) + profile["hem_ratio"] * hem_band
        if cuff_band > 0.0:
            width_ratio = width_ratio * (1.0 - cuff_band) + profile["cuff_ratio"] * cuff_band

        depth_ratio = profile["depth_ratio"] * torso_t + profile["sleeve_width_ratio"] * arm_t
        y_ratio = profile["length_ratio"] * torso_t + profile["sleeve_length_ratio"] * arm_t

        x_offset = x - center.x
        z_offset = z - center.z
        y_offset = y - hem_y

        vertex.co.x = center.x + x_offset * width_ratio
        vertex.co.z = center.z + z_offset * depth_ratio
        vertex.co.y = hem_y + y_offset * y_ratio

    mesh.update()


def rename_for_size(mesh_obj, size_name):
    mesh_obj.name = f"hoodie_blue_{size_name}_repaired"
    mesh_obj.data.name = f"hoodie_blue_{size_name}_repaired_mesh"


def export_current_scene(size_name):
    for output_dir in OUTPUT_DIRS:
        output_dir.mkdir(parents=True, exist_ok=True)
        output_path = output_dir / f"Hoodie_{size_name}_Repaired.fbx"
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.fbx(
            filepath=str(output_path),
            use_selection=True,
            object_types={"EMPTY", "ARMATURE", "MESH"},
            add_leaf_bones=False,
            bake_anim=False,
            mesh_smooth_type="OFF",
        )
        print(f"EXPORTED {output_path}")


def build_size(size_name):
    clear_scene()
    _, mesh_obj = import_medium()
    deform_mesh(mesh_obj, size_name)
    rename_for_size(mesh_obj, size_name)
    export_current_scene(size_name)


def main():
    for size_name in ("S", "M", "L", "XL"):
        build_size(size_name)


if __name__ == "__main__":
    main()
