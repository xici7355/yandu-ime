using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace PersonalIme {
 internal static class ReadingPolicy {
  public const int MaxCharacters=12000;
  public static bool ValidText(string value){return !String.IsNullOrWhiteSpace(value)&&value.Length<=MaxCharacters;}
  public static Rectangle Selection(Point a,Point b,Rectangle bounds){return Rectangle.Intersect(Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)),bounds);}
  public static bool ValidCapture(Rectangle r){return r.Width>=8&&r.Height>=8&&(long)r.Width*r.Height<=24000000;}
  public static Rectangle TextBounds(System.Windows.Rect[] fragments,Rectangle desktop){
   var result=Rectangle.Empty;if(fragments==null)return result;
   foreach(var value in fragments){if(value.IsEmpty||Double.IsNaN(value.Left)||Double.IsNaN(value.Top)||Double.IsNaN(value.Right)||Double.IsNaN(value.Bottom)||Double.IsInfinity(value.Left)||Double.IsInfinity(value.Top)||Double.IsInfinity(value.Right)||Double.IsInfinity(value.Bottom)||value.Width<=0||value.Height<=0)continue;
    double left=Math.Max(desktop.Left,value.Left),top=Math.Max(desktop.Top,value.Top),right=Math.Min(desktop.Right,value.Right),bottom=Math.Min(desktop.Bottom,value.Bottom);if(right<=left||bottom<=top)continue;var r=Rectangle.FromLTRB((int)Math.Floor(left),(int)Math.Floor(top),(int)Math.Ceiling(right),(int)Math.Ceiling(bottom));result=result.IsEmpty?r:Rectangle.Union(result,r);
   }if(!result.IsEmpty){result.Inflate(2,2);result=Rectangle.Intersect(result,desktop);}return result;
  }
 }

 // UIA reads only the explicit selection. No Ctrl+C simulation or clipboard polling.
 internal sealed class SelectedText {
  public string Text;public IntPtr Foreground;public Point Anchor;public Rectangle Region;
  public static SelectedText Read(IntPtr window,Point point){
   try {
    if(window==IntPtr.Zero||Native.GetForegroundWindow()!=window)return null;
    uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid==(uint)Process.GetCurrentProcess().Id)return null;
    var element=AutomationElement.FocusedElement;if(PrivacyPolicy.ProtectedElement(element))return null;
    Rectangle region;var selected=ReadElement(element,pid,out region);
    if(selected==null){element=AutomationElement.FromPoint(new System.Windows.Point(point.X,point.Y));for(int i=0;i<6&&element!=null&&selected==null;i++){selected=ReadElement(element,pid,out region);if(selected==null)element=TreeWalker.ControlViewWalker.GetParent(element);}}
    if(selected==null||Native.GetForegroundWindow()!=window)return null;
    return new SelectedText{Text=selected,Foreground=window,Anchor=point,Region=region};
   }catch{return null;}
  }
  static string ReadElement(AutomationElement element,uint pid,out Rectangle region){
   region=Rectangle.Empty;
   if(element==null||element.Current.ProcessId!=pid||PrivacyPolicy.ProtectedElement(element))return null;
   object pattern;if(!element.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return null;
   var ranges=((TextPattern)pattern).GetSelection();if(ranges.Length!=1)return null;
   var value=ranges[0].GetText(ReadingPolicy.MaxCharacters+1);
   if(ReadingPolicy.ValidText(value))try{region=ReadingPolicy.TextBounds(ranges[0].GetBoundingRectangles(),SystemInformation.VirtualScreen);}catch{}
   return ReadingPolicy.ValidText(value)&&!PrivacyPolicy.IsSensitive(value)?value:null;
  }
 }

 internal sealed class TranslateBadge : Form {
  public event Action Requested;
  public TranslateBadge(){Text="言渡 · 划词翻译";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;ClientSize=new Size(42,38);BackColor=Color.FromArgb(174,255,50);
   var button=new Label{Dock=DockStyle.Fill,Text="译",TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Microsoft YaHei UI",13,FontStyle.Bold),ForeColor=Color.Black,Cursor=Cursors.Hand};Controls.Add(button);button.Click+=delegate{if(Requested!=null)Requested();};
  }
  protected override bool ShowWithoutActivation {get{return true;}}
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
  protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
  public void At(Point point){var area=Screen.FromPoint(point).WorkingArea;Location=new Point(Math.Max(area.Left,Math.Min(point.X+12,area.Right-Width)),Math.Max(area.Top,Math.Min(point.Y+18,area.Bottom-Height)));Show();}
 }

 internal sealed class CaptureOverlay : Form {
  readonly Bitmap image,dimmed;readonly Rectangle desktop;Point start,current;bool dragging,finished;readonly System.Windows.Forms.Timer refresh=new System.Windows.Forms.Timer{Interval=16};
  [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
  public Bitmap Captured {get;private set;}
  public Rectangle ScreenRegion {get;private set;}
  public CaptureOverlay(Bitmap source,Rectangle bounds){image=source;desktop=bounds;Text="言渡 · 框选截图";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=bounds;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;
   dimmed=new Bitmap(source.Width,source.Height,PixelFormat.Format24bppRgb);using(var g=Graphics.FromImage(dimmed)){g.DrawImageUnscaled(source,0,0);using(var shade=new SolidBrush(Color.FromArgb(105,0,0,0)))g.FillRectangle(shade,new Rectangle(Point.Empty,source.Size));}
   refresh.Tick+=delegate{if(!dragging||finished)return;var point=PointToClient(Cursor.Position);if((GetAsyncKeyState(1)&0x8000)==0){Finish(point);return;}UpdatePoint(point);};
   KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Escape)CancelSelection();};
  }
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImageUnscaled(dimmed,0,0);
   var r=ReadingPolicy.Selection(start,current,ClientRectangle);if(dragging&&r.Width>0&&r.Height>0){e.Graphics.DrawImage(image,r,r,GraphicsUnit.Pixel);using(var pen=new Pen(Color.FromArgb(174,255,50),2))e.Graphics.DrawRectangle(pen,r);}
   using(var font=new Font("Microsoft YaHei UI",12))using(var ink=new SolidBrush(Color.White))e.Graphics.DrawString("拖动框选文字区域 · Esc / 右键取消",font,ink,24,24);
  }
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Right){CancelSelection();return;}if(e.Button!=MouseButtons.Left||finished)return;start=current=e.Location;dragging=true;Capture=true;refresh.Start();}
  protected override void OnMouseMove(MouseEventArgs e){} // Poll latest physical position; queued moves never stretch the selection after release.
  void UpdatePoint(Point point){if(point==current)return;var old=ReadingPolicy.Selection(start,current,ClientRectangle);current=point;var dirty=Rectangle.Union(old,ReadingPolicy.Selection(start,current,ClientRectangle));dirty.Inflate(3,3);Invalidate(dirty);}
  protected override void OnMouseUp(MouseEventArgs e){if(e.Button==MouseButtons.Left)Finish(e.Location);}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(dragging&&!Capture){if((GetAsyncKeyState(1)&0x8000)!=0)CancelSelection();else Finish(PointToClient(Cursor.Position));}}
  void CancelSelection(){finished=true;dragging=false;refresh.Stop();Capture=false;DialogResult=DialogResult.Cancel;Close();}
  protected override void OnFormClosing(FormClosingEventArgs e){if(DialogResult!=DialogResult.OK){finished=true;dragging=false;refresh.Stop();Capture=false;}base.OnFormClosing(e);}
  void Finish(Point point){if(!dragging||finished)return;current=point;dragging=false;refresh.Stop();Capture=false;var r=ReadingPolicy.Selection(start,current,ClientRectangle);if(!ReadingPolicy.ValidCapture(r)){Invalidate();return;}finished=true;ScreenRegion=new Rectangle(desktop.Left+r.Left,desktop.Top+r.Top,r.Width,r.Height);Captured=image.Clone(r,PixelFormat.Format24bppRgb);DialogResult=DialogResult.OK;Close();}
  protected override void Dispose(bool disposing){if(disposing){refresh.Dispose();dimmed.Dispose();}base.Dispose(disposing);}
  public static Bitmap CaptureDesktop(Rectangle bounds){if((long)bounds.Width*bounds.Height>100000000)throw new InvalidOperationException("屏幕区域过大，请暂时减少屏幕数量后重试。");var image=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format24bppRgb);try{using(var g=Graphics.FromImage(image))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);return image;}catch{image.Dispose();throw;}}
 }

 internal static class LocalOcr {
  public static async Task<string> Recognize(Bitmap image,CancellationToken token){
   byte[] png;using(image)using(var memory=new MemoryStream()){token.ThrowIfCancellationRequested();SaveCapture(image,memory);png=memory.ToArray();}
   try{using(var process=Process.Start(new ProcessStartInfo{FileName=Application.ExecutablePath,Arguments="--ocr-worker-stdio",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8})){
     var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
     try{await process.StandardInput.BaseStream.WriteAsync(png,0,png.Length,token);process.StandardInput.Close();while(!process.HasExited)await Task.Delay(50,token);token.ThrowIfCancellationRequested();var reply=await output;await errors;
      const string prefix="YANDU_OCR_1|";if(process.ExitCode!=0||reply.Length>2000000||!reply.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidOperationException("本机文字识别失败，请重新框选清晰文字；图片没有写入临时文件。");
      return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(reply.Substring(prefix.Length))).Trim();
     }finally{try{if(!process.HasExited){process.Kill();process.WaitForExit(2000);}}catch{}}
    }
   }finally{Array.Clear(png,0,png.Length);}
  }
  static void SaveCapture(Bitmap source,Stream output){
   if((long)source.Width*source.Height<=2000000){using(var scaled=new Bitmap(source.Width*2,source.Height*2,PixelFormat.Format24bppRgb)){using(var graphics=Graphics.FromImage(scaled)){graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(source,new Rectangle(0,0,scaled.Width,scaled.Height));}scaled.Save(output,ImageFormat.Png);}}
   else source.Save(output,ImageFormat.Png);
  }
  // The worker uses stdin/stdout only; capture pixels and OCR text never touch disk.
  public static int Worker(){byte[] bytes=null;try{
   using(var memory=new MemoryStream()){var input=Console.OpenStandardInput();var buffer=new byte[65536];int count;while((count=input.Read(buffer,0,buffer.Length))>0){if(memory.Length+count>100L*1024*1024)return 2;memory.Write(buffer,0,count);}bytes=memory.ToArray();}
   string text=ReadPixels(bytes);byte[] reply=System.Text.Encoding.ASCII.GetBytes("YANDU_OCR_1|"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)));using(var output=Console.OpenStandardOutput()){output.Write(reply,0,reply.Length);output.Flush();}return 0;
  }catch{return 2;}finally{if(bytes!=null)Array.Clear(bytes,0,bytes.Length);}}
  static string ReadPixels(byte[] input){var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Ocr");var assembly=Assembly.LoadFrom(Path.Combine(root,"Tesseract.dll"));assembly.GetType("Tesseract.TesseractEnviornment",true).GetProperty("CustomSearchPath").SetValue(null,root,null);
   var engineType=assembly.GetType("Tesseract.TesseractEngine",true);var pixType=assembly.GetType("Tesseract.Pix",true);var modeType=assembly.GetType("Tesseract.EngineMode",true);var pageMode=assembly.GetType("Tesseract.PageSegMode",true);
   using(var engine=(IDisposable)Activator.CreateInstance(engineType,new object[]{Path.Combine(root,"tessdata"),"chi_sim+eng",Enum.Parse(modeType,"LstmOnly")}))
   using(var pix=(IDisposable)pixType.GetMethod("LoadFromMemory").Invoke(null,new object[]{input})){
    var flag=engineType.GetMethod("SetVariable",new[]{typeof(string),typeof(bool)});if(flag!=null){flag.Invoke(engine,new object[]{"tessedit_write_images",false});flag.Invoke(engine,new object[]{"classify_enable_learning",false});}
    var process=engineType.GetMethod("Process",new[]{pixType,typeof(Nullable<>).MakeGenericType(pageMode)});string text="";foreach(var layout in new[]{"Auto","SparseText","SingleLine"}){using(var page=(IDisposable)process.Invoke(engine,new object[]{pix,Enum.Parse(pageMode,layout)})){text=(string)page.GetType().GetMethod("GetText").Invoke(page,null);}if(!String.IsNullOrWhiteSpace(text))break;}return text;
   }
  }
 }
}
