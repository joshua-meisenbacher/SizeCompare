import bpy
from pathlib import Path


ROOT = Path("/Users/joshuameisenbacher/SizeGuide2")
REFERENCE_FBX = ROOT / "Assets" / "MaleHoodie_Skinned.fbx"
SOURCE_DIR = ROOT / "Assets" / "Generated" / "RepairedHoodies"
OUTPUT_DIRS = [
    ROOT / "Assets" / "Generated" / "RepairedHoodies",
    ROOT / "Assets" / "Resources" / "Generated" / "RepairedHoodies",
]

SIZE_NAMES = ["S", "M", "L", "XL"]


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_reference():
    bpy.ops.import_scene.fbx(filepath=str(REFERENCE_FBX), automatic_bone_orientation=True)
    ref_objects = list(bpy.context.scene.objects)
    ref_mesh = next(obj for obj in ref_objects if obj.type == "MESH")
    armature_object = None
    for modifier in ref_mesh.modifiers:
        if modifier.type == "ARMATURE" and modifier.object is not None:
            armature_object = modifier.object
            break
    if armature_object is None:
        raise RuntimeError("Reference hoodie mesh is missing an armature modifier.")
    return ref_mesh, armature_object


def import_source(size_name, existing_objects):
    source_path = SOURCE_DIR / f"Hoodie_{size_name}_Repaired.fbx"
    bpy.ops.import_scene.fbx(filepath=str(source_path), automatic_bone_orientation=True)
    source_objects = [obj for obj in bpy.context.scene.objects if obj not in existing_objects]
    source_mesh = next(obj for obj in source_objects if obj.type == "MESH")
    return source_mesh


def align_mesh_to_reference(source_mesh, ref_mesh):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = source_mesh
    source_mesh.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    source_mesh.matrix_world = ref_mesh.matrix_world.copy()


def clear_existing_skinning(source_mesh):
    for modifier in list(source_mesh.modifiers):
        source_mesh.modifiers.remove(modifier)

    for group in list(source_mesh.vertex_groups):
        source_mesh.vertex_groups.remove(group)


def seed_vertex_groups(source_mesh, ref_mesh):
    for group in ref_mesh.vertex_groups:
        source_mesh.vertex_groups.new(name=group.name)


def transfer_weights(source_mesh, ref_mesh):
    modifier = source_mesh.modifiers.new("WeightTransfer", "DATA_TRANSFER")
    modifier.object = ref_mesh
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.layers_vgroup_select_src = "ALL"
    modifier.layers_vgroup_select_dst = "NAME"
    modifier.mix_mode = "REPLACE"

    bpy.context.view_layer.objects.active = source_mesh
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def bind_to_armature(source_mesh, armature_object):
    source_mesh.parent = armature_object
    source_mesh.parent_type = "OBJECT"
    source_mesh.matrix_parent_inverse = armature_object.matrix_world.inverted()

    modifier = source_mesh.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = armature_object


def export_repaired_mesh(size_name, source_mesh, armature_object):
    source_mesh.name = f"hoodie_blue_{size_name}_repaired"
    source_mesh.data.name = f"hoodie_blue_{size_name}_repaired_mesh"

    for output_dir in OUTPUT_DIRS:
        output_dir.mkdir(parents=True, exist_ok=True)
        output_path = output_dir / f"Hoodie_{size_name}_Repaired.fbx"

        bpy.ops.object.select_all(action="DESELECT")
        source_mesh.select_set(True)
        armature_object.select_set(True)
        bpy.context.view_layer.objects.active = source_mesh

        bpy.ops.export_scene.fbx(
            filepath=str(output_path),
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            add_leaf_bones=False,
            bake_anim=False,
            mesh_smooth_type="OFF",
            use_armature_deform_only=False,
        )
        print(f"EXPORTED {output_path}")


def main():
    clear_scene()
    ref_mesh, armature_object = import_reference()

    existing_objects = list(bpy.context.scene.objects)
    for size_name in SIZE_NAMES:
        source_mesh = import_source(size_name, existing_objects)
        existing_objects = list(bpy.context.scene.objects)

        align_mesh_to_reference(source_mesh, ref_mesh)
        clear_existing_skinning(source_mesh)
        seed_vertex_groups(source_mesh, ref_mesh)
        transfer_weights(source_mesh, ref_mesh)
        bind_to_armature(source_mesh, armature_object)
        export_repaired_mesh(size_name, source_mesh, armature_object)


if __name__ == "__main__":
    main()
