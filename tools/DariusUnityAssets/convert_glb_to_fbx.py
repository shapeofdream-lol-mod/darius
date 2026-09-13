# Run with Blender in background mode, for example:
# blender --background --python tools/DariusUnityAssets/convert_glb_to_fbx.py -- input.glb output.fbx
#
# This is an editor/build-time tool only. The shipped mod does not depend on Blender or a glTF
# runtime importer; Unity receives a normal FBX and packages it into an AssetBundle.

import os
import sys
import bpy


def args_after_double_dash():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def main():
    args = args_after_double_dash()
    if len(args) != 2:
        raise SystemExit("usage: blender --background --python convert_glb_to_fbx.py -- <input.glb> <output.fbx>")

    src = os.path.abspath(args[0])
    dst = os.path.abspath(args[1])
    if not os.path.isfile(src):
        raise FileNotFoundError(src)
    os.makedirs(os.path.dirname(dst), exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src, import_pack_images=True, merge_vertices=False)

    # Preserve all imported actions. Unity will decide which clips loop when creating the bundle.
    for action in bpy.data.actions:
        action.use_fake_user = True

    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=False,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        path_mode="COPY",
        embed_textures=True,
    )
    print(f"Darius native model conversion complete: {src} -> {dst}")


if __name__ == "__main__":
    main()
