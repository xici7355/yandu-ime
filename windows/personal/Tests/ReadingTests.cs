using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Automation;
using PersonalIme;

class ReadingTests {
 static int checks;
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);checks++;}
 static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}}
 static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);}
 static void Until(Func<bool> ready,int timeout){var end=DateTime.UtcNow.AddMilliseconds(timeout);while(!ready()&&DateTime.UtcNow<end)Pump(30);Check(ready(),"asynchronous operation completes within bound");}
 [DllImport("user32.dll")]static extern uint GetClipboardSequenceNumber();
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 delegate bool WindowVisitor(IntPtr window,IntPtr data);
 [DllImport("user32.dll")]static extern bool EnumWindows(WindowVisitor visitor,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
 [STAThread]static int Main(string[] args){
  if(args.Length==3&&args[0]=="--ocr-worker")return LocalOcr.Worker(args[1],args[2]);
  if(args.Length==1&&args[0]=="--selection-host"){Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.Run(new Fixture());return 0;}
  try{Native.SetProcessDPIAware();Application.EnableVisualStyles();if(args.Length==1&&args[0]=="--browser")Browser();else Run();Console.WriteLine("OK "+checks+" reading-translation checks");return 0;}catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}
 }
 static void Run(){
  Check(!ReadingPolicy.ValidText("   ")&&!ReadingPolicy.ValidText(new string('a',12001)),"empty and oversized text rejected");
  Check(ReadingPolicy.ValidText("你好，世界"),"selected Chinese text accepted");
  var r=ReadingPolicy.Selection(new Point(500,400),new Point(-300,-200),new Rectangle(-1920,-1080,3840,2160));Check(r==new Rectangle(-300,-200,800,600),"reverse drag and negative monitor coordinates");
  Check(!ReadingPolicy.ValidCapture(new Rectangle(0,0,0,50))&&!ReadingPolicy.ValidCapture(new Rectangle(0,0,6000,5000)),"empty and excessive captures rejected");
  Capture();Hotkey();Ocr();Results();Selection();
 }
 static void Capture(){
  using(var bitmap=new Bitmap(640,360)){using(var g=Graphics.FromImage(bitmap))g.Clear(Color.White);
   using(var overlay=new CaptureOverlay(bitmap,new Rectangle(0,0,640,360))){overlay.Show();Pump(50);
    typeof(CaptureOverlay).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,30,30,0)});
    typeof(CaptureOverlay).GetMethod("OnMouseUp",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,530,200,0)});
    Check(overlay.Captured!=null&&overlay.Captured.Size==new Size(500,170),"capture crops only the selected rectangle");overlay.Captured.Dispose();
   }
   using(var cancel=new CaptureOverlay(bitmap,new Rectangle(0,0,640,360))){cancel.Show();typeof(Control).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(cancel,new object[]{new KeyEventArgs(Keys.Escape)});Check(cancel.Captured==null&&!cancel.Visible,"Escape cancels without producing a capture");}
   using(var cancel=new CaptureOverlay(bitmap,new Rectangle(0,0,640,360))){cancel.Show();typeof(CaptureOverlay).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(cancel,new object[]{new MouseEventArgs(MouseButtons.Right,1,30,30,0)});Check(cancel.Captured==null&&!cancel.Visible,"right click cancels capture");}
  }
 }
 class HotkeyWindow:Form {public bool Received;protected override void WndProc(ref Message m){if(m.Msg==Native.WM_HOTKEY&&m.WParam.ToInt32()==2201){Received=true;return;}base.WndProc(ref m);}}
 static void Hotkey(){using(var window=new HotkeyWindow()){
   bool registered=Native.RegisterHotKey(window.Handle,2201,0x4003,(uint)Keys.Z);Check(registered,"Ctrl Alt Z available and registered without keyboard hook");
   try{using(var other=new Form()){Check(!Native.RegisterHotKey(other.Handle,2202,0x4003,(uint)Keys.Z),"conflicting registration refused instead of replacing owner");}
    keybd_event(0x11,0,0,UIntPtr.Zero);keybd_event(0x12,0,0,UIntPtr.Zero);keybd_event(0x5a,0,0,UIntPtr.Zero);keybd_event(0x5a,0,2,UIntPtr.Zero);keybd_event(0x12,0,2,UIntPtr.Zero);keybd_event(0x11,0,2,UIntPtr.Zero);Until(()=>window.Received,1500);Check(window.Received,"actual Ctrl Alt Z delivers screenshot hotkey event");
   }finally{Native.UnregisterHotKey(window.Handle,2201);}
  }}
 static void Ocr(){
  var image=new Bitmap(1200,400);using(var g=Graphics.FromImage(image)){g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;using(var font=new Font("Microsoft YaHei UI",30))g.DrawString("你好，世界\n今天下午三点开会。\nYandu translation 123",font,Brushes.Black,30,30);}
  var task=LocalOcr.Recognize(image,CancellationToken.None);Until(()=>task.IsCompleted,30000);var text=task.GetAwaiter().GetResult();
  Check(text.Contains("你好")&&text.Contains("世界")&&text.Contains("下午")&&text.Contains("Yandu"),"actual offline OCR recognizes Chinese and English pixels");
  foreach(bool dark in new[]{false,true}){var small=new Bitmap(1000,260);using(var g=Graphics.FromImage(small)){g.Clear(dark?Color.FromArgb(28,28,28):Color.White);using(var font=new Font("Microsoft YaHei UI",16))g.DrawString("你好，世界\n今天下午三点开会。\nYandu translation 123",font,dark?Brushes.White:Brushes.Black,25,25);}var reading=LocalOcr.Recognize(small,CancellationToken.None);Until(()=>reading.IsCompleted,20000);var words=reading.GetAwaiter().GetResult();Check(words.Contains("你好")&&words.Contains("下午")&&words.Contains("Yandu"),dark?"small light text recognized on dark screenshot":"ordinary screen-sized Chinese and English recognized");}
  var canceled=new CancellationTokenSource();canceled.Cancel();using(var other=new Bitmap(100,50)){var pending=LocalOcr.Recognize(other,canceled.Token);Until(()=>pending.IsCompleted,2000);Check(pending.IsCanceled,"canceled OCR does not start work");}
  using(var cancel=new CancellationTokenSource()){var pending=LocalOcr.Recognize(new Bitmap(2400,1200),cancel.Token);cancel.Cancel();Until(()=>pending.IsCompleted,3000);Check(pending.IsCanceled,"active OCR worker is canceled and joined");}
  var folder=Path.Combine(Settings.Folder,"CaptureTemp");Check(!Directory.Exists(folder)||Directory.GetDirectories(folder).Length==0,"temporary capture files removed after success and cancellation");
 }
 static void Results(){
  var config=new Settings();config.SetKey("fixture-only-key");var handler=new MockHandler();using(var engine=new Translator(handler))using(var result=new ReadingResult(config,engine,"你好",null)){
   result.Show();Until(()=>Field<TextBox>(result,"translation").Text.Length>0,3000);Check(handler.Calls==1,"explicit result action makes one translation request");
   Check(Field<TextBox>(result,"original").Text=="你好"&&Field<TextBox>(result,"translation").Text=="Hello.","original and English shown separately");
   Check(Field<Button>(result,"copyOriginal").Enabled&&Field<Button>(result,"copyTranslation").Enabled,"copy actions enabled after translation");
   var copy=Field<Button>(result,"copyTranslation");Check(copy.Bottom<=copy.Parent.ClientSize.Height,"copy buttons fit the footer without clipping");
   Check(!All(result).Any(c=>c.Text=="朗读"||c.Text=="暂停"),"result contains no speech control");
   using(var picture=new Bitmap(result.Width,result.Height)){result.DrawToBitmap(picture,new Rectangle(Point.Empty,result.Size));picture.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reading-result.png"));}result.Close();
  }
  using(var engine=new Translator(new MockHandler()))using(var result=new ReadingResult(new Settings(),engine,"",token=>Task.FromResult("识别原文"))){result.Show();Until(()=>Field<Label>(result,"status").Text.Contains("尚未配置"),2000);Check(Field<TextBox>(result,"original").Text=="识别原文","OCR remains copyable without API");Check(!Field<TextBox>(result,"original").ReadOnly,"OCR errors can be corrected before retry");Check(!Field<Button>(result,"copyTranslation").Enabled,"failed translation cannot copy stale result");result.Close();}
  var slow=new DelayedHandler();using(var engine=new Translator(slow))using(var result=new ReadingResult(config,engine,"超时检查",null)){result.RequestTimeoutMilliseconds=100;result.Show();Until(()=>Field<Label>(result,"status").Text.Contains("超时"),2000);Check(slow.Canceled&&Field<Button>(result,"retry").Enabled,"request timeout cancels HTTP and permits retry");result.Close();}
  slow=new DelayedHandler();using(var engine=new Translator(slow))using(var result=new ReadingResult(config,engine,"关闭检查",null)){result.Show();Pump(50);result.Close();Until(()=>slow.Canceled,2000);Check(slow.Canceled,"closing result cancels active HTTP request");}
 }
 class DelayedHandler:System.Net.Http.HttpMessageHandler {public bool Canceled;protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage message,CancellationToken token){try{await Task.Delay(10000,token);return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK);}catch(OperationCanceledException){Canceled=true;throw;}}}
 static System.Collections.Generic.IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in All(c))yield return child;}
 static void Selection(){
  using(var process=Process.Start(new ProcessStartInfo{FileName=Application.ExecutablePath,Arguments="--selection-host",UseShellExecute=false})){
   try{IntPtr host=IntPtr.Zero;Until(()=>{host=Native.FindWindow(null,"Yandu isolated selection fixture");return host!=IntPtr.Zero;},5000);SetForegroundWindow(host);SetCursorPos(500,165);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Native.PostMessage(host,0x8000+700,IntPtr.Zero,IntPtr.Zero);Pump(250);Check(Native.GetForegroundWindow()==host,"selection fixture foreground verified");uint clipboard=GetClipboardSequenceNumber();
    var read=Task.Run(()=>SelectedText.Read(host,new Point(500,250)));Until(()=>read.IsCompleted,4000);Check(read.Result!=null&&read.Result.Text=="你好，世界","UIA reads selection from separate application");Check(GetClipboardSequenceNumber()==clipboard,"selection leaves clipboard unchanged");
    using(var owner=new Form()){var handle=owner.Handle;int actions=0;using(var monitor=new SelectionMonitor(owner,()=>true,value=>actions++)){
     Check(monitor.Available,"nonblocking mouse monitor registered");SetCursorPos(500,165);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Native.PostMessage(host,0x8000+700,IntPtr.Zero,IntPtr.Zero);
     var badge=Field<TranslateBadge>(monitor,"badge");Until(()=>badge.Visible,4000);Check(actions==0,"selecting text does not send translation request");Check(Native.GetForegroundWindow()==host,"translation badge preserves source focus");
     typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(badge.Controls[0],new object[]{EventArgs.Empty});Until(()=>actions==1,4000);Check(!badge.Visible,"explicit badge click dispatches once and hides badge");
     SetCursorPos(500,165);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Native.PostMessage(host,0x8000+700,IntPtr.Zero,IntPtr.Zero);Until(()=>badge.Visible,4000);Native.PostMessage(host,0x8000+701,IntPtr.Zero,IntPtr.Zero);Pump(120);
     typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(badge.Controls[0],new object[]{EventArgs.Empty});Pump(500);Check(actions==1,"stale selection is rejected when password field gains focus");
    }}
    Native.PostMessage(host,0x8000+701,IntPtr.Zero,IntPtr.Zero);Pump(150);var secret=Task.Run(()=>SelectedText.Read(host,new Point(500,250)));Until(()=>secret.IsCompleted,4000);Check(secret.Result==null,"password field is never read");
   }finally{if(!process.HasExited){process.CloseMainWindow();if(!process.WaitForExit(1500))process.Kill();}}
  }
 }
 static void Browser(){
  string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reading-browser-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string title="Yandu isolated reading "+Guid.NewGuid().ToString("N"),page=Path.Combine(root,"fixture.html");
  File.WriteAllText(page,"<!doctype html><meta charset='utf-8'><title>"+title+"</title><body style='font:30px sans-serif;padding:50px'><p>你好，世界。Yandu browser selection.</p><input type='password' value='fixture-only' style='margin-top:40px'></body>",System.Text.Encoding.UTF8);
  IntPtr browser=IntPtr.Zero;using(var process=Process.Start(new ProcessStartInfo{FileName=@"C:\Program Files\Google\Chrome\Application\chrome.exe",Arguments="--user-data-dir=\""+Path.Combine(root,"profile")+"\" --no-first-run --no-default-browser-check --disable-background-networking --force-renderer-accessibility --new-window --window-position=180,120 --window-size=900,650 \""+new Uri(page).AbsoluteUri+"\"",UseShellExecute=false})){
   try{Until(()=>{EnumWindows(delegate(IntPtr window,IntPtr data){var text=new System.Text.StringBuilder(512);GetWindowText(window,text,512);if(text.ToString().Contains(title)&&Native.IsWindowVisible(window))browser=window;return true;},IntPtr.Zero);return browser!=IntPtr.Zero;},15000);
    SetForegroundWindow(browser);Pump(500);AutomationElement doc=null;Until(()=>{doc=AutomationElement.FromHandle(browser).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));return doc!=null;},10000);
    var rectangle=doc.Current.BoundingRectangle;var point=new Point((int)rectangle.Left+80,(int)rectangle.Top+80);SetCursorPos(point.X,point.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(150);Check(Native.GetForegroundWindow()==browser,"isolated browser foreground verified");
    keybd_event(0x11,0,0,UIntPtr.Zero);keybd_event(0x41,0,0,UIntPtr.Zero);keybd_event(0x41,0,2,UIntPtr.Zero);keybd_event(0x11,0,2,UIntPtr.Zero);Pump(300);
    var selected=Task.Run(()=>SelectedText.Read(browser,point));Until(()=>selected.IsCompleted,5000);Check(selected.Result!=null&&selected.Result.Text.Contains("你好，世界")&&selected.Result.Text.Contains("Yandu"),"browser document selection available through UIA");
    Check(!selected.Result.Text.Contains("fixture-only"),"browser selection excludes password value");
   }finally{if(browser!=IntPtr.Zero)Native.PostMessage(browser,0x10,IntPtr.Zero,IntPtr.Zero);}
  }
 }
 class Fixture:Form {
  public readonly RichTextBox Editor=new RichTextBox{Dock=DockStyle.Fill,Text="你好，世界 其他内容",Font=new Font("Microsoft YaHei UI",18)};
  readonly TextBox secret=new TextBox{Dock=DockStyle.Top,UseSystemPasswordChar=true,Text="fixture-secret"};
  public Fixture(){Text="Yandu isolated selection fixture";StartPosition=FormStartPosition.Manual;TopMost=true;Bounds=new Rectangle(250,150,650,300);Controls.Add(Editor);Controls.Add(secret);Shown+=delegate{Activate();Editor.Focus();Editor.Select(0,5);};}
  protected override void WndProc(ref Message m){if(m.Msg==0x8000+700){Editor.Focus();Editor.Select(0,5);return;}if(m.Msg==0x8000+701){secret.Focus();secret.SelectAll();return;}base.WndProc(ref m);}
 }
}
