"""Inspect embedded code statically; never run the grading application."""
from pathlib import Path
import sys, marshal, types, dis, zlib, hashlib, json
sys.path.insert(0, str(Path(__file__).resolve().parent))
from upgrade_model import read_archive

sys.stdout.reconfigure(encoding='utf-8')
root = Path(__file__).resolve().parent.parent
blob = (root / 'geography-qwen3.8-flash-v3.exe').read_bytes()
_, entries, version, _ = read_archive(blob)
entry = next(e for e in entries if e[0] == 'geography')
module = marshal.loads(zlib.decompress(entry[2]) if entry[4] else entry[2])
source = (root / '.inspection/responsive_methods.py').read_text(encoding='utf-8')
compiled = compile(source, 'responsive_methods.py', 'exec')
source_cls = next(c for c in compiled.co_consts if isinstance(c, types.CodeType))
app_cls = next(c for c in module.co_consts if isinstance(c, types.CodeType) and c.co_name == 'AIGradingApp')

def compare(a, b):
    for attr in ('co_code', 'co_names', 'co_varnames', 'co_freevars', 'co_cellvars', 'co_argcount', 'co_flags', 'co_firstlineno', 'co_linetable'):
        assert getattr(a, attr) == getattr(b, attr), (a.co_name, attr)
    assert len(a.co_consts) == len(b.co_consts)
    for x, y in zip(a.co_consts, b.co_consts):
        if isinstance(x, types.CodeType):
            compare(x, y)
        else:
            assert x == y, a.co_name

for c in source_cls.co_consts:
    if isinstance(c, types.CodeType):
        compare(c, next(x for x in app_cls.co_consts if isinstance(x, types.CodeType) and x.co_name == c.co_name))
        print('VERIFIED_SOURCE', c.co_name, c.co_firstlineno)
print('SHA256', hashlib.sha256(blob).hexdigest())
print('PYTHON', version, 'ARCHIVE_ENTRIES', len(entries))
print('SOURCE_LINES', len(source.splitlines()), 'NONBLANK', sum(bool(x.strip()) for x in source.splitlines()))

def walk(c, depth=0):
    lines = [line for _, _, line in c.co_lines() if line is not None]
    print('  '*depth, c.co_name, 'FILE', c.co_filename, 'LINE', c.co_firstlineno, 'LAST', max(lines) if lines else None)
    for v in c.co_consts:
        if isinstance(v, types.CodeType):
            walk(v, depth+1)
walk(module)
out = root / '.inspection/v3_embedded_disassembly.txt'
with out.open('w', encoding='utf-8') as f:
    dis.dis(module, file=f)
print('DISASSEMBLY', out)
