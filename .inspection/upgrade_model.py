"""Change the two model constants in a PyInstaller archive; never execute it."""
from pathlib import Path
import hashlib
import json
import marshal
import struct
import types
import zlib

SOURCE = Path(r'D:\WeChat\xwechat_files\wxid_5748dcurn8e422_3177\msg\file\2026-11\geography.exe')
OUTPUT = Path(__file__).resolve().parent.parent / 'geography-qwen3.8-flash-v2.exe'
MAGIC = b'MEI\x0c\x0b\x0a\x0b\x0e'
REPLACEMENTS = {'qwen-vl-ocr': 'qwen3.8-flash', 'deepseek-v3': 'qwen3.8-flash'}


def read_archive(blob):
    cookie = blob.rfind(MAGIC)
    fields = struct.unpack('!8sIIII64s', blob[cookie:cookie + 88])
    _, length, offset, toc_length, version, library = fields
    assert cookie + 88 == len(blob), 'Unexpected trailing data'
    start = cookie + 88 - length
    toc = blob[start + offset:start + offset + toc_length]
    entries = []
    cursor = 0
    while cursor < len(toc):
        size, pos, compressed, expanded, flag, kind = struct.unpack('!iIIIBc', toc[cursor:cursor + 18])
        name_bytes = toc[cursor + 18:cursor + size]
        name = name_bytes.rstrip(b'\0').decode('utf-8')
        data = blob[start + pos:start + pos + compressed]
        assert len(data) == compressed
        entries.append((name, name_bytes, data, expanded, flag, kind))
        cursor += size
    assert cursor == len(toc)
    return blob[:start], entries, version, library


def verify_code(before, after):
    assert isinstance(before, types.CodeType) and isinstance(after, types.CodeType)
    for attr in dir(before):
        if attr.startswith('co_') and attr != 'co_consts' and not callable(getattr(before, attr)):
            assert getattr(before, attr) == getattr(after, attr), attr
    assert len(before.co_consts) == len(after.co_consts)
    changed = 0
    for old, new in zip(before.co_consts, after.co_consts):
        if isinstance(old, types.CodeType):
            changed += verify_code(old, new)
        elif isinstance(old, str) and old in REPLACEMENTS:
            assert new == REPLACEMENTS[old]
            changed += 1
        elif before.co_name == 'grade_paper' and type(old) is int and old == 3136:
            assert new == 65536
            changed += 1
        else:
            assert old == new
    return changed


def main():
    original = SOURCE.read_bytes()
    assert hashlib.sha256(original).hexdigest() == '59413904b8692fcaba32ac6b7830452554ba0c599ac23d250358196218530755'
    prefix, entries, version, library = read_archive(original)
    assert version == 310
    pe_offset = struct.unpack_from('<I', prefix, 0x3c)[0]
    optional = pe_offset + 24
    directory = optional + (112 if struct.unpack_from('<H', prefix, optional)[0] == 0x20b else 96)
    assert struct.unpack_from('<II', prefix, directory + 4 * 8) == (0, 0), 'Signed EXE needs separate handling'
    payload = bytearray()
    toc = bytearray()
    modified = []
    for name, name_bytes, packed, expanded, flag, kind in entries:
        if name == 'geography':
            raw = zlib.decompress(packed) if flag else packed
            old_code = marshal.loads(raw)
            for old, new in REPLACEMENTS.items():
                old_bytes, new_bytes = old.encode(), new.encode()
                old_record = b'z' + bytes([len(old_bytes)]) + old_bytes
                new_record = b'z' + bytes([len(new_bytes)]) + new_bytes
                assert raw.count(old_record) == 1, old
                raw = raw.replace(old_record, new_record)
            old_pixel_record = b'i' + struct.pack('<i', 3136)
            new_pixel_record = b'i' + struct.pack('<i', 65536)
            assert raw.count(old_pixel_record) == 1
            raw = raw.replace(old_pixel_record, new_pixel_record)
            assert verify_code(old_code, marshal.loads(raw)) == 3
            expanded = len(raw)
            packed = zlib.compress(raw, 9) if flag else raw
            modified.append(name)
        toc.extend(struct.pack('!iIIIBc', 18 + len(name_bytes), len(payload), len(packed), expanded, flag, kind))
        toc.extend(name_bytes)
        payload.extend(packed)
    assert modified == ['geography']
    cookie = struct.pack('!8sIIII64s', MAGIC, len(payload) + len(toc) + 88, len(payload), len(toc), version, library)
    rebuilt = prefix + payload + toc + cookie
    new_prefix, new_entries, new_version, new_library = read_archive(rebuilt)
    assert prefix == new_prefix and version == new_version and library == new_library
    assert len(entries) == len(new_entries)
    for old, new in zip(entries, new_entries):
        assert old[:2] == new[:2] and old[4:] == new[4:]
        if old[0] != 'geography':
            assert old == new, old[0]
        else:
            before = zlib.decompress(old[2]) if old[4] else old[2]
            after = zlib.decompress(new[2]) if new[4] else new[2]
            assert verify_code(marshal.loads(before), marshal.loads(after)) == 3
            assert len(after) == new[3]
        if new[4]:
            assert len(zlib.decompress(new[2])) == new[3], new[0]
    assert not OUTPUT.exists(), 'Do not overwrite an existing output'
    OUTPUT.write_bytes(rebuilt)
    assert OUTPUT.read_bytes() == rebuilt
    assert SOURCE.read_bytes() == original
    report = {
        'output': str(OUTPUT), 'size': len(rebuilt),
        'source_sha256': hashlib.sha256(original).hexdigest(),
        'output_sha256': hashlib.sha256(rebuilt).hexdigest(),
        'model_changes': REPLACEMENTS, 'entries_verified': len(entries),
        'min_pixels_change': {'before': 3136, 'after': 65536},
        'other_entries_identical': len(entries) - 1,
        'bytecode_instructions_unchanged': True, 'original_unchanged': True,
        'live_api_test': 'Not run: requires user API key',
    }
    OUTPUT.with_suffix('.verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=True, indent=2))


if __name__ == '__main__':
    main()
