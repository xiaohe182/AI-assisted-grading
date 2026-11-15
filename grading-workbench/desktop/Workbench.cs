using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public sealed class Workbench : Form
{
    readonly WebView2 view = new WebView2();
    readonly string assets;
    readonly bool smoke;
    bool smokeReloaded;

    public Workbench(string directory, bool test)
    {
        assets = directory;
        smoke = test;
        Text = "阅卷台";
        AutoScaleMode=AutoScaleMode.None;
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;
        var initialSize=test?new Size(1280,860):DefaultWindowSize(area);
        StartPosition=FormStartPosition.Manual;
        Bounds=new Rectangle(area.Left+(area.Width-initialSize.Width)/2,area.Top+(area.Height-initialSize.Height)/2,initialSize.Width,initialSize.Height);
        // 400 px is an initial width fallback, not a resize restriction.
        MinimumSize=Size.Empty;
        view.Dock = DockStyle.Fill;
        Controls.Add(view);
        Shown += OpenWorkbench;
    }

    public static Size DefaultWindowSize(Rectangle area) {
        return new Size(Math.Max(400,(int)Math.Round(area.Width*.38)),(int)Math.Round(area.Height*.95));
    }

    async void OpenWorkbench(object sender, EventArgs args)
    {
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YueJuanTai", smoke ? "WebView2-smoke" : "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, profile);
            await view.EnsureCoreWebView2Async(env);
            // Smoke assertions use CSS pixels; normalize high-DPI host scaling.
            if(smoke) using(var graphics=CreateGraphics()) view.ZoomFactor=96.0/graphics.DpiX;
            var core = view.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("workbench.example", assets, CoreWebView2HostResourceAccessKind.Deny);
            var screenReader = new ScreenReader(this);
            screenReader.RegionEvent += name => core.PostWebMessageAsJson(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { regionEvent=name }));
            core.WebMessageReceived += (source, message) => ModelRequest.Receive(source,message,screenReader,smoke);
            core.NavigationStarting += (s, e) => {
                Uri url;
                e.Cancel = !Uri.TryCreate(e.Uri, UriKind.Absolute, out url) || url.Scheme != "https" || url.Host != "workbench.example";
            };
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.NavigationCompleted += async (s, e) => {
                if (!smoke) return;
                if(DefaultWindowSize(new Rectangle(0,0,1600,1000))!=new Size(608,950)
                    || DefaultWindowSize(new Rectangle(0,0,1000,800))!=new Size(400,760)
                    || MinimumSize.Width>=400)throw new Exception("默认窗口尺寸或缩放限制不正确");
                for(int i=0;i<100;i++) {
                    if(await core.ExecuteScriptAsync("!!document.querySelector('.student-column textarea')") == "true") break;
                    await System.Threading.Tasks.Task.Delay(50);
                }
                await core.ExecuteScriptAsync("document.querySelector('a[href=\"#settings\"]').click()");
                await System.Threading.Tasks.Task.Delay(100);
                if(!smokeReloaded) {
                    await core.ExecuteScriptAsync(@"(() => {
                        const subject = document.querySelector('.settings-intro select');
                        Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(subject,'物理');
                        subject.dispatchEvent(new Event('change',{bubbles:true}));
                        const input = document.querySelector('.secret-field input');
                        Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'local-cache-smoke-key');
                        input.dispatchEvent(new Event('input',{bubbles:true}));
                    })()");
                    await System.Threading.Tasks.Task.Delay(100);
                    await core.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.includes('保存模型配置')).click()");
                    for(int i=0;i<100;i++) {
                        if(await core.ExecuteScriptAsync("document.querySelector('.toast')?.textContent.includes('模型配置已保存') || false") == "true") break;
                        await System.Threading.Tasks.Task.Delay(50);
                    }
                    smokeReloaded = true;
                    core.Navigate("https://workbench.example/index.html");
                    return;
                }
                bool cached = await core.ExecuteScriptAsync("document.querySelector('.secret-field input').value === 'local-cache-smoke-key'") == "true";
                foreach(var tab in new[]{"模型与连接","本地保存","老师定制化"}) {
                    await core.ExecuteScriptAsync("Array.from(document.querySelectorAll('.settings-tabs button')).find(b=>b.textContent==='"+tab+"').click()");
                    await System.Threading.Tasks.Task.Delay(100);
                    using(var screenshot=File.Create(Path.Combine(Path.GetTempPath(),"grading-settings-"+tab+".png")))
                        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
                    if(await core.ExecuteScriptAsync("document.documentElement.scrollWidth <= innerWidth && !document.querySelector('.settings-page h1')") != "true")
                        cached=false;
                }
                Width=760;
                await System.Threading.Tasks.Task.Delay(150);
                using(var screenshot=File.Create(Path.Combine(Path.GetTempPath(),"grading-settings-narrow.png")))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
                if(await core.ExecuteScriptAsync("document.documentElement.scrollWidth <= innerWidth") != "true")
                    cached=false;
                Width=1280;
                await System.Threading.Tasks.Task.Delay(100);
                await core.ExecuteScriptAsync("document.querySelector('a[href=\"#workbench\"]').click()");
                await System.Threading.Tasks.Task.Delay(100);
                var loaded = await core.ExecuteScriptAsync("Boolean(!document.querySelector('.workbench-page h1') && document.querySelector('.auto-check input').checked && document.querySelectorAll('[data-slot=card]').length === 2 && document.querySelector('[data-slot=card]').classList.contains('rubric-column') && document.body.textContent.includes('OCR 选区') && !document.body.textContent.includes('全能老师') && !document.body.textContent.includes('配置模型'))");
                var styles = await core.ExecuteScriptAsync(@"(() => {
                    const style = selector => getComputedStyle(document.querySelector(selector));
                    const bottom = document.querySelector('.student-column textarea').getBoundingClientRect().bottom;
                    const footer = document.querySelector('.workbench-footer').getBoundingClientRect().top;
                    const original = document.querySelector('.answer-image-frame').getBoundingClientRect();
                    const text = document.querySelector('.student-column textarea').getBoundingClientRect();
                    return style('.sidebar nav a.active').fontWeight === '600'
                        && style('.sidebar nav a:not(.active)').fontWeight === '600'
                        && document.querySelector('.rubric-text').placeholder.includes('欧姆定律')
                        && document.querySelector('.rubric-text').value === ''
                        && getComputedStyle(document.querySelector('.rubric-text'),'::placeholder').color === 'rgb(161, 161, 170)'
                        && document.querySelector('.student-column textarea').placeholder === ''
                        && document.querySelector('.student-column textarea').readOnly
                        && !document.querySelector('.student-column textarea').disabled
                        && document.querySelector('.image-placeholder').textContent.length > 0
                        && !document.querySelector('.workbench-page select')
                        && !document.querySelector('.footer-actions input')
                        && original.right < text.left && Math.abs(original.bottom-text.bottom) <= 2
                        && document.querySelector('.ocr-actions button:last-child').textContent.includes('OCR 选区')
                        && style('.field textarea').lineHeight === '20px' && footer - bottom >= 0 && footer - bottom <= 24;
                })()");
                await core.ExecuteScriptAsync("document.querySelector('.footer-actions button.primary').click()");
                await System.Threading.Tasks.Task.Delay(150);
                var message = await core.ExecuteScriptAsync(@"(() => {
                    return document.querySelector('.footer-actions button.primary').disabled
                        && document.querySelector('#rubric-maximum').value === ''
                        && !document.body.textContent.includes('准备开始')
                        && !document.body.textContent.includes('桌面预览版');
                })()");
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "grading-smoke-result.txt"), "navigation="+e.IsSuccess+" cached="+cached+" loaded="+loaded+" styles="+styles+" message="+message);
                Environment.ExitCode = e.IsSuccess && cached && loaded == "true" && styles == "true" && message == "true" ? 0 : 1;
                using(var screenshot = File.Create(Path.Combine(Path.GetTempPath(), "grading-layout-check.png")))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, screenshot);
                await core.ExecuteScriptAsync("location.hash='records'");
                await System.Threading.Tasks.Task.Delay(100);
                var removedRecords = await core.ExecuteScriptAsync(@"(() => {
                    return !!document.querySelector('.workbench-page')
                        && !document.querySelector('a[href=""#records""]')
                        && !document.body.textContent.includes('阅卷记录');
                })()");
                if(removedRecords != "true") Environment.ExitCode = 1;
                var smokeConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YueJuanTai", "ModelConfig-smoke", "models.dat");
                if(File.Exists(smokeConfig)) File.Delete(smokeConfig);
                using(var marker = new RegionMarker(new System.Drawing.Rectangle(120,120,240,80)))
                {
                    var capture=marker.CaptureBounds;
                    if(marker.Region.IsVisible(capture.Left+20,capture.Top+20)
                        || marker.CloseButton.Text!="关闭 OCR 选区" || marker.ReselectButton.Text!="重选区域")
                        Environment.ExitCode=1;
                }
                Close();
            };
            core.Navigate("https://workbench.example/index.html");
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            if(smoke) File.WriteAllText(Path.Combine(Path.GetTempPath(), "grading-smoke-result.txt"),error.ToString());
            if (!smoke) MessageBox.Show("无法打开阅卷台，请检查 Microsoft Edge WebView2 运行时。\n" + error.Message, Text);
            Close();
        }
    }
}
