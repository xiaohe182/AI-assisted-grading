import test from 'node:test';
import assert from 'node:assert/strict';
import {readGrade,gradeText,desktopRequest,readOcrText,readAnswer} from '../src/grading.js';
import {defaultTeacher,initialSettings} from '../src/domain.js';

test('accepts direct scores, decimals, score labels and legacy JSON without requiring maximum',()=>{
 for(const text of ['5.5','5.5分','得分：5.5分','```json\n{"score":5.5}\n```','{"score":"5.5","maxScore":6}']) {
  assert.equal(readGrade(text,.5).score,5.5);
 }
 assert.equal(readGrade('0',1).score,0);
 assert.equal(readGrade('{"score":6}',1).maxScore,null);
});

test('OCR JSON becomes readable text without coordinates or question metadata',()=>{
 const fixture={stem:{text:'计算练习·答题页\n\n姓名: 林晓 班级: 3 年级 6 班 学号: 11\n\n请计算下列各题 (含随机作答)',
  pos_list:[{rotate_rect:[518,178,255,857,90]}]},option:[],answer:[],type:'填空题'};
 assert.equal(readOcrText('```json\n'+JSON.stringify(fixture)+'\n```'),fixture.stem.text);
 assert.equal(readOcrText(JSON.stringify({stem:{text:'1. 计算'},option:[{text:'A. 12'}],answer:[{text:'59.58'}]})),'1. 计算\nA. 12\n59.58');
 assert.equal(readOcrText('59.58'), '59.58');
 assert.equal(readOcrText('0.50'), '0.50');
 assert.equal(readOcrText('(1) 59.58\n(2) 12'), '(1) 59.58\n(2) 12');
 assert.equal(readOcrText('{"text":"第一行\\n第二行"}'),'第一行\n第二行');
 assert.throws(()=>readOcrText('{"stem":{"text":"未完成'));
 assert.throws(()=>readOcrText('{"pos_list":[1,2],"type":"填空"}'));
});

test('stop immediately releases a pending request and ignores its late response',async()=>{
 const previousWindow=globalThis.window;
 const handlers=new Set();
 const sent=[];
 globalThis.window={chrome:{webview:{
  addEventListener:(_,handler)=>handlers.add(handler),
  removeEventListener:(_,handler)=>handlers.delete(handler),
  postMessage:payload=>sent.push(payload)
 }}};
 try {
  const controller=new AbortController();
  const pending=desktopRequest({action:'nextRegion'},65000,controller.signal);
  const receiveOld=[...handlers][0];
  controller.abort();
  assert.equal(handlers.size,0);
  await assert.rejects(pending,{name:'AbortError'});
  await assert.rejects(desktopRequest({action:'submitScore',score:'6'},65000,controller.signal),{name:'AbortError'});
  assert.equal(sent.length,1);
  const next=desktopRequest({action:'captureRegion'});
  receiveOld({data:{id:sent[0].id,content:'stale result'}});
  assert.equal(handlers.size,1);
  for(const handler of handlers) handler({data:{id:sent[1].id,content:'new result'}});
  assert.equal(await next,'new result');
 } finally {globalThis.window=previousWindow;}
});

test('OCR math markup is readable while incorrect student answers stay unchanged',()=>{
 const raw=String.raw`请计算下列各题（含随机作答）
1. \(18 + 9 = 29\)
2. \(26 - 7 = 19\)
3. \(42 + 4 = 56\)
4. \(14 + 2 = 16\)`;
 const expected='请计算下列各题（含随机作答）\n1. 18 + 9 = 29\n2. 26 - 7 = 19\n3. 42 + 4 = 56\n4. 14 + 2 = 16';
 assert.equal(readOcrText(raw),expected);
 assert.equal(readOcrText(JSON.stringify({text:raw})),expected);
 assert.equal(readOcrText(JSON.stringify(raw)),expected);
 assert.equal(readOcrText(String.raw`\[2 \times 3 = 7\]`),'2 × 3 = 7');
 assert.equal(readOcrText('$$18 + 9 = 29$$'),'18 + 9 = 29');
 assert.equal(readOcrText(String.raw`计算题\[1. 18 + 9 = 29\]\[2. 26 - 7 = 19\]`),'计算题\n1. 18 + 9 = 29\n\n2. 26 - 7 = 19');
 assert.equal(readOcrText('价格 $5；（1）-0.50'),'价格 $5；（1）-0.50');
});

test('rejects unsupported answers instead of taking question numbers as scores',()=>{
 for(const text of ['第1题答案59.58，无法评分','null','{"score":null}','true','[]','[6]','{}','-1','{"score":8,"maxScore":6}']) {
  assert.throws(()=>readGrade(text,1));
 }
 assert.throws(()=>readGrade('5.5',1));
});

test('normal grading entry sends reference and short numeric answers and accepts a direct score',async()=>{
 const previousWindow=globalThis.window;
 const handlers=new Set();
 let sent;
 globalThis.window={chrome:{webview:{
  addEventListener:(_,handler)=>handlers.add(handler),
  removeEventListener:(_,handler)=>handlers.delete(handler),
  postMessage:request=>{
   sent=JSON.parse(request.body);
   queueMicrotask(()=>{for(const handler of handlers)handler({data:{
    id:request.id,content:JSON.stringify({choices:[{message:{content:'2'}}]})
   }})});
  }
 }}};
 try {
  const task={subject:'数学',reference:'答案：2；本题2分',recognized:'2',maxScore:2};
  const result=await gradeText(task,defaultTeacher,{...initialSettings.grading,apiKey:'test'});
  assert.equal(result.score,2);
  assert.equal(JSON.parse(sent.messages[1].content).学生识别内容,'2');
  assert.equal(sent.enable_thinking,false);
  assert.equal(handlers.size,0);
 } finally {globalThis.window=previousWindow;}
});

test('OCR reads title and all nested text blocks through the normal request entry',async()=>{
 const previousWindow=globalThis.window;
 const handlers=new Set();
 const content={text:'一起完成',blocks:[
  {text:'周末，我们一起整理图书角。'},
  {text:'1. 图书角为什么变得整洁有序？',children:[{text:'答：因为大家分工合作，一起整理。'}]},
  {text:'2. 请用一个词语概括你的感受。',answer:[{text:'团结',pos_list:[{rotate_rect:[1,2,3,4,90]}]}]}
 ]};
 let requestBody;
 globalThis.window={chrome:{webview:{
  addEventListener:(_,handler)=>handlers.add(handler),
  removeEventListener:(_,handler)=>handlers.delete(handler),
  postMessage:request=>{
   requestBody=JSON.parse(request.body);
   queueMicrotask(()=>{for(const handler of handlers)handler({data:{
    id:request.id,content:JSON.stringify({choices:[{message:{content:JSON.stringify(content)}}]})
   }})});
  }
 }}};
 try {
  const text=await readAnswer('data:image/png;base64,test','test','https://example.com/v1');
  assert.equal(text,'一起完成\n周末，我们一起整理图书角。\n1. 图书角为什么变得整洁有序？\n答：因为大家分工合作，一起整理。\n2. 请用一个词语概括你的感受。\n团结');
  assert.equal(requestBody.model,'qwen-vl-ocr');
  assert.equal(requestBody.max_tokens,4096);
  assert.equal(requestBody.messages[0].content[0].min_pixels,262144);
  assert.equal(requestBody.messages[0].content[0].max_pixels,6422528);
 } finally {globalThis.window=previousWindow;}
});
