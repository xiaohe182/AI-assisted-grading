# AI-assisted-grading

面向 Windows 的全科 AI 辅助阅卷桌面工具。

支持屏幕 OCR 选区、参考答案与评分标准、AI 评分、自动填分、停止阅卷和老师配置。OCR 使用 qwen-vl-ocr，评分服务可在设置中选择，API Key 加密保存在本机。

## 使用

解压 `交付/阅卷台.zip`，运行 `阅卷台.exe`。首次在设置中填写并保存模型配置，框选阅卷平台的答案区域后开始阅卷。详细操作见压缩包内的使用说明。

运行环境为 Windows 10/11 64 位、.NET Framework 4.6.2 或更高版本，以及 Microsoft Edge WebView2 Runtime。

## 开发与打包

```powershell
cd grading-workbench
npm ci
node --test tests/*.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File desktop/build.ps1
```

桌面宿主代码位于 `grading-workbench/desktop`，界面代码位于 `grading-workbench/src`。打包输出为项目根目录的 `阅卷台.exe`。

仓库包含源码、测试、文档和交付文件。第三方依赖、内置 Python 运行库、构建缓存及本地阅卷数据通过 `.gitignore` 排除。
