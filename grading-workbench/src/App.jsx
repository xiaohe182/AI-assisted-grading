import {rubricExamples} from './rubricExamples.js';
import {gradeText,desktopRequest,readAnswer} from './grading.js';
import { Header } from './admin/header';
import { Main } from './admin/main';
import { Card, CardHeader, CardContent } from './admin/card';
import {SidebarMenu,SidebarMenuItem} from './SidebarMenu.jsx';
import {useState,useEffect,useRef} from 'react';
import {BookOpen,NotePencil,GearSix,Crop,ImageSquare,Check,CheckCircle,Info,ArrowRight,Stop,FolderOpen,Eye,EyeSlash,FloppyDisk,Lightning,SpinnerGap,SidebarSimple,CaretRight} from '@phosphor-icons/react';
import '@fontsource/inter/latin-400.css';
import '@fontsource/inter/latin-500.css';
import '@fontsource/inter/latin-600.css';
import {Button,Input,Select,Multiline,SectionTitle,Notice,Modal} from './ui.jsx';
import {defaultTeacher,initialSettings,providers,validateScore} from './domain.js';
import {loadFolder,writeFolder} from './storage.js';
import {TeacherSettings} from './TeacherSettings.jsx';

export function App({initialConfig=initialSettings,startupError=''}){
 const [page,setPage]=useState(location.hash.slice(1)==='settings'?location.hash.slice(1):'workbench');
 const [settings,setSettings]=useState(structuredClone(initialConfig));const [models,setModels]=useState(structuredClone(initialConfig));const [teacherDraft,setTeacherDraft]=useState(structuredClone(defaultTeacher));
 const [compact,setCompact]=useState(false);
 const [settingsTab,setSettingsTab]=useState('models');const [directory,setDirectory]=useState(null);const [notice,setNotice]=useState(startupError?{text:startupError}:'');const [saving,setSaving]=useState(false);const [folderCandidate,setFolderCandidate]=useState(null);
 const [task,setTask]=useState({subject:'通用',recognized:'',reference:'',rubric:'',maxScore:null});
 const [image,setImage]=useState('');
 const [score,setScore]=useState('');
 
 const saveLock=useRef(false),fileInput=useRef(null),imageObject=useRef(null),messageRef=useRef(null);
 const [grading,setGrading]=useState(false);
 const [suggestion,setSuggestion]=useState(null);
 const requestVersion=useRef(0), requestBusy=useRef(false);
 const activeRequest=useRef(null), stopBarrier=useRef(Promise.resolve());
 const [region,setRegion]=useState(false);
 const [screenAnswer,setScreenAnswer]=useState(false),[automatic,setAutomatic]=useState(true);
 const [,setProgress]=useState('');
 const teacher=settings.profiles.find(p=>p.id===settings.teacherId)||defaultTeacher;
 function notify(text){setNotice({text})}
 function navigate(to){if(requestBusy.current){stopGrading()}location.hash=to;setPage(to)}
 useEffect(()=>{const changed=()=>{const route=location.hash.slice(1);setPage(route==='settings'?route:'workbench')};window.addEventListener('hashchange',changed);return()=>window.removeEventListener('hashchange',changed)},[]);
 useEffect(()=>{document.title=({workbench:'阅卷工作台',settings:'设置'})[page]+' · 阅卷台'},[page]);
 useEffect(()=>{if(notice){messageRef.current?.showPopover?.();const id=setTimeout(()=>setNotice(''),5500);return()=>clearTimeout(id)}},[notice]);
 useEffect(()=>()=>{if(imageObject.current)URL.revokeObjectURL(imageObject.current)},[]);
 function clearGrade(){requestVersion.current++;setScore('');setSuggestion(null)}
 function updateTask(key,value){clearGrade();setTask(t=>({...t,[key]:value}))}
 useEffect(()=>{clearGrade()},[teacher,settings.grading]);
 useEffect(()=>{setTask(t=>({...t,subject:settings.subject||'通用'}))},[settings.subject]);
 function ocrKey(){
   return settings.ocr.apiKey || (settings.grading.provider==='qwen' && new URL(settings.grading.url).origin===new URL(initialSettings.ocr.url).origin ? settings.grading.apiKey : '');
 }
 async function runWork(work) {
   if(requestBusy.current || saveLock.current)return;
   const controller=new AbortController();
   activeRequest.current=controller;
   requestBusy.current=true;setGrading(true);
   const version=++requestVersion.current;
   try {await stopBarrier.current;controller.signal.throwIfAborted();await work(version,controller.signal)}
   catch(error){if(version===requestVersion.current){notify(error.message)}}
   finally{if(activeRequest.current===controller){activeRequest.current=null;requestBusy.current=false;setGrading(false);setProgress('')}}
 }
 async function recognize(action,version,signal) {
   setProgress(action==='selectRegion'?'选区中…':'读取答卷…');
   const picture=await desktopRequest({action},action==='selectRegion'?600000:65000,signal);
   if(version!==requestVersion.current || picture==='cancel')return;
   if(action==='selectRegion'){setRegion(true);clearGrade();setScreenAnswer(false);setImage('');setTask(t=>({...t,recognized:''}));return}
   setSuggestion(null);setImage(picture);setScreenAnswer(false);
   setTask(t=>({...t,recognized:''}));setProgress('识别中…');
   const recognized=await readAnswer(picture,ocrKey(),initialSettings.ocr.url,signal);
   if(version!==requestVersion.current)return;
   setTask(t=>({...t,recognized}));setScreenAnswer(true);
   return recognized;
 }
 function stopGrading(){
   requestVersion.current++;
   activeRequest.current?.abort();
   activeRequest.current=null;
   requestBusy.current=false;
   setGrading(false);setProgress('');setSuggestion(null);setScreenAnswer(false);
   stopBarrier.current=desktopRequest({action:'stop'});
   stopBarrier.current.catch(error=>notify(error.message));
   notify('阅卷已停止');
 }
 async function gradeAndContinue(answer,version,submitAutomatically,signal) {
   while(version===requestVersion.current){
     const paper={...task,recognized:answer};
     setProgress('评分中…');
     const result=await gradeText(paper,teacher,settings.grading,signal);
     if(version!==requestVersion.current)return;
     setSuggestion(result);setScore(String(result.score));
     if(!submitAutomatically){
       notify('评分完成');
       return;
     }
     if(!await submitPaper(result.score,version,signal) || version!==requestVersion.current)return;
     answer=await recognize('nextRegion',version,signal);
     if(!answer)return;
   }
 }
 async function submitPaper(value,version,signal){
   if(version!==requestVersion.current)return false;
   signal.throwIfAborted();
   setProgress('填分中…');
   await desktopRequest({action:'submitScore',score:String(value)},65000,signal);
   if(version!==requestVersion.current)return false;
   setScreenAnswer(false);setSuggestion(null);
   return version===requestVersion.current;
 }
 function requestGrade(){
   runWork(async (version,signal)=>{
     if(!task.reference.trim())throw Error('请填写参考答案与评分标准');
     if(!region && !task.recognized.trim())throw Error('请先框选 OCR 选区');
     const answer=region ? await recognize('captureRegion',version,signal) : task.recognized;
     if(!answer || version!==requestVersion.current)return;
     await gradeAndContinue(answer,version,automatic&&region,signal);
   });
 }
 function confirmEntry(){
   runWork(async (version,signal)=>{
     const value=validateScore(score,suggestion?.maxScore??null,teacher.step);
     if(!await submitPaper(value,version,signal))return;
     const answer=await recognize('nextRegion',version,signal);
     if(answer)await gradeAndContinue(answer,version,automatic,signal);
   });
 }
 async function persist(nextSettings=settings){if(saveLock.current){notify('已有保存操作进行中，请稍候');return false;}if(!directory){notify('请先在“本地保存”中选择文件夹，再保存老师配置。');return false;}saveLock.current=true;setSaving(true);try{await writeFolder(directory,nextSettings);setSettings(nextSettings);notify('已保存到本地目录');return true}catch(error){notify('保存失败：'+error.message+'。草稿仍保留，请检查目录权限后重试。');return false}finally{saveLock.current=false;setSaving(false)}}

 async function saveModels(nextSettings) {
   if(saveLock.current)return;
   saveLock.current=true;setSaving(true);
   try {
     await desktopRequest({action:'saveModels',body:JSON.stringify({
       subject:nextSettings.subject,ocr:nextSettings.ocr,grading:nextSettings.grading
     })});
     setSettings(nextSettings);setModels(structuredClone(nextSettings));
     notify('模型配置已保存');
   } catch(error) {notify('模型配置保存失败：'+error.message);}
   finally {saveLock.current=false;setSaving(false);}
 }
 async function chooseFolder(){if(saveLock.current){notify('请等待当前保存完成再更改目录');return;}if(!window.showDirectoryPicker){notify('此浏览器不支持选择本地目录。');return}saveLock.current=true;setSaving(true);try{const handle=await window.showDirectoryPicker({mode:'readwrite',id:'grading-data'});const data=await loadFolder(handle);if(data.settings){setFolderCandidate({handle,data});return;}await writeFolder(handle,settings);setDirectory(handle);notify('已选择目录，后续保存会写入此文件夹')}catch(error){if(error.name!=='AbortError')notify('无法使用此目录：'+error.message)}finally{saveLock.current=false;setSaving(false)}}
 function activateFolder(){if(saveLock.current){notify('请等待当前保存完成');return;}const {handle,data}=folderCandidate;const loaded={...structuredClone(initialSettings),...data.settings,ocr:{...initialSettings.ocr},grading:{...initialSettings.grading,...data.settings.grading,enable_thinking:false,apiKey:data.settings.grading.url===settings.grading.url?settings.grading.apiKey:''}};loaded.ocr.apiKey=settings.ocr.apiKey;setDirectory(handle);setSettings(loaded);setModels(structuredClone(loaded));setTeacherDraft(structuredClone(loaded.profiles.find(p=>p.id===loaded.teacherId)||defaultTeacher));setFolderCandidate(null);notify('已载入所选文件夹中的本地配置')}
 async function saveTeacher(profile,selectedId){const next=profile?{...settings,teacherId:profile.id,profiles:[...settings.profiles.filter(p=>p.id!==profile.id),profile]}:{...settings,teacherId:selectedId};const ok=await persist(next);if(ok&&profile)setTeacherDraft(structuredClone(profile));return ok}
 async function importImage(e){
   const file=e.target.files?.[0];e.target.value='';if(!file)return;
   if(!['image/png','image/jpeg','image/webp'].includes(file.type)||file.size>12*1024*1024){notify('请选择 12MB 以内的答卷图片');return}
   await runWork(async (version,signal)=>{
     const url=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(reader.result);reader.onerror=()=>reject(Error('图片读取失败'));reader.readAsDataURL(file)});
     if(version!==requestVersion.current)return;
     setScreenAnswer(false);setImage(url);setScore('');setSuggestion(null);setTask(t=>({...t,recognized:''}));
     setProgress('识别中…');const text=await readAnswer(url,ocrKey(),initialSettings.ocr.url,signal);
     if(version===requestVersion.current)setTask(t=>({...t,recognized:text}));
   });
 }
 return <div className={compact?"app-shell compact":"app-shell"}><aside className="sidebar"><a className="brand" href="#workbench" aria-label="阅卷台首页"><span className="brand-mark"><BookOpen size={19}/></span><span>阅卷台</span></a><div className="nav-caption">工作空间</div><nav aria-label="主导航"><SidebarMenu>{[['workbench','阅卷工作台',NotePencil],['settings','设置',GearSix]].map(([id,label,Icon])=><SidebarMenuItem key={id}><a href={'#'+id} key={id} aria-current={page===id?'page':undefined} className={page===id?'active':''} aria-label={label} onClick={()=>navigate(id)}><Icon size={21}/><span>{label}</span></a></SidebarMenuItem>)}</SidebarMenu></nav></aside>
 <div className="admin-inset">
 <Header className="admin-header" collapsed={compact} onToggle={()=>setCompact(!compact)}>
   <span>阅卷台</span><CaretRight size={14}/><span aria-current="page">{({workbench:'阅卷工作台',settings:'设置'})[page]}</span>
 </Header>
 <Main fluid className={page==='workbench'?'main-content workbench-page':'main-content settings-page'}>
 {page==='workbench'&&<>
 <input ref={fileInput} className="sr-only" type="file" accept="image/png,image/jpeg,image/webp" onChange={importImage}/>
 <div className="workbench-grid">
 <Card className="rubric-column"><CardHeader><SectionTitle>参考答案与评分标准</SectionTitle></CardHeader><CardContent className="rubric-content"><textarea aria-label="参考答案与评分标准" className="rubric-text resize-none" rows={6} placeholder={rubricExamples[settings.subject] || rubricExamples.通用} disabled={grading} value={task.reference} onChange={e=>updateTask('reference',e.target.value)}/></CardContent></Card>
 <Card className="student-column"><CardHeader><SectionTitle aside={<div className="ocr-actions">
 {region&&<Button disabled={grading||saving} onClick={()=>runWork((v,signal)=>recognize('captureRegion',v,signal))}>重新识别</Button>}
 <Button variant="icon quiet" disabled={grading||saving} aria-label="选择答卷图片" title="选择答卷图片" onClick={()=>fileInput.current.click()}><ImageSquare size={18}/></Button>
 <Button disabled={grading||saving} onClick={()=>runWork((v,signal)=>recognize('selectRegion',v,signal))}><Crop size={16}/>{region?'重选 OCR 选区':'OCR 选区'}</Button></div>}>学生答案</SectionTitle></CardHeader><CardContent className="student-content">
 <div className="answer-original"><span>答卷原图</span><div className="answer-image-frame">{image?<img src={image} alt="学生答卷"/>:<span className="image-placeholder">识别或选择图片后显示答卷原图</span>}</div></div>
 <Multiline label="OCR 识别结果" placeholder="识别后的学生答案显示在这里" rows={4} readOnly value={task.recognized}/>
 </CardContent></Card></div>
 <footer className="workbench-footer">
 <label className="auto-check"><input type="checkbox" checked={automatic} disabled={grading} onChange={e=>setAutomatic(e.target.checked)}/>自动填分</label>
 <output className="corner-score">得分　{score||'—'}</output>
 <div className="footer-actions">
 {!automatic&&screenAnswer&&suggestion&&<Button disabled={grading||saving} onClick={confirmEntry}>确认填入并回车</Button>}
 <Button variant="primary" disabled={grading||saving} onClick={requestGrade}>AI 阅卷</Button>
 <Button disabled={!grading&&!suggestion} onClick={stopGrading}>停止阅卷</Button></div></footer></>}
 {page==='settings'&&<><nav className="settings-tabs" aria-label="设置分类">{[['models','模型与连接'],['storage','本地保存'],['teacher','老师定制化']].map(([id,label])=><button key={id} aria-current={settingsTab===id?'page':undefined} className={settingsTab===id?'active':''} onClick={()=>{setSettingsTab(id)}}>{label}</button>)}</nav>

 {settingsTab==='models'&&<section className="settings-panel"><div className="settings-intro"><Select label="科目" value={models.subject||'通用'} options={['通用','语文','数学','英语','物理','化学','生物','历史','地理','政治','其他科目']} onChange={e=>setModels({...models,subject:e.target.value})}/><div className="ocr-model-summary"><span>识字模型</span><span>qwen-vl-ocr</span></div></div><div className="grading-config"><ModelForm value={models.grading} onChange={grading=>setModels({...models,grading})} notify={notify}/></div>{(models.grading.provider!=='qwen'||models.grading.url!==initialSettings.ocr.url)&&<Input type="password" autoComplete="off" label="百炼 OCR API Key" value={models.ocr.apiKey||''} onChange={e=>setModels({...models,ocr:{...models.ocr,apiKey:e.target.value}})}/>}<div className="settings-footer"><div><Button onClick={()=>{setModels(structuredClone(settings));notify('已恢复保存的模型配置')}}>取消修改</Button><Button variant="primary" disabled={saving} onClick={async()=>{try{const u=new URL(models.grading.url);if(!['https:','http:'].includes(u.protocol)||!models.grading.model.trim())throw Error()}catch{notify('请检查服务地址和模型名称，输入完整的 HTTP / HTTPS 地址。');return}await saveModels({...settings,subject:models.subject||'通用',ocr:{...initialSettings.ocr,apiKey:models.ocr.apiKey||''},grading:{...models.grading,enable_thinking:false}})}}><FloppyDisk size={17}/>{saving?'正在保存…':'保存模型配置'}</Button></div></div></section>}
 {settingsTab==='storage'&&<><div className="storage-section"><FolderOpen size={24}/><div className="storage-folder"><h3>{directory?directory.name:'尚未选择保存目录'}</h3></div><Button variant="primary" onClick={chooseFolder}><FolderOpen size={18}/>{directory?'更改保存目录':'选择保存目录'}</Button></div></>}
 {settingsTab==='teacher'&&<TeacherSettings settings={settings} draft={teacherDraft} setDraft={setTeacherDraft} onSave={saveTeacher} notify={notify} storageNeeded={!directory} onOpenStorage={()=>setSettingsTab('storage')}/>}
 </>}
 </Main></div>{notice&&<div className="toast" popover="manual" ref={messageRef} role="status"><Info size={18}/>{notice.text}</div>}
 {folderCandidate&&<Modal title="载入本地数据" onClose={()=>setFolderCandidate(null)}><p>文件夹“{folderCandidate.handle.name}”中已有阅卷台配置。</p><p>载入后会替换当前草稿。</p><div className="modal-actions"><Button onClick={()=>setFolderCandidate(null)}>取消</Button><Button variant="primary" onClick={activateFolder}>载入此文件夹</Button></div></Modal>}
 </div>
}

function ModelForm({value,onChange,notify}) {
 const [visible,setVisible]=useState(false);
 return <section className="model-form">
  <div className="model-fields">
   <Select label="服务商" value={value.provider} options={Object.entries(providers).map(([key,p])=>[key,p.name])} onChange={e=>{
    const provider=providers[e.target.value];
    onChange({...value,provider:e.target.value,url:provider.url,model:provider.models[0]||'',apiKey:''});
   }}/>
   <Input label="评分模型" value={value.model} onChange={e=>onChange({...value,model:e.target.value})} placeholder="填写模型 ID"/>
   <div className="model-wide"><Input label="接口地址" value={value.url} onChange={e=>onChange({...value,url:e.target.value})} placeholder="https://…/v1"/></div>
   <div className="secret-field model-wide">
    <Input label="API Key" type={visible?'text':'password'} autoComplete="off" value={value.apiKey} onChange={e=>onChange({...value,apiKey:e.target.value})} placeholder="填写 API Key"/>
    <Button variant="icon quiet" aria-label={visible?'隐藏 API Key':'显示 API Key'} aria-pressed={visible} onClick={()=>setVisible(!visible)}>
     {visible?<EyeSlash size={19}/>:<Eye size={19}/>}
    </Button>
   </div>
  </div>
  <button className="text-link" onClick={()=>{
   try {
    const url=new URL(value.url);
    if(!['http:','https:'].includes(url.protocol)||!value.model.trim())throw Error();
    notify('配置格式正确');
   } catch {notify('请填写完整的 HTTP / HTTPS 接口地址和模型名称')}
  }}><Lightning size={16}/>检查配置格式</button>
 </section>;
}
