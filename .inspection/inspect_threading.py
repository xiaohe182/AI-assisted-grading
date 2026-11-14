import marshal,types,dis,sys
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
c=marshal.loads(Path('.inspection/original_geography.marshal').read_bytes())
print('Module names:',c.co_names)
for x in dis.get_instructions(c):print(x.offset,x.opname,x.argrepr if not isinstance(x.argval,types.CodeType) else x.argval.co_name)
def walk(c):
 if c.co_name in ('stop_grading','reset_grading_state','screenshot'):
  print('\nMETHOD',c.co_name);dis.dis(c)
 if c.co_name=='__init__':print('INIT names:',c.co_names)
 for x in c.co_consts:
  if isinstance(x,types.CodeType):walk(x)
walk(c)
