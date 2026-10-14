import struct, sys, os
src = sys.argv[1]; out = sys.argv[2]
sizes = [256, 128, 64, 48, 32, 24, 16]
entries = []
for s in sizes:
    if s >= 128:
        data = open(os.path.join(src, f"icon_{s}.png"), "rb").read()
    else:
        raw = open(os.path.join(src, f"icon_{s}.raw"), "rb").read()  # BGRA top-down
        stride = s * 4
        rows = [raw[i*stride:(i+1)*stride] for i in range(s)]
        xor = b"".join(reversed(rows))  # BMP은 bottom-up
        mask_stride = ((s + 31) // 32) * 4
        mask = b""
        for row in reversed(rows):
            bits = 0
            for x in range(s):
                a = row[x*4 + 3]
                bits = (bits << 1) | (1 if a == 0 else 0)
            bits <<= (mask_stride*8 - s)
            mask += bits.to_bytes(mask_stride, "big")
        header = struct.pack("<IiiHHIIiiII", 40, s, s*2, 1, 32, 0, len(xor)+len(mask), 0, 0, 0, 0)
        data = header + xor + mask
    entries.append((s, data))
offset = 6 + 16*len(entries)
with open(out, "wb") as f:
    f.write(struct.pack("<HHH", 0, 1, len(entries)))
    for s, data in entries:
        f.write(struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    for _, data in entries:
        f.write(data)
print("ico", os.path.getsize(out), "bytes,", len(entries), "images")
