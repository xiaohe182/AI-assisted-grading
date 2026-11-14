import test from 'node:test';
import assert from 'node:assert/strict';
import {defaultTeacher, buildSnapshot, safeSettings, validateScore} from '../src/domain.js';

test('default teacher is universal without retained custom rules',()=>{
 assert.equal(defaultTeacher.name,'全能老师'); assert.equal(defaultTeacher.rules,'');
 assert.equal(buildSnapshot(defaultTeacher,{reference:'答案',maxScore:10}).teacher.name,'全能老师');
});
test('serialized settings never include credentials',()=>{
 const out=JSON.stringify(safeSettings({ocr:{provider:'qwen',model:'qwen3.8-flash',apiKey:'secret'},grading:{apiKey:'secret2'}}));
 assert.equal(out.includes('secret'),false);
});
test('decimal scores obey range and step instead of extracting first integer',()=>{
 assert.equal(validateScore('8.5',10,.5),8.5);
 for(const value of ['得分 8','11','-1','2.3',''])assert.throws(()=>validateScore(value,10,.5));
});
test('history takes a snapshot so later edits cannot rewrite its rubric',()=>{
 const teacher={...defaultTeacher,rules:'过程分'}; const task={reference:'原参考答案',maxScore:10};
 const snapshot=buildSnapshot(teacher,task); teacher.rules='不同规则'; task.reference='下一题';
 assert.equal(snapshot.teacher.rules,'过程分'); assert.equal(snapshot.task.reference,'原参考答案');
});
