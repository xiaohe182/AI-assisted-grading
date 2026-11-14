from pathlib import Path
import marshal,types,dis
p=Path('.inspection/original_geography.marshal');code=marshal.loads(p.read_bytes())
def walk(c):
 if c.co_name=='grade_paper':
  print('grade_paper strings:')
  for v in c.co_consts:
   if isinstance(v,str):
    if 'sk-' not in v: print(repr(v))
  ins=list(dis.get_instructions(c))
  for i,x in enumerate(ins):
   if x.opname=='LOAD_CONST' and x.argval in ('qwen-vl-ocr','deepseek-v3'):
    print('MODEL CALL:')
    for y in ins[max(0,i-6):i+5]:print(y.opname, y.argrepr)
 for v in c.co_consts:
  if isinstance(v,types.CodeType):walk(v)
walk(code)
