using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

class ScreenReaderTest
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,IntPtr process);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [STAThread]
    static int Main()
    {
        try {SetProcessDpiAwarenessContext(new IntPtr(-4));} catch(EntryPointNotFoundException) {}
        Application.EnableVisualStyles();
        var area=Screen.PrimaryScreen.WorkingArea;
        using(var owner=new Form {Text="阅卷窗口测试",TopMost=true,StartPosition=FormStartPosition.Manual,
            Bounds=new Rectangle(area.Left+10,area.Top+10,340,260)})
        using(var platform=new Form {Text="填分测试窗口",TopMost=true,StartPosition=FormStartPosition.Manual,
            Bounds=new Rectangle(area.Left+370,area.Top+10,400,300)})
        {
            var answer=new Label {Text="学生答案 0",Bounds=new Rectangle(20,20,300,100),
                BackColor=Color.White,Font=new Font("Arial",18)};
            var input=new TextBox {Bounds=new Rectangle(20,160,200,32)};
            platform.Controls.Add(answer);platform.Controls.Add(input);
            int submissions=0,hidden=0,activated=0;
            input.KeyDown+=(s,e)=>{
                if(e.KeyCode!=Keys.Enter)return;
                e.SuppressKeyPress=true;
                if(input.Text!="2")throw new Exception("填入的分数不正确");
                input.Clear();submissions++;
                answer.Text="学生答案 "+submissions;
            };
            owner.Shown+=async(s,e)=>{
                try {
                    platform.Show();platform.Activate();input.Focus();
                    uint foregroundThread=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
                    uint currentThread=GetCurrentThreadId();
                    AttachThreadInput(currentThread,foregroundThread,true);
                    SetForegroundWindow(platform.Handle);
                    input.Focus();
                    AttachThreadInput(currentThread,foregroundThread,false);
                    await Task.Delay(200);
                    var reader=new ScreenReader(owner);
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    typeof(ScreenReader).GetField("region",flags).SetValue(reader,answer.RectangleToScreen(answer.ClientRectangle));
                    typeof(ScreenReader).GetField("target",flags).SetValue(reader,platform.Handle);
                    owner.VisibleChanged+=(a,b)=>{if(!owner.Visible)hidden++;};
                    owner.Activated+=(a,b)=>activated++;
                    await reader.Execute("captureRegion","");
                    for(int i=0;i<3;i++) {
                        await reader.Execute("submitScore","2");
                        await reader.Execute("nextRegion","");
                    }
                    if(hidden!=0 || activated!=0 || submissions!=3)
                        throw new Exception("连续阅卷发生窗口切换或填分次数错误");

                    // Manual confirmation starts from an overlapping workbench.
                    owner.Bounds=platform.Bounds;
                    owner.Activate();
                    await Task.Delay(100);
                    hidden=0;activated=0;
                    await reader.Execute("submitScore","2");
                    await reader.Execute("nextRegion","");
                    if(hidden!=0 || activated!=0 || submissions!=4)
                        throw new Exception("手动确认发生窗口闪烁");
                    answer.Text="学生答案发生变化";
                    await Task.Delay(100);
                    bool rejected=false;
                    try {await reader.Execute("submitScore","2");}
                    catch(Exception) {rejected=true;}
                    if(!rejected || hidden!=0 || activated!=0)
                        throw new Exception("答卷变化校验或异常恢复失败");
                    await reader.Execute("captureRegion","");
                    var waiting=reader.Execute("nextRegion","");
                    var elapsed=System.Diagnostics.Stopwatch.StartNew();
                    reader.Stop();
                    bool canceled=false;
                    try {await waiting;} catch(OperationCanceledException) {canceled=true;}
                    if(!canceled || elapsed.ElapsedMilliseconds>200 || submissions!=4)
                        throw new Exception("停止没有及时取消等待");
                    await reader.Execute("captureRegion","");
                    if(hidden!=0 || activated!=0)throw new Exception("停止或重新开始发生窗口切换");
                    Console.WriteLine("PASS: continuous/manual entry; no flicker; canceled wait in "+elapsed.ElapsedMilliseconds+" ms; restart succeeded.");

                } catch(Exception error) {
                    Console.Error.WriteLine(error.ToString());
                    Environment.ExitCode=1;
                } finally {platform.Close();owner.Close();}
            };
            Application.Run(owner);
        }
        return Environment.ExitCode;
    }
}
