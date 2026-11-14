import {safeSettings,defaultTeacher,providers} from './domain.js';
function validateData(data){
 const fail=()=>{throw Error('此目录中的阅卷台数据格式不兼容，请选择其他目录')};
 const object=v=>!!v&&typeof v==='object'&&!Array.isArray(v);
 const teacher=p=>{if(!object(p)||typeof p.id!=='string'||!p.id||typeof p.name!=='string'||!p.name||!Number.isFinite(Number(p.step))||Number(p.step)<=0)fail();for(const [key,example] of Object.entries(defaultTeacher)){if(key in p&&typeof example==='string'&&typeof p[key]!=='string')fail();}};
 if(!object(data)||data.version!==1||!Array.isArray(data.records)||!object(data.settings)||!Array.isArray(data.settings.profiles))fail();
 for(const key of ['ocr','grading']){const m=data.settings[key];if(!object(m)||typeof m.provider!=='string'||!providers[m.provider]||typeof m.url!=='string'||typeof m.model!=='string')fail();}
 data.settings.profiles.forEach(teacher);
 if(typeof data.settings.teacherId!=='string'||(data.settings.teacherId!=='universal'&&!data.settings.profiles.some(p=>p.id===data.settings.teacherId)))fail();
 if(new Set(data.settings.profiles.map(p=>p.id)).size!==data.settings.profiles.length)fail();
 for(const row of data.records){if(!object(row)||typeof row.id!=='string'||!Number.isFinite(Date.parse(row.time))||typeof row.subject!=='string'||typeof row.status!=='string'||typeof row.reason!=='string'||!object(row.task))fail();teacher(row.teacher);for(const key of ['recognized','reference','rubric'])if(typeof row.task[key]!=='string')fail();if((row.task.maxScore!==null&&(!Number.isFinite(Number(row.task.maxScore))||Number(row.task.maxScore)<=0))||typeof row.score!=='number'||!Number.isFinite(row.score)||row.score<0||(row.task.maxScore!==null&&row.score>Number(row.task.maxScore)))fail();}
 return data;
}
export async function loadFolder(handle){
 let text;try{const file=await handle.getFileHandle('阅卷台数据.json');text=await(await file.getFile()).text();}catch(e){if(e.name==='NotFoundError')return {settings:null,records:[]};throw e;}
 const data=JSON.parse(text);
 return validateData(data);
}
export async function writeFolder(handle,settings,records){
 if(!handle)throw Error('请先在“本地保存”中选择文件夹');
 // Preserve legacy data when updating teacher settings.
 if(records===undefined) records=(await loadFolder(handle)).records;
 const file=await handle.getFileHandle('阅卷台数据.json',{create:true});
 const stream=await file.createWritable();
 try{await stream.write(JSON.stringify({version:1,settings:safeSettings(settings),records},null,2));await stream.close();}catch(e){try{await stream.abort();}catch{}throw e;}
}
export function downloadJson(name,data){const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));const link=document.createElement('a');link.href=url;link.download=name;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
