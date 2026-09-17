# Build-time GLB -> FBX conversion for the native Unity model bundle.
import json
import os
import re
import struct
import sys
import bpy


NO_TEXTURE = "-"
GLB_MAGIC = 0x46546C67
GLB_JSON = 0x4E4F534A
GLB_BIN = 0x004E4942


def args_after_double_dash():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def safe_name(value):
    value = re.sub(r"[^A-Za-z0-9_.-]+", "_", value or "texture").strip("._")
    return value or "texture"


def load_glb(src):
    with open(src, "rb") as handle:
        data = handle.read()
    if len(data) < 12:
        raise RuntimeError(f"Invalid GLB header: {src}")
    magic, version, declared_length = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC or version != 2 or declared_length != len(data):
        raise RuntimeError(
            f"Unsupported GLB header: {src} magic=0x{magic:08x} version={version} "
            f"declared={declared_length} actual={len(data)}"
        )

    document = None
    binary = None
    offset = 12
    while offset + 8 <= len(data):
        length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        end = offset + length
        if end > len(data):
            raise RuntimeError(f"Truncated GLB chunk: {src}")
        chunk = data[offset:end]
        offset = end
        if chunk_type == GLB_JSON:
            document = json.loads(chunk.decode("utf-8").rstrip("\0 \t\r\n"))
        elif chunk_type == GLB_BIN:
            binary = chunk

    if document is None or binary is None:
        raise RuntimeError(f"GLB is missing JSON or BIN chunk: {src}")
    return document, binary


def texture_source(document, texture_index):
    textures = document.get("textures", [])
    if texture_index < 0 or texture_index >= len(textures):
        raise RuntimeError(f"GLB texture index out of range: {texture_index}")
    texture = textures[texture_index]
    source = texture.get("source")
    if source is None:
        source = texture.get("extensions", {}).get("KHR_texture_basisu", {}).get("source")
    if not isinstance(source, int):
        raise RuntimeError(f"GLB texture has no supported image source: {texture_index}")
    return source


def material_sources(document):
    result = {}
    for index, material in enumerate(document.get("materials", [])):
        name = material.get("name")
        if not name:
            raise RuntimeError(f"GLB material has no name: index={index}")
        if name in result:
            raise RuntimeError(f"Duplicate GLB material name: {name}")
        base_color = material.get("pbrMetallicRoughness", {}).get("baseColorTexture")
        source = None if base_color is None else texture_source(document, base_color.get("index", -1))
        result[name] = source
    if not result:
        raise RuntimeError("GLB contains no materials")
    return result


def image_extension(mime_type):
    if mime_type == "image/png":
        return ".png"
    if mime_type in {"image/jpeg", "image/jpg"}:
        return ".jpg"
    raise RuntimeError(f"Unsupported GLB image MIME type for Unity sidecar: {mime_type!r}")


def export_textures(dst, document, binary, sources):
    output_dir = os.path.dirname(dst)
    stem = os.path.splitext(os.path.basename(dst))[0]
    images = document.get("images", [])
    views = document.get("bufferViews", [])
    exported = {}

    for source in sorted({value for value in sources.values() if value is not None}):
        if source < 0 or source >= len(images):
            raise RuntimeError(f"GLB image source out of range: {source}")
        image = images[source]
        view_index = image.get("bufferView")
        if not isinstance(view_index, int) or view_index < 0 or view_index >= len(views):
            raise RuntimeError(f"GLB image is not embedded in a bufferView: source={source}")
        view = views[view_index]
        if view.get("buffer", 0) != 0:
            raise RuntimeError(f"GLB image uses unsupported buffer index: source={source} buffer={view.get('buffer')}")
        start = view.get("byteOffset", 0)
        length = view.get("byteLength")
        if not isinstance(start, int) or not isinstance(length, int) or start < 0 or length <= 0:
            raise RuntimeError(f"Invalid GLB image bufferView: source={source}")
        end = start + length
        if end > len(binary):
            raise RuntimeError(f"GLB image bufferView exceeds BIN chunk: source={source}")

        name = image.get("name") or f"image_{source}"
        filename = f"{stem}__tex_{source:02d}_{safe_name(name)}{image_extension(image.get('mimeType'))}"
        with open(os.path.join(output_dir, filename), "wb") as handle:
            handle.write(binary[start:end])
        exported[source] = filename

    if not exported:
        raise RuntimeError(f"GLB materials contain no base-color textures: {dst}")
    print(f"Darius native texture export complete: {dst} textures={len(exported)}")
    return exported


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


def export_material_map(dst, exported, sources):
    stem = os.path.splitext(os.path.basename(dst))[0]
    path = os.path.join(os.path.dirname(dst), f"{stem}__materials.tsv")
    lines = []
    textured = 0
    untextured = 0
    for slot, material in exported_material_slots():
        if any(c in material.name for c in "\t\r\n"):
            raise RuntimeError(f"Material name cannot be represented in TSV manifest: {material.name!r}")
        if material.name not in sources:
            raise RuntimeError(f"Exported Blender material is missing from GLB JSON: {material.name}")

        source = sources[material.name]
        if source is None:
            filename = NO_TEXTURE
            untextured += 1
        else:
            filename = exported.get(source)
            if filename is None:
                raise RuntimeError(f"Base-color image was not exported: material={material.name} source={source}")
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

    document, binary = load_glb(src)
    sources = material_sources(document)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src, import_pack_images=True, merge_vertices=False)
    exported = export_textures(dst, document, binary, sources)
    export_material_map(dst, exported, sources)
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
