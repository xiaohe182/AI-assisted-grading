using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
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

// Only the border and toolbar own pixels; the selected answer stays click-through.
internal sealed class RegionMarker : Form
{
    public event Action CloseRequested;
    public event Action ReselectRequested;
    public readonly Button CloseButton,ReselectButton;
    public Rectangle CaptureBounds {get;private set;}
    public bool ToolbarOverlapsSelection {get;private set;}
    readonly Label status;
    readonly Rectangle toolbar;
    public RegionMarker(Rectangle selection) {
        AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;
        BackColor=Color.Black;TransparencyKey=Color.Black;Opacity=.99;
        var screen=Screen.FromRectangle(selection).Bounds;
        int barWidth=Math.Min(470,screen.Width),barHeight=44;
        int barX=Math.Max(screen.Left,Math.Min(selection.Left,screen.Right-barWidth));
        int barY=selection.Top-58>=screen.Top?selection.Top-58:selection.Bottom+58<=screen.Bottom?selection.Bottom+14:Math.Max(screen.Top,selection.Top);
        var barScreen=new Rectangle(barX,barY,barWidth,barHeight);
        var outer=Rectangle.Inflate(selection,14,14);
        Bounds=Rectangle.Union(outer,barScreen);
        CaptureBounds=new Rectangle(selection.X-Left,selection.Y-Top,selection.Width,selection.Height);
        toolbar=new Rectangle(barX-Left,barY-Top,barWidth,barHeight);
        ToolbarOverlapsSelection=barScreen.IntersectsWith(selection);
        var rim=new Region(new Rectangle(outer.X-Left,outer.Y-Top,outer.Width,outer.Height));
        rim.Exclude(CaptureBounds);rim.Union(toolbar);Region=rim;
        Font=new Font("Microsoft YaHei UI",9f);
        var title=new Label {Text="OCR 选区",ForeColor=Color.FromArgb(207,250,254),BackColor=Color.FromArgb(8,22,46),TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(toolbar.X+15,toolbar.Y+7,90,30)};
        status=new Label {Text="选区已就绪",ForeColor=Color.FromArgb(45,212,191),BackColor=title.BackColor,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(toolbar.X+105,toolbar.Y+7,95,30)};
        ReselectButton=MakeButton("重选区域",new Rectangle(toolbar.Right-262,toolbar.Y+7,100,30),Color.FromArgb(34,211,238));
        CloseButton=MakeButton("关闭 OCR 选区",new Rectangle(toolbar.Right-152,toolbar.Y+7,137,30),Color.FromArgb(251,113,133));
        ReselectButton.Click+=(o,e)=>RequestReselect();CloseButton.Click+=(o,e)=>RequestClose();
        Controls.AddRange(new Control[]{title,status,ReselectButton,CloseButton});
    }
    public void RequestClose(){if(CloseRequested!=null)CloseRequested();}
    public void RequestReselect(){if(ReselectRequested!=null)ReselectRequested();}
    public void SetScanning(bool scanning){status.Text=scanning?"正在识别":"选区已就绪";}
    static Button MakeButton(string text,Rectangle bounds,Color color){
        var button=new Button {Text=text,Bounds=bounds,FlatStyle=FlatStyle.Flat,ForeColor=color,BackColor=Color.FromArgb(12,28,53),Cursor=Cursors.Hand,TabStop=false};
        button.FlatAppearance.BorderColor=color;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(25,49,79);return button;
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var r=Rectangle.Inflate(CaptureBounds,4,4);
        using(var glow=new Pen(Color.FromArgb(8,55,81),10))g.DrawRectangle(glow,r);
        using(var line=new Pen(Color.FromArgb(34,211,238),1))g.DrawRectangle(line,r);
        using(var violet=new Pen(Color.FromArgb(139,92,246),1))g.DrawRectangle(violet,Rectangle.Inflate(r,5,5));
        int arm=Math.Min(30,Math.Min(r.Width,r.Height)/3);
        using(var pen=new Pen(Color.FromArgb(18,238,255),5)){
            g.DrawLines(pen,new[]{new Point(r.Left,r.Top+arm),new Point(r.Left,r.Top),new Point(r.Left+arm,r.Top)});
            g.DrawLines(pen,new[]{new Point(r.Right-arm,r.Top),new Point(r.Right,r.Top),new Point(r.Right,r.Top+arm)});
            g.DrawLines(pen,new[]{new Point(r.Left,r.Bottom-arm),new Point(r.Left,r.Bottom),new Point(r.Left+arm,r.Bottom)});
            g.DrawLines(pen,new[]{new Point(r.Right-arm,r.Bottom),new Point(r.Right,r.Bottom),new Point(r.Right,r.Bottom-arm)});
        }
        using(var ticks=new Pen(Color.FromArgb(34,211,238),2))
            for(int i=0;i<8 && r.Width>180;i++){int x=r.Left+65+i*6;g.DrawLine(ticks,x,r.Top-8,x+3,r.Top-5);g.DrawLine(ticks,x,r.Bottom+5,x+3,r.Bottom+8);}
        using(var node=new SolidBrush(Color.FromArgb(165,243,252))){g.FillRectangle(node,r.Left+r.Width/2-3,r.Top-3,6,6);g.FillRectangle(node,r.Left+r.Width/2-3,r.Bottom-3,6,6);}
        using(var bg=new SolidBrush(Color.FromArgb(8,22,46)))g.FillPolygon(bg,Bevel(toolbar));
        using(var edge=new Pen(Color.FromArgb(34,211,238),1))g.DrawPolygon(edge,Bevel(toolbar));
    }
    static Point[] Bevel(Rectangle r){return new[]{new Point(r.Left+12,r.Top),new Point(r.Right-12,r.Top),new Point(r.Right,r.Top+12),new Point(r.Right,r.Bottom-12),new Point(r.Right-12,r.Bottom),new Point(r.Left+12,r.Bottom),new Point(r.Left,r.Bottom-12),new Point(r.Left,r.Top+12)};}
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.ExStyle|=0x08000000|0x00080000|0x80;return cp;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
}
internal sealed class ScreenReader
{
    [DllImport("dwmapi.dll")] static extern int DwmFlush();
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flag);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    readonly Form owner;
    Rectangle region;
    IntPtr target;
    RegionMarker marker;
    OcrScanOverlay scanner;
    public event Action<string> RegionEvent;
    public void CloseRegion(){
        Stop();region=Rectangle.Empty;previous=null;target=IntPtr.Zero;
        if(marker!=null){marker.Dispose();marker=null;}
        if(RegionEvent!=null)RegionEvent("regionClosed");
    }
    public void SetScanning(bool enabled) {
        if(marker!=null&&!marker.IsDisposed){marker.SetScanning(enabled);if(!marker.Visible)marker.Show();}
        if(scanner!=null){scanner.Dispose();scanner=null;}
        if(enabled && !region.IsEmpty && !capturing){scanner=new OcrScanOverlay(region,target);scanner.Start();}
    }
    public System.Threading.CancellationTokenSource Active = new System.Threading.CancellationTokenSource();
    public void Stop(){SetScanning(false);Active.Cancel();Active=new System.Threading.CancellationTokenSource();}
    string previous;
    bool capturing;
    static IntPtr Root(Point p) { return GetAncestor(WindowFromPoint(p),2); }
    public ScreenReader(Form window) { owner=window; owner.FormClosed+=(s,e)=>{SetScanning(false);Active.Cancel();if(marker!=null)marker.Dispose();}; }

    string Snapshot(bool waitForInk = false)
    {
        bool hadScanner=scanner!=null;
        SetScanning(false); // Never include the visual effect in an OCR screenshot.
        bool hiddenMarker=marker!=null && marker.ToolbarOverlapsSelection && marker.Visible;
        if(hiddenMarker)marker.Hide();
        if(hadScanner||hiddenMarker)DwmFlush();
        try {
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
        finally {if(marker!=null&&!marker.IsDisposed&&!marker.Visible)marker.Show();}
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
                SetScanning(false);
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
                    marker.CloseRequested+=CloseRegion;
                    marker.ReselectRequested+=()=>{Stop();if(RegionEvent!=null)RegionEvent("reselectRegion");};
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
