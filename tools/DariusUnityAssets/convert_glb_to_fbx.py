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
        alpha_mode = str(material.get("alphaMode", "OPAQUE")).upper()
        if alpha_mode not in {"OPAQUE", "MASK", "BLEND"}:
            raise RuntimeError(f"Unsupported GLB alphaMode material={name} alphaMode={alpha_mode!r}")
        alpha_cutoff = float(material.get("alphaCutoff", 0.5))
        authored_visible = material.get("extras", {}).get("visible", True)
        if not isinstance(authored_visible, bool):
            raise RuntimeError(
                f"GLB material extras.visible must be boolean material={name} visible={authored_visible!r}"
            )
        result[name] = {
            "source": source,
            "alpha_mode": alpha_mode,
            "alpha_cutoff": alpha_cutoff,
            "authored_visible": authored_visible,
        }
    if not result:
        raise RuntimeError("GLB contains no materials")
    return result


def skinned_material_bindings(document, sources):
    nodes = document.get("nodes", [])
    meshes = document.get("meshes", [])
    materials = document.get("materials", [])
    result = []
    renderer_names = set()

    for node_index, node in enumerate(nodes):
        if "skin" not in node or "mesh" not in node:
            continue
        renderer = node.get("name")
        if not renderer:
            raise RuntimeError(f"Skinned GLB node has no name: node={node_index}")
        if any(c in renderer for c in "\t\r\n"):
            raise RuntimeError(f"Skinned GLB node name cannot be represented in TSV: {renderer!r}")
        if renderer in renderer_names:
            raise RuntimeError(f"Duplicate skinned GLB node name: {renderer}")
        renderer_names.add(renderer)

        mesh_index = node.get("mesh")
        if not isinstance(mesh_index, int) or mesh_index < 0 or mesh_index >= len(meshes):
            raise RuntimeError(f"Skinned GLB node has invalid mesh: node={renderer} mesh={mesh_index}")
        primitives = meshes[mesh_index].get("primitives", [])
        if not primitives:
            raise RuntimeError(f"Skinned GLB mesh has no primitives: node={renderer} mesh={mesh_index}")

        for slot, primitive in enumerate(primitives):
            material_index = primitive.get("material")
            if not isinstance(material_index, int) or material_index < 0 or material_index >= len(materials):
                raise RuntimeError(
                    f"Skinned GLB primitive has no valid material: node={renderer} slot={slot} material={material_index}"
                )
            material = materials[material_index].get("name")
            if not material or material not in sources:
                raise RuntimeError(
                    f"Skinned GLB primitive material is missing from material table: node={renderer} slot={slot}"
                )
            material_source = sources[material]
            result.append((
                renderer,
                slot,
                material,
                material_source["source"],
                material_source["alpha_mode"],
                material_source["alpha_cutoff"],
                material_source["authored_visible"],
            ))

    if not result:
        raise RuntimeError("GLB contains no skinned material bindings")
    return result


def image_extension(mime_type):
    if mime_type == "image/png":
        return ".png"
    if mime_type in {"image/jpeg", "image/jpg"}:
        return ".jpg"
    raise RuntimeError(f"Unsupported GLB image MIME type for Unity sidecar: {mime_type!r}")


def export_textures(dst, document, binary, bindings):
    output_dir = os.path.dirname(dst)
    stem = os.path.splitext(os.path.basename(dst))[0]
    images = document.get("images", [])
    views = document.get("bufferViews", [])
    exported = {}

    for source in sorted({binding[3] for binding in bindings if binding[3] is not None}):
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
        raise RuntimeError(f"GLB skinned materials contain no base-color textures: {dst}")
    print(f"Darius native texture export complete: {dst} textures={len(exported)}")
    return exported


def export_material_map(dst, exported, bindings):
    stem = os.path.splitext(os.path.basename(dst))[0]
    path = os.path.join(os.path.dirname(dst), f"{stem}__materials.tsv")
    lines = []
    textured = 0
    untextured = 0

    for renderer, slot, material, source, alpha_mode, alpha_cutoff, authored_visible in bindings:
        if any(c in material for c in "\t\r\n"):
            raise RuntimeError(f"Material name cannot be represented in TSV manifest: {material!r}")
        if source is None:
            filename = NO_TEXTURE
            untextured += 1
        else:
            filename = exported.get(source)
            if filename is None:
                raise RuntimeError(f"Base-color image was not exported: material={material} source={source}")
            textured += 1
        lines.append(
            f"{renderer}\t{slot}\t{material}\t{filename}\t{alpha_mode}\t"
            f"{alpha_cutoff:.6g}\t{1 if authored_visible else 0}"
        )

    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(
        f"Darius native material map complete: {path} renderers={len({row[0] for row in bindings})} "
        f"materials={len(lines)} textured={textured} untextured={untextured}"
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
    bindings = skinned_material_bindings(document, sources)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src, import_pack_images=True, merge_vertices=False)
    exported = export_textures(dst, document, binary, bindings)
    export_material_map(dst, exported, bindings)
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
