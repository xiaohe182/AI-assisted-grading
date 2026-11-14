import sys,marshal,types,dis
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
c=marshal.loads(Path('.inspection/original_geography.marshal').read_bytes())
def walk(c):
 if c.co_name=='grade_paper':
  print('CONSTANTS',list(enumerate(c.co_consts)))
  dis.dis(c)
 for v in c.co_consts:
  if isinstance(v,types.CodeType):walk(v)
walk(c)
