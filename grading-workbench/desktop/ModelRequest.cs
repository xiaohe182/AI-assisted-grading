using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

internal static class ModelRequest
{
    static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public static async void Receive(object sender, CoreWebView2WebMessageReceivedEventArgs args, ScreenReader screen, bool smoke)
    {
        var core = (CoreWebView2)sender;
        if (new Uri(args.Source).GetLeftPart(UriPartial.Authority) != "https://workbench.example") return;
        var json = new JavaScriptSerializer { MaxJsonLength = 32000000 };
        string id = null, content = null, error = null;
        try
        {
            var data = json.Deserialize<Dictionary<string,string>>(args.WebMessageAsJson);
            id = data["id"];
            string action;
            if(data.TryGetValue("action",out action))
            {
                if(action=="loadModels" || action=="saveModels")
                {
                    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YueJuanTai", smoke ? "ModelConfig-smoke" : "ModelConfig");
                    var path = Path.Combine(folder, "models.dat");
                    if(action=="loadModels")
                        content = File.Exists(path) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)) : "{}";
                    else
                    {
                        json.DeserializeObject(data["body"]);
                        Directory.CreateDirectory(folder);
                        var pending = path + ".tmp";
                        try {
                            File.WriteAllBytes(pending, ProtectedData.Protect(Encoding.UTF8.GetBytes(data["body"]), null, DataProtectionScope.CurrentUser));
                            if(File.Exists(path)) File.Replace(pending, path, null);
                            else File.Move(pending, path);
                        } finally { if(File.Exists(pending)) File.Delete(pending); }
                        content = "ok";
                    }
                }
                else if(action=="stop"){screen.Stop();content="ok";}
                else {
                if(action!="selectRegion" && action!="captureRegion" && action!="nextRegion" && action!="submitScore")
                    throw new Exception("无效的桌面操作");
                content=await screen.Execute(action,data.ContainsKey("score")?data["score"]:"");
                }
            }
            else
            {
            var token = screen.Active.Token;
            token.ThrowIfCancellationRequested();
            var url = new Uri(data["url"].TrimEnd('/') + "/chat/completions");
            if (url.Scheme != "https" && !(url.IsLoopback && url.Scheme == "http"))
                throw new Exception("接口需要 HTTPS 或本机 HTTP 地址");
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", data["key"]);
                request.Content = new StringContent(data["body"], Encoding.UTF8, "application/json");
                using (var response = await Client.SendAsync(request,token))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new Exception("模型接口返回 " + (int)response.StatusCode + "，请检查模型配置后重试");
                    content = await response.Content.ReadAsStringAsync();
                    token.ThrowIfCancellationRequested();
                }
            }
        }
        }
        catch (OperationCanceledException) { error = "操作已取消或请求超时"; }
        catch (Exception ex) { error = ex.Message; }
        try { core.PostWebMessageAsJson(json.Serialize(new { id, content, error })); }
        catch (InvalidOperationException) { /* Window closed during the request. */ }
    }
}
