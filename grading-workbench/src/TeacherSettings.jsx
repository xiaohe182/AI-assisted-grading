import {useState} from 'react';
import {Plus,Info,FloppyDisk,FileArrowUp} from '@phosphor-icons/react';
import {Button,Input,Select,Multiline,Notice,Modal} from './ui.jsx';
import {defaultTeacher} from './domain.js';

const sections=['基本教学信息','批改范围','评分要求','每题评分依据','不确定情况处理','结果展示','平台与填分','样卷与验收'];
export function TeacherSettings({settings,draft,setDraft,onSave,notify,storageNeeded,onOpenStorage}){
 const [section,setSection]=useState(2);const [naming,setNaming]=useState(false);const [name,setName]=useState('');const [saving,setSaving]=useState(false);
 const update=(key,value)=>setDraft({...draft,[key]:value});
 const field=(label,key,placeholder='')=><Input label={label} value={draft[key]??''} placeholder={placeholder} onChange={e=>update(key,e.target.value)}/>;
 const area=(label,key,placeholder='',rows=4)=><Multiline label={label} value={draft[key]??''} placeholder={placeholder} rows={rows} onChange={e=>update(key,e.target.value)}/>;
 const select=(label,key,options)=><Select label={label} value={draft[key]} options={options} onChange={e=>update(key,e.target.value)}/>;
 const selected=settings.profiles.find(p=>p.id===settings.teacherId)||defaultTeacher;
 const dirty=JSON.stringify(draft)!==JSON.stringify(selected);
 async function save(){if(!name.trim()){notify('请输入老师配置名称');return;}setSaving(true);const ok=await onSave({...draft,id:draft.id==='universal'?crypto.randomUUID():draft.id,name:name.trim()});setSaving(false);if(ok)setNaming(false);}
 return <><div className="profile-bar"><Select label="当前老师配置" value={settings.teacherId} disabled={dirty} options={[[defaultTeacher.id,'全能老师（默认）'],...settings.profiles.map(p=>[p.id,p.name])]} onChange={async e=>{const p=settings.profiles.find(p=>p.id===e.target.value)||defaultTeacher;const ok=await onSave(null,p.id);if(ok)setDraft(structuredClone(p))}}/><Button onClick={()=>{setDraft({...structuredClone(defaultTeacher),id:'universal',name:''});setName('')}}><Plus size={17}/>新建老师配置</Button>{dirty&&<span className="draft-tag">有未保存修改</span>}</div>
 <div className="teacher-layout"><nav className="section-nav" aria-label="老师定制化章节">{sections.map((s,i)=><button key={s} aria-current={section===i?'page':undefined} className={section===i?'active':''} onClick={()=>setSection(i)}>{s}</button>)}</nav><div className="teacher-form"><div className="section-title"><h2>{sections[section]}</h2></div>
 <form noValidate onSubmit={e=>e.preventDefault()}>
 {section===0&&<><div className="two-fields">{field('任教学科','subject','例如：数学，也可填写多个科目')}{field('任教年级','grade','例如：高二')}</div>{field('教材版本','edition','例如：人教版')}{area('主要批改题型','types','例如：填空、计算、阅读理解、翻译、作文')}</>}
 {section===1&&<>{select('批改范围','scope',['单题','单题多个小问','整张试卷（需进一步适配）'])}{select('批改答案还是过程','process',['按题目要求','只判断最终答案','同时批改答题过程'])}{select('学生作答形式','writing',['手写','打印','手写与打印混合'])}{area('特殊内容','special','公式、图形、表格、化学式等，请说明识别要求')}</>}
 {section===2&&<>{select('评分方式','method',['按当前题目评分标准','按得分点给分','整体评价给分'])}{select('同义表达','synonym',['按题目要求','意思正确即可给分','必须符合指定术语'])}{select('过程分','stepCredit',['按题目要求','步骤正确给予过程分','只看最终答案'])}<div className="two-fields">{select('最小计分单位','step',[['1','1 分'],['0.5','0.5 分'],['0.25','0.25 分'],['0.1','0.1 分']])}<div className="field"><label>小数分</label><div className="readonly-value">{Number(draft.step)<1?'按所选计分单位允许小数':'仅整数分'}</div></div></div>{area('扣分与特殊规则','rules','例如：单位遗漏扣 0.5 分；同一错误不重复扣分。\n未填写的规则不额外扣分。')}</>}
 {section===3&&<>{area('每题需要提供的材料','taskRequirements','题目全文、参考答案、总分及小问满分、得分点分值、扣分规则、可接受的其他解法',6)}{area('不同题型的补充说明','taskNotes','例如：作文使用分档标准；计算题需注明过程分')}</>}
 {section===4&&<>{select('识别不清或无法确定时','uncertain',['显示建议与疑问，由老师确认','暂停批改，交给老师确认','其他（填写补充规则）'])}{area('空白答案','blank','仅在确认加载完成后判断白卷')}{area('划掉重写、多种答案并存','rewritten','请说明采用哪个答案，以及无法确定时的处理方式')}</>}
 {section===5&&<>{area('希望显示的评分结果','display','总分、简短扣分原因、每个得分点得分、原始识别文字',5)}</>}
 {section===6&&<>{field('阅卷平台名称','platform')}{field('平台网址','url')}{field('电脑系统与浏览器','browser')}{select('答案和输入框位置','position',['固定区域','随页面变化','需要观察平台演示'])}{area('正常批改一份的操作流程','platformNotes','描述输入分数、回车、切换下一份的过程，以及可能出现的加载提示。')}</>}
 {section===7&&<>{area('代表性样卷与给分说明','acceptance','请列出满分、部分正确、错误、白卷、其他解法和争议样卷，并注明老师分数及扣分原因。',5)}{area('验收标准','tolerance','允许的分数偏差、哪些情况必须转人工确认、对比样卷的验收范围。')}</>}
 </form></div></div><div className="settings-footer"><div><Button onClick={()=>{setDraft(structuredClone(selected));notify('已恢复当前保存的老师配置')}}>取消修改</Button><Button variant="primary" onClick={()=>{setName(draft.id==='universal'?'':draft.name);setNaming(true)}}><FloppyDisk size={17}/>保存为老师配置</Button></div></div>
 {naming&&<Modal title="保存老师配置" onClose={()=>setNaming(false)}><Input label="老师配置名称" disabled={saving} autoFocus value={name} onChange={e=>{setName(e.target.value)}} placeholder="例如：高二数学 · 王老师"/><div className="modal-persistence-error">{storageNeeded&&<button className="text-link" onClick={()=>{setNaming(false);onOpenStorage()}}>选择本地保存目录</button>}</div><div className="modal-actions"><Button disabled={saving} onClick={()=>setNaming(false)}>取消</Button><Button variant="primary" disabled={saving} onClick={save}>{saving?'正在保存…':'保存并选用'}</Button></div></Modal>}</>
}
