import test from 'node:test';
import assert from 'node:assert/strict';
import {identifyMaximum} from '../src/maximum.js';
import {gradeText, readGrade} from '../src/grading.js';
import {defaultTeacher} from '../src/domain.js';
const model={model:'test',provider:'custom',url:'https://example.test',apiKey:'test'};
async function bridge(replies,work){
 const old=globalThis.window, handlers=new Set(), requests=[];
 globalThis.window={chrome:{webview:{addEventListener:(_,h)=>handlers.add(h),removeEventListener:(_,h)=>handlers.delete(h),postMessage:r=>{
  requests.push(JSON.parse(r.body)); const content=replies.shift();
  queueMicrotask(()=>{for(const h of handlers)h({data:{id:r.id,content:JSON.stringify({choices:[{message:{content}}]})}})});
 }}}};
 try{return await work(requests)}finally{globalThis.window=old}
}
test('even explicit totals are interpreted by the model',async()=>{
 await bridge(['6'],async requests=>{
  assert.equal(await identifyMaximum('答案59.58；满分6分；扣1分',model),6);
  assert.equal(requests.length,1);
 });
});
test('environment rubric with trailing overall score is sent intact to the model',async()=>{
 const reference='标准答案：①树立节约资源、保护环境的意识。②落实垃圾分类，减少一次性用品。③节水节电，养成节约习惯。④积极参与环保宣传和社区环保实践。\n评分要点：环保意识3分；垃圾分类或减少一次性用品3分；节水节电2分；参与环保宣传或实践2分。同义表达可得分，各点不重复计分，9分。';
 await bridge(['9分'],async requests=>{
  assert.equal(await identifyMaximum(reference,model),9);
  assert.equal(requests[0].messages[1].content,reference);
  assert.doesNotMatch(requests[0].messages[0].content,/返回9|返回10|末尾|优先于分项/);
 });
});
test('complex maximum extraction sends only reference, not student answer',async()=>{
 await bridge(['6'],async requests=>{
  assert.equal(await identifyMaximum('三个得分点，每点2分',model),6);
  assert.equal(requests.length,1);
  assert.equal(requests[0].messages[1].content,'三个得分点，每点2分');
 });
});
test('user maximum overrides model maximum and bounds score',()=>{
 assert.equal(readGrade('{"score":4,"maxScore":100}',1,6).maxScore,6);
 assert.throws(()=>readGrade('7',1,6));
});
test('null is retried without old interruption and uses confirmed maximum',async()=>{
 await bridge(['null','4'],async requests=>{
  const result=await gradeText({reference:'满分6分',recognized:'学生答案',maxScore:5},defaultTeacher,model);
  assert.equal(result.score,4);assert.equal(result.maxScore,5);assert.equal(requests.length,2);
  assert.equal(JSON.parse(requests[0].messages[1].content).评分标准满分,5);
 });
});
test('repeated null remains unscored rather than fabricated or thrown',async()=>{
 await bridge(['null','null'],async requests=>{
  assert.equal(await gradeText({reference:'满分6分',recognized:'学生答案',maxScore:6},defaultTeacher,model),null);
  assert.equal(requests.length,2);
 });
});

test('out of range grade is retried against the rubric maximum',async()=>{
 await bridge(['9','6'],async()=>{
  const result=await gradeText({reference:'满分6分',recognized:'答案',maxScore:6},defaultTeacher,model);
  assert.equal(result.score,6);
 });
});
test('stop cancels before the retry is sent',async()=>{
 const controller=new AbortController();controller.abort();
 await bridge([],async requests=>{
  await assert.rejects(gradeText({reference:'满分6分',recognized:'答案',maxScore:6},defaultTeacher,model,controller.signal),{name:'AbortError'});
  assert.equal(requests.length,0);
 });
});

 test('grading requires a known positive maximum before any request',async()=>{
  for(const maxScore of [undefined,null,0,-1,NaN,Infinity]) {
   await bridge([],async requests=>{
    await assert.rejects(gradeText({reference:'评分标准',recognized:'答案',maxScore},defaultTeacher,model),/满分/);
    assert.equal(requests.length,0);
   });
  }
 });
 test('both endpoints are valid and scores outside the interval are rejected',()=>{
  assert.equal(readGrade('0',1,9).score,0);
  assert.equal(readGrade('9',1,9).score,9);
  assert.throws(()=>readGrade('-1',1,9));
  assert.throws(()=>readGrade('10',1,9));
 });

test('invalid or missing credentials are reported before maximum requests',async()=>{
 for(const apiKey of ['', '中文key', 'key with spaces', 'key\nvalue']) {
  await bridge([],async requests=>{
   await assert.rejects(identifyMaximum('总分10分',{...model,apiKey}),/API Key/);
   assert.equal(requests.length,0);
  });
 }
});
test('malformed model output is a format error, not an unknown maximum',async()=>{
 await bridge(['模型响应异常'],async()=>{
  await assert.rejects(identifyMaximum('总分10分',model),/格式/);
 });
 await bridge(['null'],async()=>{
  assert.equal(await identifyMaximum('没有给出分值',model),null);
 });
});
