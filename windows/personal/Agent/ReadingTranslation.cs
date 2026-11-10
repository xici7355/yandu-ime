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
 }

 // UIA reads only the explicit selection. No Ctrl+C simulation or clipboard polling.
 internal sealed class SelectedText {
  public string Text;public IntPtr Foreground;public Point Anchor;
  public static SelectedText Read(IntPtr window,Point point){
   try {
    if(window==IntPtr.Zero||Native.GetForegroundWindow()!=window)return null;
    uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid==(uint)Process.GetCurrentProcess().Id)return null;
    var element=AutomationElement.FocusedElement;if(element!=null&&element.Current.IsPassword)return null;
    var selected=ReadElement(element,pid);
    if(selected==null){element=AutomationElement.FromPoint(new System.Windows.Point(point.X,point.Y));for(int i=0;i<6&&element!=null&&selected==null;i++){selected=ReadElement(element,pid);element=TreeWalker.ControlViewWalker.GetParent(element);}}
    if(selected==null||Native.GetForegroundWindow()!=window)return null;
    return new SelectedText{Text=selected,Foreground=window,Anchor=point};
   }catch{return null;}
  }
  static string ReadElement(AutomationElement element,uint pid){
   if(element==null||element.Current.ProcessId!=pid||element.Current.IsPassword)return null;
   object pattern;if(!element.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return null;
   var ranges=((TextPattern)pattern).GetSelection();if(ranges.Length!=1)return null;
   var value=ranges[0].GetText(ReadingPolicy.MaxCharacters+1);
   return ReadingPolicy.ValidText(value)?value:null;
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

 internal sealed class ReadingResult : Form {
  readonly Settings config;readonly Translator translator;readonly Func<CancellationToken,Task<string>> recognize;
  readonly CancellationTokenSource lifetime=new CancellationTokenSource();CancellationTokenSource request;
  readonly TextBox original=new TextBox(),translation=new TextBox();readonly Label status=new Label(),targetLabel=new Label();readonly Button retry=new SoftButton(),copyOriginal=new SoftButton(),copyTranslation=new SoftButton();
  internal int RequestTimeoutMilliseconds=35000;
  public ReadingResult(Settings settings,Translator engine,string text,Func<CancellationToken,Task<string>> ocr){
   config=settings;translator=engine;recognize=ocr;Text=ocr==null?"言渡 · 划词翻译":"言渡 · 截屏翻译";Icon=Brand.Icon;StartPosition=FormStartPosition.CenterScreen;Size=new Size(780,650);MinimumSize=new Size(560,480);BackColor=Palette.Background;ForeColor=Palette.Text;Font=new Font("Microsoft YaHei UI",10);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=7};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(var h in new[]{48f,28f,0f,28f,0f,60f,48f})layout.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?50:h));Controls.Add(layout);
   var heading=new Label{Text=Text,Font=new Font(Font.FontFamily,17,FontStyle.Bold),Dock=DockStyle.Fill,ForeColor=Palette.Text};layout.Controls.Add(heading,0,0);layout.Controls.Add(new Label{Text=ocr==null?"原文":"识别原文（可修正后重新翻译）",Dock=DockStyle.Fill},0,1);
   Configure(original);original.ReadOnly=ocr==null;original.Text=text??"";layout.Controls.Add(original,0,2);targetLabel.Text=Translator.ReadingTargetChinese(original.Text)?"中文译文":"英文译文";targetLabel.Dock=DockStyle.Fill;targetLabel.Padding=new Padding(0,6,0,0);layout.Controls.Add(targetLabel,0,3);Configure(translation);translation.ReadOnly=true;layout.Controls.Add(translation,0,4);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0),WrapContents=true};layout.Controls.Add(footer,0,5);
   retry.Text="重新翻译";copyOriginal.Text="复制原文";copyTranslation.Text="复制译文";foreach(var button in new[]{retry,copyOriginal,copyTranslation}){button.Size=new Size(115,34);button.BackColor=Color.FromArgb(174,255,50);button.ForeColor=Color.Black;footer.Controls.Add(button);}status.Dock=DockStyle.Fill;status.ForeColor=Palette.Secondary;layout.Controls.Add(status,0,6);
   copyOriginal.Click+=delegate{Copy(original.Text);};copyTranslation.Click+=delegate{Copy(translation.Text);};retry.Click+=async delegate{await Run(false);};Shown+=async delegate{await Run(recognize!=null);};FormClosed+=delegate{lifetime.Cancel();if(request!=null)request.Cancel();};
  }
  void Configure(TextBox box){box.Multiline=true;box.ScrollBars=ScrollBars.Vertical;box.Dock=DockStyle.Fill;box.BackColor=Palette.Surface;box.ForeColor=Palette.Text;box.BorderStyle=BorderStyle.FixedSingle;box.Font=new Font("Microsoft YaHei UI",11);box.MaxLength=ReadingPolicy.MaxCharacters;}
  void Copy(string value){if(String.IsNullOrEmpty(value))return;try{Clipboard.SetText(value);status.Text="已复制";}catch(ExternalException){status.Text="剪贴板正忙，请重试。";}}
  async Task Run(bool withOcr){
   if(request!=null)return;var active=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);request=active;active.CancelAfter(RequestTimeoutMilliseconds);retry.Enabled=copyTranslation.Enabled=false;translation.Text="";
   try{if(withOcr){status.Text="正在本机识别文字……";original.Text=await recognize(active.Token);}
    active.Token.ThrowIfCancellationRequested();if(!ReadingPolicy.ValidText(original.Text))throw new InvalidOperationException(String.IsNullOrWhiteSpace(original.Text)?"没有识别到文字，请重新框选清晰的文字区域。":"文字超过 12000 字，请缩小范围。");
    if(!config.Ready)throw new InvalidOperationException("翻译已暂停或尚未配置 API，请在言渡设置的翻译页配置。原文仍可复制。");
    targetLabel.Text=Translator.ReadingTargetChinese(original.Text)?"中文译文":"英文译文";status.Text=Translator.ReadingTargetChinese(original.Text)?"英 → 中 · 正在翻译……":"中 → 英 · 正在翻译……";var result=await translator.TranslateReading(config,original.Text,active.Token);active.Token.ThrowIfCancellationRequested();if(!IsDisposed){translation.Text=result;status.Text="翻译完成";copyTranslation.Enabled=true;}
   }catch(OperationCanceledException){if(!IsDisposed)status.Text="操作已取消或超时，可重试。";}
   catch(Exception ex){if(!IsDisposed)status.Text=ex is System.Net.Http.HttpRequestException?"无法连接翻译接口，请检查网络后重试。":ex.Message;}
   finally{request=null;active.Dispose();if(!IsDisposed)retry.Enabled=true;}
  }
 }

 internal static class LocalOcr {
  public static async Task<string> Recognize(Bitmap image,CancellationToken token){
   string folder=Path.Combine(Settings.Folder,"CaptureTemp",Guid.NewGuid().ToString("N"));string input=Path.Combine(folder,"capture.png"),output=Path.Combine(folder,"text.txt");
   try{using(image){token.ThrowIfCancellationRequested();Directory.CreateDirectory(folder);SaveCapture(image,input);}using(var process=Process.Start(new ProcessStartInfo{FileName=Application.ExecutablePath,Arguments="--ocr-worker \""+input+"\" \""+output+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){
     try{while(!process.HasExited){await Task.Delay(100,token);}token.ThrowIfCancellationRequested();if(process.ExitCode!=0||!File.Exists(output))throw new InvalidOperationException("本机文字识别失败，请重新框选；如仍失败请重新安装完整安装包。");return File.ReadAllText(output,System.Text.Encoding.UTF8).Trim();}
     finally{try{if(!process.HasExited){process.Kill();process.WaitForExit(2000);}}catch{}}
    }
   }finally{try{Directory.Delete(folder,true);}catch{}}
  }
  static void SaveCapture(Bitmap source,string path){
   // Enlarge ordinary screen text before OCR, while bounding worker memory.
   if((long)source.Width*source.Height<=2000000){using(var scaled=new Bitmap(source.Width*2,source.Height*2,PixelFormat.Format24bppRgb)){using(var graphics=Graphics.FromImage(scaled)){graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(source,new Rectangle(0,0,scaled.Width,scaled.Height));}scaled.Save(path,ImageFormat.Png);}}
   else source.Save(path,ImageFormat.Png);
  }
  // Separate worker bounds OCR failures and cancellation; it never uses the network.
  public static int Worker(string input,string output){
   try{var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Ocr");var assembly=Assembly.LoadFrom(Path.Combine(root,"Tesseract.dll"));assembly.GetType("Tesseract.TesseractEnviornment",true).GetProperty("CustomSearchPath").SetValue(null,root,null);
    var engineType=assembly.GetType("Tesseract.TesseractEngine",true);var pixType=assembly.GetType("Tesseract.Pix",true);var modeType=assembly.GetType("Tesseract.EngineMode",true);var pageMode=assembly.GetType("Tesseract.PageSegMode",true);
    using(var engine=(IDisposable)Activator.CreateInstance(engineType,new object[]{Path.Combine(root,"tessdata"),"chi_sim+eng",Enum.Parse(modeType,"LstmOnly")}))
    using(var pix=(IDisposable)pixType.GetMethod("LoadFromMemory").Invoke(null,new object[]{File.ReadAllBytes(input)})){
     var process=engineType.GetMethod("Process",new[]{pixType,typeof(Nullable<>).MakeGenericType(pageMode)});
     string text="";foreach(var layout in new[]{"Auto","SparseText","SingleLine"}){using(var page=(IDisposable)process.Invoke(engine,new object[]{pix,Enum.Parse(pageMode,layout)})){text=(string)page.GetType().GetMethod("GetText").Invoke(page,null);}if(!String.IsNullOrWhiteSpace(text))break;}File.WriteAllText(output,text,new System.Text.UTF8Encoding(false));
    }return 0;
   }catch{return 2;}
  }
 }
}
