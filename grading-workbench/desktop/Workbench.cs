using System;
using System.IO;
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
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        view.Dock = DockStyle.Fill;
        Controls.Add(view);
        Shown += OpenWorkbench;
    }

    async void OpenWorkbench(object sender, EventArgs args)
    {
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YueJuanTai", smoke ? "WebView2-smoke" : "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, profile);
            await view.EnsureCoreWebView2Async(env);
            var core = view.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("workbench.example", assets, CoreWebView2HostResourceAccessKind.Deny);
            var screenReader = new ScreenReader(this);
            core.WebMessageReceived += (source, message) => ModelRequest.Receive(source,message,screenReader,smoke);
            core.NavigationStarting += (s, e) => {
                Uri url;
                e.Cancel = !Uri.TryCreate(e.Uri, UriKind.Absolute, out url) || url.Scheme != "https" || url.Host != "workbench.example";
            };
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.NavigationCompleted += async (s, e) => {
                if (!smoke) return;
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
                        && document.querySelector('.student-column textarea').placeholder.length > 0
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
                    const toast = document.querySelector('.toast');
                    return !!toast && toast.matches(':popover-open') && toast.getBoundingClientRect().top < 50
                        && !document.body.textContent.includes('准备开始')
                        && !document.body.textContent.includes('桌面预览版');
                })()");
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
                    var label=marker.Controls[0];
                    if(marker.Region.IsVisible(122,42) || !marker.Region.IsVisible(1,42)
                        || label.Text!="OCR 选区" || !marker.Region.IsVisible(label.Left+1,label.Top+1))
                        Environment.ExitCode=1;
                }
                Close();
            };
            core.Navigate("https://workbench.example/index.html");
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            if (!smoke) MessageBox.Show("无法打开阅卷台，请检查 Microsoft Edge WebView2 运行时。\n" + error.Message, Text);
            Close();
        }
    }
}
