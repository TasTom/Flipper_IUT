"""Fetch only the GGUF header (via HTTP Range) and report metadata KVs like general.architecture.

Parses the GGUF KV section directly from partial bytes, so multi-GB weights are never downloaded.
"""
import struct
import sys
import urllib.request

url = sys.argv[1]
probe_bytes = int(sys.argv[2]) if len(sys.argv) > 2 else 2_000_000

req = urllib.request.Request(url, headers={"Range": f"bytes=0-{probe_bytes - 1}"})
with urllib.request.urlopen(req) as r:
    data = r.read()
print(f"fetched {len(data)} bytes (status {r.status})")

GGUF_STRING, GGUF_ARRAY = 8, 9
SCALARS = {0: ("b", 1), 1: ("B", 1), 2: ("h", 2), 3: ("H", 2), 4: ("i", 4), 5: ("I", 4),
           6: ("f", 4), 7: ("?", 1), 10: ("Q", 8), 11: ("q", 8), 12: ("d", 8)}


class Cursor:
    def __init__(self, buf, pos=0):
        self.buf, self.pos = buf, pos

    def u32(self):
        v = struct.unpack_from("<I", self.buf, self.pos)[0]
        self.pos += 4
        return v

    def u64(self):
        v = struct.unpack_from("<Q", self.buf, self.pos)[0]
        self.pos += 8
        return v

    def string(self):
        n = self.u64()
        v = self.buf[self.pos:self.pos + n]
        self.pos += n
        return v.decode("utf-8", "replace")


def read_value(c, vtype, depth=0):
    if vtype == GGUF_STRING:
        return c.string()
    if vtype == GGUF_ARRAY:
        item_type = c.u32()
        count = c.u64()
        if depth > 0:
            for _ in range(count):
                read_value(c, item_type, depth + 1)
            return f"<array len={count}>"
        return [read_value(c, item_type, depth + 1) for _ in range(count)]
    fmt, size = SCALARS[vtype]
    v = struct.unpack_from("<" + fmt, c.buf, c.pos)[0]
    c.pos += size
    return v


if data[:4] != b"GGUF":
    sys.exit(f"not a GGUF file (magic={data[:4]!r})")

c = Cursor(data, 4)
version = c.u32()
tensor_count = c.u64()
kv_count = c.u64()
print(f"gguf version={version} tensor_count={tensor_count} kv_count={kv_count}")

interesting = ("general.architecture", "general.name", "general.type", "general.file_type",
               "general.size_label", "general.basename", "general.quantization_version")
for _ in range(kv_count):
    try:
        key = c.string()
    except Exception as exc:
        sys.exit(f"metadata truncated after {c.pos} bytes ({exc}); increase probe size")
    vtype = c.u32()
    value = read_value(c, vtype)
    if key in interesting or "architecture" in key:
        shown = value if not isinstance(value, list) else f"<array len={len(value)}>"
        print(f"  {key} = {shown}")
    if c.pos >= len(data):
        print("  (metadata continues beyond probe; raise probe size if needed)")
        break

