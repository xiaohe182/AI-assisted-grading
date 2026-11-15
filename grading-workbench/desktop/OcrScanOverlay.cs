using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Per-pixel alpha, click-through overlay on the actual selected screen rectangle.
internal sealed class OcrScanOverlay : Form
{
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; public Point(int x,int y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential)] struct NativeSize { public int Width,Height; public NativeSize(int w,int h){Width=w;Height=h;} }
    [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend { public byte Op,Flags,Alpha,Format; }
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref Point at,ref NativeSize size,IntPtr src,ref Point origin,int key,ref Blend blend,int flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    readonly Timer timer=new Timer {Interval=16};
    readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
    readonly IntPtr target;
    public OcrScanOverlay(Rectangle selection,IntPtr window) {
        target=window;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;Bounds=selection;ShowInTaskbar=false;TopMost=true;
        timer.Tick+=(s,e)=>{
            if(clock.ElapsedMilliseconds>65000){Close();return;}
            if(GetForegroundWindow()!=target){Hide();return;}
            if(!Visible)Show();
            DrawFrame();
        };
    }
    public const double SweepSeconds=0.55;
    public static double PhaseAt(double seconds){return 0.10+0.80*((seconds%SweepSeconds)/SweepSeconds);}
    public void Start(){DrawFrame();if(GetForegroundWindow()==target)Show();timer.Start();}
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x08000000|0x80;return p;}}
    public static Bitmap Render(int width,int height,double phase) {
        var bitmap=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(bitmap)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            // x + y = travel: a diagonal ribbon sweeping from top-left to bottom-right.
            float travel=(float)((width+height+280)*phase-140);
            using(var brush=new LinearGradientBrush(new RectangleF(travel-150,0,300,1),Color.Transparent,Color.Transparent,0f)) {
                brush.InterpolationColors=new ColorBlend {Positions=new[]{0f,.32f,.48f,.50f,.52f,.68f,1f},Colors=new[]{Color.Transparent,Color.FromArgb(36,109,60,255),Color.FromArgb(170,31,217,255),Color.FromArgb(245,229,255,255),Color.FromArgb(170,89,223,255),Color.FromArgb(30,126,73,255),Color.Transparent}};
                using(var transform=new Matrix(1,0,-1,1,0,0))brush.Transform=transform;
                g.FillPolygon(brush,new[]{new PointF(travel-150,0),new PointF(travel+150,0),new PointF(travel+150-height,height),new PointF(travel-150-height,height)});
            }
            using(var core=new Pen(Color.FromArgb(235,232,255,255),2))g.DrawLine(core,travel,0,travel-height,height);
            for(int i=0;i<5;i++){
                float offset=-105+i*42;
                using(var trail=new Pen(Color.FromArgb(38+i*12,i%2==0?78:141,178,255),1))g.DrawLine(trail,travel+offset,0,travel+offset-height,height);
            }
            for(int i=0;i<36;i++){
                float x=(i*137+19)%Math.Max(1,width),y=travel-x+(i*29%190)-95;
                float radius=i%6==0?2.2f:1.1f;
                int alpha=90+(int)(120*Math.Abs(Math.Sin(i*1.7+phase*6.28)));
                using(var glow=new SolidBrush(Color.FromArgb(35,41,209,255)))g.FillEllipse(glow,x-5,y-5,10,10);
                using(var dot=new SolidBrush(Color.FromArgb(alpha,210,250,255)))g.FillEllipse(dot,x-radius,y-radius,radius*2,radius*2);
                if(i%6==0)using(var sparkle=new Pen(Color.FromArgb(alpha,169,241,255),1)){g.DrawLine(sparkle,x-6,y,x+6,y);g.DrawLine(sparkle,x,y-6,x,y+6);}
            }
        }
        return bitmap;
    }
    void DrawFrame() {
        using(var bitmap=Render(Width,Height,PhaseAt(clock.Elapsed.TotalSeconds))) {
            IntPtr screen=GetDC(IntPtr.Zero), memory=CreateCompatibleDC(screen), image=IntPtr.Zero, old=IntPtr.Zero;
            try {
                image=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(memory,image);
                var at=new Point(Left,Top);var origin=new Point(0,0);var size=new NativeSize(Width,Height);
                var blend=new Blend {Alpha=255,Format=1};
                UpdateLayeredWindow(Handle,screen,ref at,ref size,memory,ref origin,0,ref blend,2);
            } finally {if(old!=IntPtr.Zero)SelectObject(memory,old);if(image!=IntPtr.Zero)DeleteObject(image);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
        }
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
