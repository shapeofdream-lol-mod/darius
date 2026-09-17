# Build-time GLB -> FBX conversion for the native Unity model bundle.
import os
import re
import sys
import bpy


NO_TEXTURE = "-"


def args_after_double_dash():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def safe_name(value):
    value = re.sub(r"[^A-Za-z0-9_.-]+", "_", value or "texture").strip("._")
    return value or "texture"


def export_textures(dst):
    output_dir = os.path.dirname(dst)
    stem = os.path.splitext(os.path.basename(dst))[0]
    exported = {}
    for index, image in enumerate(bpy.data.images):
        if image is None or image.name in {"Render Result", "Viewer Node"} or image.size[0] <= 0 or image.size[1] <= 0:
            continue
        filename = f"{stem}__tex_{index:02d}_{safe_name(image.name)}.png"
        image.filepath_raw = os.path.join(output_dir, filename)
        image.file_format = "PNG"
        image.save()
        exported[image.name] = filename
    if not exported:
        raise RuntimeError(f"GLB contains no exportable textures: {dst}")
    print(f"Darius native texture export complete: {dst} textures={len(exported)}")
    return exported


def linked_image(socket, visited=None):
    if socket is None or not getattr(socket, "is_linked", False):
        return None
    visited = visited or set()
    for link in socket.links:
        node = link.from_node
        if node is None:
            continue
        key = node.as_pointer()
        if key in visited:
            continue
        visited.add(key)
        if node.type == "TEX_IMAGE" and node.image is not None:
            return node.image
        for child in node.inputs:
            image = linked_image(child, visited)
            if image is not None:
                return image
    return None


def base_color_image(material):
    if material is None or material.node_tree is None:
        return None
    for node in material.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        socket = node.inputs.get("Base Color")
        image = linked_image(socket)
        if image is not None:
            return image
    return None


def exported_material_slots():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected exactly one exported mesh object, found {len(meshes)}")

    mesh = meshes[0].data
    used = sorted({polygon.material_index for polygon in mesh.polygons})
    if not used:
        raise RuntimeError("Exported mesh contains no material slots in use")
    if used != list(range(len(used))):
        raise RuntimeError(f"Exported mesh material slots are not contiguous: {used}")
    if len(mesh.materials) < len(used):
        raise RuntimeError(
            f"Exported mesh material slot count mismatch used={len(used)} slots={len(mesh.materials)}"
        )

    result = []
    for slot in used:
        material = mesh.materials[slot]
        if material is None:
            raise RuntimeError(f"Exported mesh material slot is empty: {slot}")
        result.append((slot, material))
    return result


def export_material_map(dst, exported):
    stem = os.path.splitext(os.path.basename(dst))[0]
    path = os.path.join(os.path.dirname(dst), f"{stem}__materials.tsv")
    lines = []
    textured = 0
    untextured = 0
    for slot, material in exported_material_slots():
        if any(c in material.name for c in "\t\r\n"):
            raise RuntimeError(f"Material name cannot be represented in TSV manifest: {material.name!r}")

        image = base_color_image(material)
        if image is None:
            filename = NO_TEXTURE
            untextured += 1
        else:
            filename = exported.get(image.name)
            if filename is None:
                raise RuntimeError(f"Base-color image was not exported: material={material.name} image={image.name}")
            textured += 1
        lines.append(f"{slot}\t{material.name}\t{filename}")

    if not lines:
        raise RuntimeError(f"GLB contains no material bindings: {dst}")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(
        f"Darius native material map complete: {path} materials={len(lines)} "
        f"textured={textured} untextured={untextured}"
    )


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
    exported = export_textures(dst)
    export_material_map(dst, exported)
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
        path_mode="RELATIVE",
        embed_textures=False,
    )
    print(f"Darius native model conversion complete: {src} -> {dst}")


if __name__ == "__main__":
    main()
