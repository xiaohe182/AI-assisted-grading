import React from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App.jsx";
import "./admin/theme.css";
import "./app.css";

import {desktopRequest} from './grading.js';
import {initialSettings} from './domain.js';

async function start() {
 let settings = structuredClone(initialSettings);
 let startupError = '';
 if (window.chrome?.webview) {
   try {
     const saved = JSON.parse(await desktopRequest({action:'loadModels'}));
     settings = {...settings, subject:saved.subject || '通用',
       ocr:{...settings.ocr,apiKey:saved.ocr?.apiKey || ''},
       grading:{...settings.grading,...saved.grading,enable_thinking:false}};
   } catch { startupError = '本地模型配置读取失败，请在设置中重新保存'; }
 }
 createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App initialConfig={settings} startupError={startupError}/>
  </React.StrictMode>,
);
}
start();
