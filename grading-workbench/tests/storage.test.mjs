import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,readFile,writeFile,rename,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {loadFolder,writeFolder} from '../src/storage.js';
import {initialSettings,defaultTeacher} from '../src/domain.js';

test('saving teacher settings preserves legacy records after feature removal',async()=>{
 const record={id:'legacy',time:'2026-11-14T00:00:00Z',subject:'数学',
  status:'模型评分',reason:'正确',score:2,teacher:defaultTeacher,
  task:{recognized:'答案',reference:'参考',rubric:'每条2分',maxScore:2}};
 let data={version:1,settings:structuredClone(initialSettings),records:[record]};
 const handle={getFileHandle:async()=>({
  getFile:async()=>({text:async()=>JSON.stringify(data)}),
  createWritable:async()=>({write:async text=>{data=JSON.parse(text)},close:async()=>{},abort:async()=>{}})
 })};
 await writeFolder(handle,{...initialSettings,subject:'语文'});
 assert.deepEqual(data.records,[record]);
 assert.equal(data.settings.subject,'语文');
});

test('selected folder receives real JSON data without model secrets',async()=>{
 const path=await mkdtemp(join(tmpdir(),'grading-test-'));
 const handle={getFileHandle:async name=>({getFile:async()=>({text:()=>readFile(join(path,name),'utf8')}),createWritable:async()=>({write:data=>writeFile(join(path,name+'.pending'),data),close:()=>rename(join(path,name+'.pending'),join(path,name)),abort:()=>rm(join(path,name+'.pending'),{force:true})})})};
 try{const settings=structuredClone(initialSettings);settings.ocr.apiKey='DO-NOT-SAVE';await writeFolder(handle,settings,[]);const data=await loadFolder(handle);assert.equal(data.version,1);assert.deepEqual(data.records,[]);assert.equal(JSON.stringify(data).includes('DO-NOT-SAVE'),false)}finally{await rm(path,{recursive:true,force:true})}
});
test('unselected directory rejects instead of announcing success',async()=>{await assert.rejects(writeFolder(null,initialSettings,[]),/选择文件夹/)});
test('failed write aborts and propagates error without closing partial data',async()=>{let aborted=false,closed=false;const handle={getFileHandle:async()=>({createWritable:async()=>({write:async()=>{throw Error('disk full')},close:async()=>{closed=true},abort:async()=>{aborted=true}})})};await assert.rejects(writeFolder(handle,initialSettings,[]),/disk full/);assert.equal(aborted,true);assert.equal(closed,false)});
test('incompatible data is rejected before activating directory',async()=>{const handle={getFileHandle:async()=>({getFile:async()=>({text:async()=>JSON.stringify({version:9,records:[],settings:{profiles:[]}})})})};await assert.rejects(loadFolder(handle),/不兼容/)});
test('malformed record and null teacher cannot activate a folder',async()=>{
 for(const patch of [{records:[{}]},{settings:{...initialSettings,profiles:[null]}}]){
  const data={version:1,settings:initialSettings,records:[],...patch};
  const handle={getFileHandle:async()=>({getFile:async()=>({text:async()=>JSON.stringify(data)})})};
  await assert.rejects(loadFolder(handle),/不兼容/);
 }
});
