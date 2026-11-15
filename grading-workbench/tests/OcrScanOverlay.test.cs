using System;
using System.Drawing;
using System.Drawing.Imaging;
class ScanOverlayTest {
 [STAThread] static int Main(){
  if(OcrScanOverlay.SweepSeconds>0.6 || OcrScanOverlay.PhaseAt(0)<=0)throw new Exception("Scan starts too late or moves too slowly");
  using(var marker=new RegionMarker(new Rectangle(20,80,500,500))){
   if(marker.Region.IsVisible(marker.CaptureBounds.Left+30,marker.CaptureBounds.Top+30))throw new Exception("Frame covers the answer");
   using(var preview=new Bitmap(marker.Width,marker.Height)){
    marker.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));
    foreach(System.Windows.Forms.Control child in marker.Controls)child.DrawToBitmap(preview,child.Bounds);
    preview.Save("datav-frame-check.png",ImageFormat.Png);
   }
   bool close=false,reselect=false;marker.CloseRequested+=()=>close=true;marker.ReselectRequested+=()=>reselect=true;
   marker.RequestClose();marker.RequestReselect();
   if(!close||!reselect)throw new Exception("Toolbar actions not connected");
  }

  using(var a=OcrScanOverlay.Render(800,400,.5))
  using(var b=OcrScanOverlay.Render(800,400,.25)){
   if(a.GetPixel(400,200).A<150 || a.GetPixel(500,100).A<150)throw new Exception("Missing diagonal highlight");
   if(a.GetPixel(20,20).A!=0 || a.GetPixel(780,380).A!=0)throw new Exception("Overlay obscures unrelated pixels");
   if(b.GetPixel(400,200).A!=0)throw new Exception("Highlight did not move");
   a.Save("scan-diagonal-check.png",ImageFormat.Png);
  }
  using(var owner=new System.Windows.Forms.Form()) {
   var reader=new ScreenReader(owner);
   var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   typeof(ScreenReader).GetField("region",flags).SetValue(reader,new Rectangle(0,0,800,400));
   var scanner=typeof(ScreenReader).GetField("scanner",flags);
   reader.SetScanning(true);
   if(scanner.GetValue(reader)==null)throw new Exception("Scan not created");
   reader.Stop();
   if(scanner.GetValue(reader)!=null)throw new Exception("Stop did not dispose overlay");
   reader.SetScanning(true);
   try{typeof(ScreenReader).GetMethod("Snapshot",flags).Invoke(reader,new object[]{false});}catch(System.Reflection.TargetInvocationException){}
   if(scanner.GetValue(reader)!=null)throw new Exception("Capture retained overlay");
  }
  using(var owner=new System.Windows.Forms.Form()) {
   var reader=new ScreenReader(owner);var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   typeof(ScreenReader).GetField("region",flags).SetValue(reader,new Rectangle(20,80,500,500));
   reader.SetScanning(true);var token=reader.Active.Token;reader.CloseRegion();
   if(!token.IsCancellationRequested || !(bool)typeof(Rectangle).GetProperty("IsEmpty").GetValue(typeof(ScreenReader).GetField("region",flags).GetValue(reader),null))throw new Exception("Close must cancel and clear selection");
   if(typeof(ScreenReader).GetField("scanner",flags).GetValue(reader)!=null)throw new Exception("Close retained scan");
  }
  Console.WriteLine("PASS: diagonal highlight, transparent background, moving sweep");return 0;
 }
}
