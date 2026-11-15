async page => {
 await page.addInitScript(()=>{
  const handlers=new Set();window.scanTest={};
  const send=(id,content)=>{for(const h of handlers)h({data:{id,content}})};
  const reply=(r,content)=>queueMicrotask(()=>send(r.id,content));
  window.scanTest.finish=(name,content)=>send(window.scanTest[name],content);
  window.chrome=window.chrome||{};
  window.chrome.webview={addEventListener:(_,h)=>handlers.add(h),removeEventListener:(_,h)=>handlers.delete(h),postMessage:r=>{
   if(r.action==='loadModels')return reply(r,JSON.stringify({ocr:{apiKey:'test'},grading:{provider:'custom',model:'mock',url:'https://example.test',apiKey:'test'}}));
   if(r.action==='selectRegion')return reply(r,'selected');
   if(r.action==='captureRegion')return reply(r,'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1kAAAAASUVORK5CYII=');
   if(r.action==='submitScore'){window.scanTest.submit=r.id;return;}
   if(r.action==='nextRegion'){window.scanTest.next=r.id;return;}
   if(r.action==='startScan'){window.scanTest.scanning=true;return reply(r,'ok');}
   if(r.action==='stopScan'){window.scanTest.scanning=false;return reply(r,'ok');}
   if(r.action==='closeRegion'){window.scanTest.closed=true;return reply(r,'ok');}
   if(r.action==='stop')return reply(r,'ok');
   const body=JSON.parse(r.body);
   if(body.model==='qwen-vl-ocr'){window.scanTest.ocr=r.id;return;}
   reply(r,JSON.stringify({choices:[{message:{content:body.messages[0].content.includes('判断本次评分的满分')?'10':'8'}}]}));
  }};
 });
 await page.reload();
 await page.getByRole('textbox',{name:'参考答案与评分标准',exact:true}).fill('参考答案：节约资源。评分标准：总分10分。');
 await page.waitForFunction(()=>document.querySelector('#rubric-maximum').value==='10');
 await page.getByRole('button',{name:'OCR 选区',exact:true}).click();
 await page.getByRole('button',{name:'AI 阅卷',exact:true}).click();
 await page.waitForFunction(()=>!!window.scanTest.ocr);
 await page.waitForFunction(()=>window.scanTest.scanning===true);
 if(await page.getByRole('textbox',{name:'OCR 识别结果',exact:true}).inputValue()!=='')throw Error('OCR result should be empty');
 if(await page.locator('.ocr-skeleton,.scan-beam').count())throw Error('Old workbench animation remains');
 await page.screenshot({path:'scan-animation-check.png'});
 await page.evaluate(()=>window.scanTest.finish('ocr',JSON.stringify({choices:[{message:{content:'节约资源，保护环境。'}}]})));
 await page.waitForFunction(()=>!!window.scanTest.submit);
 if(await page.getByRole('textbox',{name:'OCR 识别结果',exact:true}).inputValue()!=='')throw Error('OCR not cleared during submit');
 if(!(await page.locator('.corner-score').textContent()).includes('上次得分　8'))throw Error('last score lost during submission');
 if(await page.locator('.ocr-analyzing').count())throw Error('Unexpected analysis text');
 await page.evaluate(()=>window.scanTest.finish('submit','ok'));
 await page.waitForFunction(()=>!!window.scanTest.next);
 await page.getByRole('button',{name:'关闭 OCR 选区',exact:true}).click();
 await page.waitForFunction(()=>window.scanTest.closed===true);
 if(await page.getByRole('button',{name:'OCR 选区',exact:true}).count()!==1)throw Error('Selection not reset');
 await page.waitForFunction(()=>!document.querySelector('.ocr-result').getAttribute('aria-busy') || document.querySelector('.ocr-result').getAttribute('aria-busy')==='false');
 await page.evaluate(()=>window.scanTest.finish('next','late-image'));
 if(await page.locator('.ocr-analyzing').count())throw Error('late result restarted scanner');
 return 'PASS: OCR scanner, immediate submit clearing, next-paper scanner, stop and late-response suppression';
}
