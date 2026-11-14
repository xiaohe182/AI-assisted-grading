"""Build a copy with only grading/control method bodies replaced."""
from pathlib import Path
import hashlib
import json
import marshal
import struct
import types
import zlib
from upgrade_model import read_archive

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'geography.exe'
OUTPUT = ROOT / 'geography-qwen3.8-flash-v3.exe'
original = SOURCE.read_bytes()
assert hashlib.sha256(original).hexdigest() == '81dfe5bf531045b0cafa3be468f75689450ccf485f789014fd63149c8996961a'
replacement = compile((ROOT / '.inspection/responsive_methods.py').read_text(encoding='utf-8'), 'responsive_methods.py', 'exec')
replacement_class = next(c for c in replacement.co_consts if isinstance(c, types.CodeType))
methods = {c.co_name: c for c in replacement_class.co_consts if isinstance(c, types.CodeType)}
assert set(methods) == {'grade_paper', 'stop_grading', 'reset_grading_state'}
prefix, entries, version, library = read_archive(original)
assert version == 310
payload, toc = bytearray(), bytearray()
changed = []
for name, name_bytes, packed, expanded, flag, kind in entries:
    if name == 'geography':
        module = marshal.loads(zlib.decompress(packed))
        cls = next(c for c in module.co_consts if isinstance(c, types.CodeType) and c.co_name == 'AIGradingApp')
        patched_class = cls.replace(co_consts=tuple(methods[c.co_name] if isinstance(c, types.CodeType) and c.co_name in methods else c for c in cls.co_consts))
        patched_module = module.replace(co_consts=tuple(patched_class if c is cls else c for c in module.co_consts))
        assert module.co_code == patched_module.co_code and cls.co_code == patched_class.co_code
        for a, b in zip(cls.co_consts, patched_class.co_consts):
            if isinstance(a, types.CodeType) and a.co_name in methods:
                changed.append(a.co_name)
            else:
                assert a == b
        raw = marshal.dumps(patched_module)
        assert isinstance(marshal.loads(raw), types.CodeType)
        packed, expanded = zlib.compress(raw, 9), len(raw)
    toc.extend(struct.pack('!iIIIBc', 18 + len(name_bytes), len(payload), len(packed), expanded, flag, kind))
    toc.extend(name_bytes)
    payload.extend(packed)
assert set(changed) == set(methods)
cookie = struct.pack('!8sIIII64s', b'MEI\x0c\x0b\x0a\x0b\x0e', len(payload) + len(toc) + 88, len(payload), len(toc), version, library)
rebuilt = prefix + payload + toc + cookie
new_prefix, new_entries, _, _ = read_archive(rebuilt)
assert new_prefix == prefix and len(entries) == len(new_entries)
for before, after in zip(entries, new_entries):
    assert before[0:2] == after[0:2] and before[4:] == after[4:]
    if before[0] != 'geography':
        assert before == after
    if after[4]:
        assert len(zlib.decompress(after[2])) == after[3]
if OUTPUT.exists():
    assert hashlib.sha256(OUTPUT.read_bytes()).hexdigest() == 'b0d517874bb1dccdb189c034c84ce6cc77ec235c5d51c937f7f950fff86aadb5', 'Existing output was modified'
OUTPUT.write_bytes(rebuilt)
assert OUTPUT.read_bytes() == rebuilt and SOURCE.read_bytes() == original
report = dict(output=OUTPUT.name, size=len(rebuilt), sha256=hashlib.sha256(rebuilt).hexdigest(),
              changed_methods=changed, other_archive_entries_unchanged=1045,
              original_unchanged=True, live_api_test=False)
OUTPUT.with_suffix('.verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
