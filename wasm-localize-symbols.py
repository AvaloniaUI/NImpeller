#!/usr/bin/env python3
"""Rewrites a relocatable wasm object so that every defined symbol outside the Impeller C API is
BINDING_LOCAL, and drops its COMDAT table.

libimpeller.a statically bundles Skia, HarfBuzz, ICU, wuffs, ... whose global symbols collide with
other archives an app links (SkiaSharp, HarfBuzzSharp). wasm-ld has neither --exclude-libs nor
--allow-multiple-definition and llvm-objcopy ignores symbol operations for wasm, so the `linking`
section is patched directly:

* the symbol table's binding field (low two bits of the flags) is set to local, and
* the COMDAT subsection is removed — the merged object is already deduplicated internally, and
  keeping the groups would make wasm-ld discard same-named COMDATs (template instantiations) in
  other objects in favour of the now-local copies here.

usage: wasm-localize-symbols.py <in.o> <out.o> [keep-prefix ...]   (default prefix: Impeller)
"""
import sys

BINDING_MASK = 0x3  # 0 = global, 1 = weak, 2 = local
BINDING_LOCAL = 0x2
UNDEFINED = 0x10
EXPLICIT_NAME = 0x40
KIND_DATA = 1
KIND_SECTION = 3
COMDAT_SUBSECTION = 7
SYMTAB_SUBSECTION = 8


def read_leb(buf, pos):
    result = shift = 0
    while True:
        b = buf[pos]
        pos += 1
        result |= (b & 0x7F) << shift
        shift += 7
        if not b & 0x80:
            return result, pos


def write_leb(value):
    out = bytearray()
    while True:
        b = value & 0x7F
        value >>= 7
        if value:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


def find_linking(buf):
    """Returns (section_start, payload_start, section_end) of the `linking` custom section."""
    assert buf[:8] == b"\0asm\1\0\0\0", "not a wasm binary"
    pos = 8
    while pos < len(buf):
        start = pos
        sid = buf[pos]
        pos += 1
        size, pos = read_leb(buf, pos)
        end = pos + size
        if sid == 0:
            name_len, p = read_leb(buf, pos)
            if buf[p:p + name_len] == b"linking":
                return start, p + name_len, end
        pos = end
    raise SystemExit("no linking section: input must be a relocatable object (wasm-ld -r)")


def localize_symtab(sub, keep_prefixes):
    """Patches bindings in a symbol table subsection payload in place; returns (count, localized, kept)."""
    pos = 0
    count, pos = read_leb(sub, pos)
    localized = kept = 0
    for _ in range(count):
        kind = sub[pos]
        pos += 1
        flags_pos = pos
        flags, pos = read_leb(sub, pos)
        defined = not flags & UNDEFINED
        name = None
        if kind == KIND_DATA:
            name_len, pos = read_leb(sub, pos)
            name = sub[pos:pos + name_len]
            pos += name_len
            if defined:
                for _ in range(3):  # segment index, offset, size
                    _, pos = read_leb(sub, pos)
        elif kind == KIND_SECTION:
            _, pos = read_leb(sub, pos)
        else:  # function, global, tag, table
            _, pos = read_leb(sub, pos)
            if defined or flags & EXPLICIT_NAME:
                name_len, pos = read_leb(sub, pos)
                name = sub[pos:pos + name_len]
                pos += name_len
        if not defined or flags & BINDING_MASK == BINDING_LOCAL or name is None:
            continue
        if any(name.startswith(prefix) for prefix in keep_prefixes):
            kept += 1
            continue
        sub[flags_pos] = (sub[flags_pos] & ~BINDING_MASK) | BINDING_LOCAL
        localized += 1
    return count, localized, kept


def rewrite(buf, keep_prefixes):
    start, pos, end = find_linking(buf)
    version_start = pos
    _version, pos = read_leb(buf, pos)
    payload = bytearray(buf[version_start:pos])
    stats = (0, 0, 0)
    comdats = 0
    while pos < end:
        sub_type = buf[pos]
        pos += 1
        size, pos = read_leb(buf, pos)
        sub = bytearray(buf[pos:pos + size])
        pos += size
        if sub_type == COMDAT_SUBSECTION:
            comdats, _ = read_leb(sub, 0)
            continue
        if sub_type == SYMTAB_SUBSECTION:
            stats = localize_symtab(sub, keep_prefixes)
        payload += bytes([sub_type]) + write_leb(len(sub)) + sub
    name = write_leb(len(b"linking")) + b"linking"
    section = bytes([0]) + write_leb(len(name) + len(payload)) + name + payload
    return buf[:start] + section + buf[end:], stats, comdats


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    keep = [p.encode() for p in (sys.argv[3:] or ["Impeller"])]
    buf = bytearray(open(sys.argv[1], "rb").read())
    out, (count, localized, kept), comdats = rewrite(buf, keep)
    open(sys.argv[2], "wb").write(out)
    print(f"{sys.argv[2]}: {count} symbols, {localized} localized, {kept} kept global, "
          f"{comdats} COMDAT groups dropped")


if __name__ == "__main__":
    main()
