"""Converts the Sketchfab mushroom-cloud glTF into the mod's own model format.

This work is based on "Mushroom cloud low poly"
(https://sketchfab.com/3d-models/mushroom-cloud-low-poly-7d37e0570a164eaba038594bf3d664f6)
by PROMEIISTER (https://sketchfab.com/PROMEIISTER), licensed under CC-BY-4.0
(http://creativecommons.org/licenses/by/4.0/).

What it does, and why:

  - bakes the glTF node transforms into the vertices, so the OBJ needs no hierarchy
  - normalises the model: centred on X/Z, base at y=0, height exactly 1.0, so the game
    code can scale it straight to metres
  - writes an OBJ whose v, vt and vn lists are index-aligned, one of each per vertex in
    the same order - the convention the mod's ObjParser understands textures through.
    glTF vertices already carry unified attributes, so this costs nothing
  - bakes one diffuse texture from the PBR set: baseColor shaded by the occlusion map,
    plus a fraction of the emissive (the fire glowing in the crevices). CS's runtime
    Standard shader gets one map, so the lighting the PBR textures would have provided
    is baked into it instead
  - resizes the texture to 1024 so the Workshop download stays small

Usage:  python tools/cloud-model/convert.py <gltf-folder> [out-models-folder]
"""
import json
import os
import struct
import sys

from PIL import Image

EMISSIVE_BAKE = 0.35   # how much of the fire glow is baked in permanently
AO_FLOOR = 0.45        # occlusion never darkens below this - a cloud is lit by sky from all sides
TEXTURE_SIZE = 1024
COMPONENT_FORMATS = {5126: ("f", 4), 5125: ("I", 4), 5123: ("H", 2)}
TYPE_WIDTHS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def read_accessor(gltf, buffers, index):
    acc = gltf["accessors"][index]
    view = gltf["bufferViews"][acc["bufferView"]]
    fmt, size = COMPONENT_FORMATS[acc["componentType"]]
    width = TYPE_WIDTHS[acc["type"]]
    offset = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = view.get("byteStride", size * width)
    data = buffers[view.get("buffer", 0)]
    out = []
    for i in range(acc["count"]):
        base = offset + i * stride
        out.append(struct.unpack_from("<" + fmt * width, data, base))
    return out


def matmul(a, b):
    """Column-major 4x4 multiply, as glTF stores its matrices."""
    return [sum(a[row + 4 * k] * b[k + 4 * col] for k in range(4))
            for col in range(4) for row in range(4)]


def transform(m, p, w=1.0):
    x = m[0] * p[0] + m[4] * p[1] + m[8] * p[2] + m[12] * w
    y = m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13] * w
    z = m[2] * p[0] + m[6] * p[1] + m[10] * p[2] + m[14] * w
    return (x, y, z)


def node_world_matrices(gltf):
    """Walks the scene graph and returns each node's composed world matrix."""
    identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    world = {}

    def walk(index, parent):
        node = gltf["nodes"][index]
        local = node.get("matrix", identity)
        world[index] = matmul(parent, local)
        for child in node.get("children", []):
            walk(child, world[index])

    for root in gltf["scenes"][gltf.get("scene", 0)]["nodes"]:
        walk(root, identity)
    return world


def main():
    src = sys.argv[1]
    out_dir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        os.path.dirname(__file__), "..", "..", "src", "MissileDisaster", "Models")
    gltf = json.load(open(os.path.join(src, "scene.gltf")))
    buffers = [open(os.path.join(src, b["uri"]), "rb").read() for b in gltf["buffers"]]

    # The one mesh, with its node's world transform baked in.
    mesh_node = next(i for i, n in enumerate(gltf["nodes"]) if "mesh" in n)
    world = node_world_matrices(gltf)[mesh_node]
    prim = gltf["meshes"][gltf["nodes"][mesh_node]["mesh"]]["primitives"][0]

    positions = [transform(world, p) for p in read_accessor(gltf, buffers, prim["attributes"]["POSITION"])]
    normals = [transform(world, n, w=0.0) for n in read_accessor(gltf, buffers, prim["attributes"]["NORMAL"])]
    uvs = read_accessor(gltf, buffers, prim["attributes"]["TEXCOORD_0"])
    indices = [i[0] for i in read_accessor(gltf, buffers, prim["indices"])]

    # Normalise: centred on X/Z, base at y=0, height 1.
    xs, ys, zs = zip(*positions)
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    base, height = min(ys), max(ys) - min(ys)
    positions = [((x - cx) / height, (y - base) / height, (z - cz) / height) for x, y, z in positions]

    xs, ys, zs = zip(*positions)
    half_x = max(abs(min(xs)), abs(max(xs)))
    half_z = max(abs(min(zs)), abs(max(zs)))
    print(f"vertices={len(positions)} triangles={len(indices) // 3}")
    print(f"normalised: height=1.0  cap half-width x={half_x:.3f} z={half_z:.3f}")

    os.makedirs(out_dir, exist_ok=True)
    credit = ("Based on \"Mushroom cloud low poly\" by PROMEIISTER "
              "(https://sketchfab.com/3d-models/mushroom-cloud-low-poly-7d37e0570a164eaba038594bf3d664f6), CC-BY-4.0.")

    # OBJ: v, vt and vn index-aligned, which is what lets the mod's parser keep UVs
    # without any per-corner index bookkeeping. glTF UVs are top-left origin; OBJ's are
    # bottom-left, so V is flipped.
    with open(os.path.join(out_dir, "MushroomCloud.obj"), "w", newline="\n") as f:
        f.write(f"# {credit}\n# Generated by tools/cloud-model/convert.py - do not edit by hand.\n")
        f.write("mtllib MushroomCloud.mtl\nusemtl MyClouds\n")
        for p in positions:
            f.write("v %.6f %.6f %.6f\n" % p)
        for u, v in uvs:
            f.write("vt %.6f %.6f\n" % (u, 1.0 - v))
        for n in normals:
            f.write("vn %.6f %.6f %.6f\n" % n)
        for i in range(0, len(indices), 3):
            a, b, c = indices[i] + 1, indices[i + 1] + 1, indices[i + 2] + 1
            f.write(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")

    with open(os.path.join(out_dir, "MushroomCloud.mtl"), "w", newline="\n") as f:
        f.write(f"# {credit}\nnewmtl MyClouds\nKd 1.0 1.0 1.0\nd 1.0\nmap_Kd MushroomCloud.png\n")

    # Texture bake: baseColor * shaded occlusion + a fraction of the emissive.
    tex = gltf["materials"][0]["pbrMetallicRoughness"]["baseColorTexture"]["index"]
    def image_of(index):
        uri = gltf["images"][gltf["textures"][index]["source"]]["uri"]
        return Image.open(os.path.join(src, uri)).convert("RGB").resize((TEXTURE_SIZE, TEXTURE_SIZE), Image.LANCZOS)
    base_img = image_of(tex)
    ao_img = image_of(gltf["materials"][0]["occlusionTexture"]["index"]).getchannel(0)  # occlusion is the R channel
    emissive_img = image_of(gltf["materials"][0]["emissiveTexture"]["index"])

    out = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE))
    bp, ap, ep, op = base_img.load(), ao_img.load(), emissive_img.load(), out.load()
    for y in range(TEXTURE_SIZE):
        for x in range(TEXTURE_SIZE):
            ao = AO_FLOOR + (1.0 - AO_FLOOR) * (ap[x, y] / 255.0)
            r, g, b = bp[x, y]
            er, eg, eb = ep[x, y]
            op[x, y] = (min(255, int(r * ao + er * EMISSIVE_BAKE)),
                        min(255, int(g * ao + eg * EMISSIVE_BAKE)),
                        min(255, int(b * ao + eb * EMISSIVE_BAKE)))
    out.save(os.path.join(out_dir, "MushroomCloud.png"), optimize=True)
    print(f"wrote MushroomCloud.obj / .mtl / .png ({TEXTURE_SIZE}px) to {os.path.abspath(out_dir)}")


if __name__ == "__main__":
    main()
