"""Build an OCR copy of v3; preserve its UI and grading controls."""
from pathlib import Path
import hashlib
import json
import marshal
import struct
import types
import zlib
from upgrade_model import read_archive

root = Path(__file__).resolve().parent.parent
source = root / 'geography-qwen3.8-flash-v3.exe'
output = root / 'geography-qwen3.8-flash-v4-ocr.exe'
original = source.read_bytes()
assert hashlib.sha256(original).hexdigest() == 'fd1a3ec7509e05407b57a49cb83655296f5e6e772ea22d983b0ca346ea8edef7'
text = (root / '.inspection/responsive_methods.py').read_text(encoding='utf-8')
original_compiled = compile(text, 'responsive_methods.py', 'exec')
original_cls = next(c for c in original_compiled.co_consts if isinstance(c, types.CodeType))
original_grade = next(c for c in original_cls.co_consts if isinstance(c, types.CodeType) and c.co_name == 'grade_paper')

# OCR extracts text; only the scoring model has a thinking-mode switch.
assert text.count("model='qwen3.8-flash'") == 2
text = text.replace("model='qwen3.8-flash'", "model='qwen-vl-ocr'", 1)
text = text.replace("extra_body={'enable_thinking': False})", ")", 1)
old_end = "'。你只要返回分数数值就可以，不用分析过程。'}])"
new_end = "'。你只要返回分数数值就可以，不用分析过程。'}],\n                            extra_body={'enable_thinking': False})"
assert text.count(old_end) == 1
text = text.replace(old_end, new_end, 1)
compiled = compile(text, 'responsive_methods_ocr.py', 'exec')
cls = next(c for c in compiled.co_consts if isinstance(c, types.CodeType))
grade = next(c for c in cls.co_consts if isinstance(c, types.CodeType) and c.co_name == 'grade_paper')

prefix, entries, version, library = read_archive(original)
assert version == 310
payload, toc = bytearray(), bytearray()
before_cls = None
for name, name_bytes, packed, expanded, flag, kind in entries:
    if name == 'geography':
        module = marshal.loads(zlib.decompress(packed) if flag else packed)
        before_cls = next(c for c in module.co_consts if isinstance(c, types.CodeType) and c.co_name == 'AIGradingApp')
        existing_grade = next(c for c in before_cls.co_consts if isinstance(c, types.CodeType) and c.co_name == 'grade_paper')
        assert existing_grade == original_grade, 'v3 source must match the embedded method'
        after_cls = before_cls.replace(co_consts=tuple(grade if c is existing_grade else c for c in before_cls.co_consts))
        after_module = module.replace(co_consts=tuple(after_cls if c is before_cls else c for c in module.co_consts))
        raw = marshal.dumps(after_module)
        packed, expanded = (zlib.compress(raw, 9) if flag else raw), len(raw)
    toc.extend(struct.pack('!iIIIBc', 18 + len(name_bytes), len(payload), len(packed), expanded, flag, kind))
    toc.extend(name_bytes)
    payload.extend(packed)
assert before_cls is not None
cookie = struct.pack('!8sIIII64s', b'MEI\x0c\x0b\x0a\x0b\x0e', len(payload) + len(toc) + 88, len(payload), len(toc), version, library)
rebuilt = prefix + payload + toc + cookie
new_prefix, new_entries, new_version, new_library = read_archive(rebuilt)
assert (prefix, version, library) == (new_prefix, new_version, new_library)
assert len(entries) == len(new_entries)
for before, after in zip(entries, new_entries):
    assert before[:2] == after[:2] and before[4:] == after[4:]
    if before[0] != 'geography':
        assert before == after
    else:
        decoded = marshal.loads(zlib.decompress(after[2]) if after[4] else after[2])
        assert decoded.co_code == module.co_code
        decoded_cls = next(c for c in decoded.co_consts if isinstance(c, types.CodeType) and c.co_name == 'AIGradingApp')
        for a, b in zip(module.co_consts, decoded.co_consts):
            if a is not before_cls:
                assert a == b
        assert decoded_cls.co_code == before_cls.co_code
        for a, b in zip(before_cls.co_consts, decoded_cls.co_consts):
            assert b == (grade if a is existing_grade else a)
    if after[4]:
        assert len(zlib.decompress(after[2])) == after[3]
assert not output.exists(), 'Keep existing builds; never overwrite an EXE'
(root / '.inspection/responsive_methods_ocr.py').write_text(text, encoding='utf-8')
output.write_bytes(rebuilt)
assert output.read_bytes() == rebuilt and source.read_bytes() == original
report = dict(output=output.name, size=len(rebuilt), sha256=hashlib.sha256(rebuilt).hexdigest(),
              source=source.name, source_sha256=hashlib.sha256(original).hexdigest(),
              ocr_model='qwen-vl-ocr', scoring_model='qwen3.8-flash', scoring_thinking=False,
              changed_methods=['grade_paper'], other_archive_entries_unchanged=len(entries)-1,
              original_unchanged=True, live_api_test=False)
output.with_suffix('.verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
