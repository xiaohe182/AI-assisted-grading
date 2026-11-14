using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class RegionPicker : Form
{
    Point start, end;
    bool dragging;

    public Rectangle Selection { get; private set; }

    public RegionPicker()
    {

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Black;
        Opacity = 0.32;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        KeyPreview = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) { DialogResult = DialogResult.Cancel; return; }
        dragging = true; Capture = true; start = end = e.Location;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging) { end = e.Location; Invalidate(); }
    }
    Rectangle Box(Point p)
    {
        return Rectangle.FromLTRB(Math.Min(start.X,p.X),Math.Min(start.Y,p.Y),Math.Max(start.X,p.X),Math.Max(start.Y,p.Y));
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging) return;
        dragging = false; Capture = false;
        var rect = Box(e.Location);
        if (rect.Width < 8 || rect.Height < 8) { Invalidate(); return; }
        Selection = RectangleToScreen(rect);
        DialogResult = DialogResult.OK;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (dragging) e.Graphics.DrawRectangle(Pens.Red,Box(end));
        var pos = PointToClient(Cursor.Position);
        e.Graphics.DrawString("OCR 选区 · Esc 取消",Font,Brushes.White,pos.X+12,pos.Y+16);
    }
}

// The visible pixels sit outside the capture rectangle so OCR never sees the label.
internal sealed class RegionMarker : Form
{
    public RegionMarker(Rectangle selection)
    {
        AutoScaleMode=AutoScaleMode.None;
        FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;
        ShowInTaskbar=false; TopMost=true;
        int top=selection.Top-SystemInformation.VirtualScreen.Top>=22 ? 22 : 2;
        Bounds=new Rectangle(selection.Left-2,selection.Top-top,Math.Max(82,selection.Width+4),selection.Height+top+(top==2?22:2));
        BackColor=Color.Firebrick;
        var rim=new Region(new Rectangle(0,top-2,selection.Width+4,selection.Height+4));
        rim.Exclude(new Rectangle(2,top,selection.Width,selection.Height));
        var label=new Label {Text="OCR 选区", ForeColor=Color.White, BackColor=BackColor,
            Bounds=new Rectangle(0,top==2?selection.Height+4:0,80,20),TextAlign=ContentAlignment.MiddleCenter};
        rim.Union(label.Bounds);
        Region=rim; Controls.Add(label);
    }
    protected override bool ShowWithoutActivation {get {return true;}}
    protected override CreateParams CreateParams {
        get {var cp=base.CreateParams; cp.ExStyle|=0x08000000|0x00000020|0x00080000; return cp;}
    }
}
internal sealed class ScreenReader
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flag);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    readonly Form owner;
    Rectangle region;
    IntPtr target;
    RegionMarker marker;
    public System.Threading.CancellationTokenSource Active = new System.Threading.CancellationTokenSource();
    public void Stop(){Active.Cancel();Active=new System.Threading.CancellationTokenSource();}
    string previous;
    bool capturing;
    static IntPtr Root(Point p) { return GetAncestor(WindowFromPoint(p),2); }
    public ScreenReader(Form window) { owner=window; owner.FormClosed+=(s,e)=>{Active.Cancel();if(marker!=null)marker.Dispose();}; }

    string Snapshot(bool waitForInk = false)
    {
        var middle = new Point(region.Left+region.Width/2,region.Top+region.Height/2);
        if (target==IntPtr.Zero || Root(middle)!=target || !SystemInformation.VirtualScreen.Contains(region))
            throw new Exception("答卷窗口或位置已改变，请重新框选");
        using(var bmp=new Bitmap(region.Width,region.Height))
        {
            using(var g=Graphics.FromImage(bmp)) g.CopyFromScreen(region.Location,Point.Empty,region.Size);
            if(waitForInk)
            {
                int marks=0;
                for(int y=0;y<bmp.Height && marks<12;y+=3)
                    for(int x=0;x<bmp.Width && marks<12;x+=3)
                    {
                        Color pixel=bmp.GetPixel(x,y);
                        if(pixel.R<190 && pixel.G<190 && pixel.B<190) marks++;
                    }
                if(marks<12) return null;
            }
            using(var stream=new MemoryStream())
            {
                bmp.Save(stream,ImageFormat.Png);
                return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());
            }
        }
    }
    public async Task<string> Execute(string action,string score)
    {
        if(capturing) throw new Exception("正在操作选区");
        var token=Active.Token;
        capturing=true;
        bool selecting=action=="selectRegion";
        try
        {
            token.ThrowIfCancellationRequested();
            if(selecting)
            {
                owner.Hide();
                await Task.Delay(160,token);
                if(marker!=null)marker.Hide();
                using(var picker=new RegionPicker())
                {
                    var handle=picker.Handle;
                    using(token.Register(()=>picker.BeginInvoke((Action)(()=>picker.DialogResult=DialogResult.Cancel))))
                    {
                        if(picker.ShowDialog()!=DialogResult.OK) return "cancel";
                    }
                    token.ThrowIfCancellationRequested();
                    var rect=picker.Selection;
                    await Task.Delay(120,token);
                    var hwnd=Root(new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2));
                    if(hwnd==IntPtr.Zero || hwnd==owner.Handle) throw new Exception("请选择阅卷平台窗口");
                    region=rect; target=hwnd; previous=null;
                    if(marker!=null)marker.Dispose();
                    marker=new RegionMarker(region);
                    return "selected";
                }
            }
            if(region.IsEmpty) throw new Exception("请先设置 OCR 选区");
            // A manual capture may start while the workbench covers the answer.
            // Leave the platform in front for subsequent captures and score entry.
            if((action=="submitScore" || (action=="captureRegion" && owner.Visible && owner.Bounds.IntersectsWith(region)))
                && GetForegroundWindow()!=target)
            {
                if(!SetForegroundWindow(target)) throw new Exception("请先切换到阅卷平台窗口");
                await Task.Delay(160,token);
            }
            if(action=="submitScore")
            {
                decimal numeric;
                if(!decimal.TryParse(score,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out numeric) || numeric<0 || score.Length>16) throw new Exception("分数无效");
                if(Snapshot()!=previous) throw new Exception("学生答案画面已变化，请重新识别");
                if(GetForegroundWindow()!=target) SetForegroundWindow(target);
                if(GetForegroundWindow()!=target) throw new Exception("请先点击阅卷平台分数框");
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                if(GetForegroundWindow()!=target) throw new Exception("请先点击阅卷平台分数框");
                SendKeys.SendWait(score);
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                if(GetForegroundWindow()!=target) throw new Exception("请先点击阅卷平台分数框");
                SendKeys.SendWait("{ENTER}");
                return "ok";
            }
            if(action=="nextRegion")
            {
                await Task.Delay(200,token);
                string candidate=null;
                for(int i=0;i<24;i++)
                {
                    token.ThrowIfCancellationRequested();
                    if(GetAsyncKeyState(27)<0) throw new OperationCanceledException("阅卷已停止");
                    string current=Snapshot(true);
                    if(current!=null && current!=previous && current==candidate) {previous=current;return current;}
                    candidate=current;
                    await Task.Delay(500,token);
                }
                throw new Exception("未确认下一份答卷加载完成，请检查后重新识别");
            }
            previous=Snapshot();
            return previous;
        }
        finally {
            capturing=false;
            if(selecting && !owner.IsDisposed) {
                owner.Show();owner.Activate();
                if(marker!=null&&!marker.IsDisposed)marker.Show();
            }
        }
    }
}
