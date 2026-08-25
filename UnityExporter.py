import os
import re
import struct
import sys
import array
import gzip

FORMAT_BYTE_SIZE = {
    0: 4, 1: 2, 2: 1, 3: 1, 4: 2, 5: 2, 6: 1, 7: 1, 8: 2, 9: 2, 10: 4, 11: 4,
}

MESH_DOC_RE = re.compile(r'--- !u!43 &\d+\nMesh:\n(.*?)(?=\n--- !u!|\Z)', re.DOTALL)
NAME_RE = re.compile(r'^  m_Name:\s*(.+)$', re.MULTILINE)
VERTEX_COUNT_RE = re.compile(r'^\s*m_VertexCount:\s*(\d+)$', re.MULTILINE)
CHANNELS_BLOCK_RE = re.compile(r'm_Channels:\n(.*?)\n\s*m_DataSize:', re.DOTALL)
CHANNEL_ENTRY_RE = re.compile(
    r'-\s*stream:\s*(\d+)\s*\n\s*offset:\s*(\d+)\s*\n\s*format:\s*(\d+)\s*\n\s*dimension:\s*(\d+)'
)
TYPELESS_RE = re.compile(r'^\s*_typelessdata:\s*([0-9a-fA-F]*)\s*$', re.MULTILINE)
INDEX_FORMAT_RE = re.compile(r'^\s*m_IndexFormat:\s*(\d+)$', re.MULTILINE)
INDEX_BUFFER_RE = re.compile(r'^\s*m_IndexBuffer:\s*([0-9a-fA-F]*)\s*$', re.MULTILINE)


def compute_stream_layout(channels):
    if not channels:
        return [], []
    stream_count = max(c[0] for c in channels) + 1
    strides = [0] * stream_count
    for stream, offset, fmt, dim in channels:
        strides[stream] = max(strides[stream], offset + dim * FORMAT_BYTE_SIZE.get(fmt, 4))
    strides = [(s + 3) & ~3 for s in strides]

    offsets = [0] * stream_count
    running = 0
    for s in range(stream_count):
        offsets[s] = running
        running += strides[s]
    return strides, offsets


def decode_mesh(body, vertex_count, blob, index_bytes, is16bit):
    channels_match = CHANNELS_BLOCK_RE.search(body)
    if not channels_match:
        return None, None, None

    channels = []
    for stream, offset, fmt, dim in CHANNEL_ENTRY_RE.findall(channels_match.group(1)):
        dim_val = int(dim) & 0xF
        if dim_val == 0:
            continue
        channels.append((int(stream), int(offset), int(fmt), dim_val))

    if not channels:
        return None, None, None

    pos_stream, pos_offset, pos_fmt, pos_dim = channels[0]
    strides, stream_blob_offsets = compute_stream_layout(channels)
    pos_stream_start = stream_blob_offsets[pos_stream]
    pos_stride = strides[pos_stream]

    positions_flat = array.array('f')

    if pos_stride == 12 and pos_offset == 0:
        try:
            expected_size = vertex_count * 12
            end_idx = pos_stream_start + expected_size
            positions_flat.frombytes(blob[pos_stream_start:end_idx])
        except Exception:
            for i in range(vertex_count):
                base = pos_stream_start + i * pos_stride + pos_offset
                positions_flat.extend(struct.unpack_from('<fff', blob, base))
    else:
        for i in range(vertex_count):
            base = pos_stream_start + i * pos_stride + pos_offset
            try:
                positions_flat.extend(struct.unpack_from('<fff', blob, base))
            except struct.error:
                break

    if is16bit:
        raw = array.array('H')
        if index_bytes:
            raw.frombytes(index_bytes)
        indices = array.array('I', raw)
    else:
        indices = array.array('I')
        if index_bytes:
            indices.frombytes(index_bytes)

    return positions_flat, indices, is16bit


def find_candidate_files(root_dir):
    exts = (".mesh", ".asset", ".prefab", ".unity")
    for dirpath, _, filenames in os.walk(root_dir):
        for fn in filenames:
            if fn.lower().endswith(exts):
                yield os.path.join(dirpath, fn)


def export(root_dir, out_path, compresslevel=9):
    found_data = []

    print(f"Scanning {root_dir}...")

    for filepath in find_candidate_files(root_dir):
        try:
            with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read()
        except Exception:
            continue

        if "--- !u!43" not in content:
            continue

        for doc_match in MESH_DOC_RE.finditer(content):
            body = doc_match.group(1)

            name_match = NAME_RE.search(body)
            if not name_match:
                continue
            name = name_match.group(1).strip().strip("'\"")

            vc_match = VERTEX_COUNT_RE.search(body)
            typeless_match = TYPELESS_RE.search(body)
            idxfmt_match = INDEX_FORMAT_RE.search(body)
            idxbuf_match = INDEX_BUFFER_RE.search(body)

            if not (vc_match and typeless_match and idxfmt_match and idxbuf_match):
                continue

            vertex_count = int(vc_match.group(1))
            hex_blob = typeless_match.group(1)
            hex_idx = idxbuf_match.group(1)

            if not hex_blob or vertex_count <= 0:
                continue

            blob = bytes.fromhex(hex_blob)
            index_bytes = bytes.fromhex(hex_idx) if hex_idx else b""
            is16bit = int(idxfmt_match.group(1)) == 0

            pos_arr, idx_arr, is16 = decode_mesh(body, vertex_count, blob, index_bytes, is16bit)

            if pos_arr is None or len(pos_arr) == 0:
                continue

            unique_id = f"{filepath}#{name}#{len(found_data)}"
            found_data.append({
                "name": name,
                "key": unique_id,
                "positions": pos_arr,
                "indices": idx_arr,
                "is16bit": is16
            })
            print(f"  found '{name}' ({vertex_count} verts) in {os.path.basename(filepath)}")

    if not found_data:
        print("No meshes found.")
        return

    print(f"\nWriting {len(found_data)} meshes to {out_path} (gzip level {compresslevel})...")

    raw_size = 0

    with gzip.open(out_path, "wb", compresslevel=compresslevel) as f:
        header = struct.pack("<i", len(found_data))
        f.write(header)
        raw_size += len(header)

        for item in found_data:
            name_bytes = item["name"].encode("utf-8")
            f.write(struct.pack("<i", len(name_bytes)))
            f.write(name_bytes)
            raw_size += 4 + len(name_bytes)

            key_bytes = item["key"].encode("utf-8")
            f.write(struct.pack("<i", len(key_bytes)))
            f.write(key_bytes)
            raw_size += 4 + len(key_bytes)

            pos_bytes = item["positions"].tobytes()
            f.write(struct.pack("<i", len(item["positions"]) // 3))
            f.write(pos_bytes)
            raw_size += 4 + len(pos_bytes)

            idx_bytes = item["indices"].tobytes()
            f.write(struct.pack("<i", len(item["indices"])))
            f.write(idx_bytes)
            raw_size += 4 + len(idx_bytes)

    compressed_size = os.path.getsize(out_path)
    ratio = (1 - compressed_size / raw_size) * 100 if raw_size else 0
    print(f"Done. Exported {len(found_data)} meshes.")
    print(f"  Uncompressed: {raw_size / 1_048_576:.2f} MB")
    print(f"  Compressed:   {compressed_size / 1_048_576:.2f} MB ({ratio:.1f}% smaller)")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} <ripped_project_root> <output.bin>")
        sys.exit(1)

    export(sys.argv[1], sys.argv[2])