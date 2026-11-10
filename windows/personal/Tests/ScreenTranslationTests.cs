using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonalIme;

class ScreenTranslationTests {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);}
 static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(2);}}
 static void Wait(Func<bool> ready,int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(!ready()&&DateTime.UtcNow<until)Pump(10);Check(ready(),"operation completes within bound");}
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [STAThread]static int Main(string[] args){if(args.Length==1&&args[0]=="--ocr-worker-stdio")return LocalOcr.Worker();try{Native.SetProcessDPIAware();Application.EnableVisualStyles();if(args.Length==1&&args[0]=="--live")Live();else if(args.Length==1&&args[0]=="--live-auto")LiveAuto();else{Geometry();if(args.Length==0||args[0]!="--no-mouse")Capture();else Console.WriteLine("SKIP repeated mouse-capture test; verified independently");Surface();SelectedSurface();Direction().GetAwaiter().GetResult();}Console.WriteLine("OK "+checks+" screenshot overlay checks");return 0;}catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}}
 static void SelectedSurface(){
  var bounds=SystemInformation.VirtualScreen;var work=Screen.PrimaryScreen.WorkingArea;var region=new Rectangle(work.Left+160,work.Top+330,140,28);var config=new Settings();config.SetKey("fixture-only-key");int ocrCalls=0;
  using(var engine=new Translator(new DirectionHandler()))using(var window=new ScreenTranslation(config,engine,Fixture(bounds,region),bounds,region,null,token=>{ocrCalls++;throw new Exception("selection must not use OCR");},"Hello, world.")){
   int forms=Application.OpenForms.Count;window.Show();Wait(()=>Field<bool>(window,"ready"),2000);var local=region;local.Offset(-bounds.Left,-bounds.Top);var card=Field<Panel>(window,"card");
   Check(ocrCalls==0&&window.Text=="言渡 · 划词翻译","selection directly uses text in the shared overlay");Check(card.Bounds==local&&Field<TextBox>(window,"english").Height>=16,"word-sized selection displays readable translated text over original");Check(Application.OpenForms.Count==forms+1&&!window.ShowInTaskbar,"selection creates no extra result window or taskbar entry");
   Save(window,"selection-cover.png");Field<CheckBox>(window,"compare").Checked=true;Pump(30);Check(!card.Bounds.IntersectsWith(local),"selection comparison moves beside original text");Save(window,"selection-compare.png");window.Close();
  }
  using(var engine=new Translator(new MockHandler()))using(var window=new ScreenTranslation(config,engine,Fixture(bounds,region),bounds,region,null,null,"你好",false)){
   window.Show();Wait(()=>Field<bool>(window,"ready"),2000);Check(Field<CheckBox>(window,"compare").Checked&&!Field<CheckBox>(window,"compare").Enabled&&Field<Label>(window,"status").Text.Contains("无法定位原文"),"missing selection geometry safely uses comparison only");window.Close();
  }
  Check(typeof(Agent).Assembly.GetType("PersonalIme.ReadingResult")==null,"obsolete standalone reading window removed from application");
 }
 static void Geometry(){var work=new Rectangle(-1920,-100,1920,1040);foreach(var region in new[]{new Rectangle(-1800,-80,400,80),new Rectangle(-1800,820,400,80),new Rectangle(-1200,400,600,150)}){var card=ScreenTranslationLayout.Beside(region,new Size(500,220),work);Check(work.Contains(card),"comparison fits monitor including negative origin");Check(!region.IntersectsWith(card),"comparison uses available space outside selected pixels");}}
 static void Capture(){var bounds=SystemInformation.VirtualScreen;using(var bitmap=new Bitmap(bounds.Width,bounds.Height))using(var overlay=new CaptureOverlay(bitmap,bounds)){
   overlay.Show();overlay.Activate();Pump(60);var work=Screen.PrimaryScreen.WorkingArea;var start=new Point(work.Left+120,work.Top+170);var end=new Point(start.X+350,start.Y+110);
   SetCursorPos(start.X,start.Y);Pump(15);mouse_event(2,0,0,0,UIntPtr.Zero);Pump(10);
   for(int i=1;i<=25;i++){SetCursorPos(start.X+(end.X-start.X)*i/25,start.Y+(end.Y-start.Y)*i/25);Pump(3);}
   SetCursorPos(end.X,end.Y);Pump(8);var timer=System.Diagnostics.Stopwatch.StartNew();mouse_event(4,0,0,0,UIntPtr.Zero);SetCursorPos(end.X+180,end.Y+160);Wait(()=>!overlay.Visible,1000);timer.Stop();
   Check(overlay.ScreenRegion==new Rectangle(start.X,start.Y,350,110),"release fixes selection before subsequent pointer motion");Check(timer.ElapsedMilliseconds<600,"mouse release completes without a repaint backlog");Console.WriteLine("release_latency_ms="+timer.ElapsedMilliseconds);
   var frozen=overlay.ScreenRegion;typeof(CaptureOverlay).GetMethod("OnMouseMove",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,0,900,700,0)});Check(overlay.ScreenRegion==frozen,"queued moves cannot alter a completed selection");overlay.Captured.Dispose();
  }}
 static void Surface(){var bounds=SystemInformation.VirtualScreen;var work=Screen.PrimaryScreen.WorkingArea;var region=new Rectangle(work.Left+160,work.Top+300,520,100);var config=new Settings();config.SetKey("fixture-only-key");var handler=new MockHandler();int saves=0;
  using(var engine=new Translator(handler))using(var window=new ScreenTranslation(config,engine,Fixture(bounds,region),bounds,region,value=>saves++,token=>Task.FromResult("你好，世界。今天下午三点开会。"))){int forms=Application.OpenForms.Count;window.Show();Wait(()=>Field<bool>(window,"ready"),3000);
   var card=Field<Panel>(window,"card");var english=Field<TextBox>(window,"english");var compare=Field<CheckBox>(window,"compare");var local=region;local.Offset(-bounds.Left,-bounds.Top);
   Check(card.Bounds==local,"comparison off covers selected source rectangle in the screenshot");Check(english.Text=="Hello."&&!Translator.HasChinese(english.Text),"English translation rendered on screenshot");Check(!window.ShowInTaskbar&&window.FormBorderStyle==FormBorderStyle.None,"result has no independent taskbar or centered dialog");Check(Application.OpenForms.Count==forms+1,"screenshot result creates only one overlay surface");
   Save(window,"screenshot-cover.png");compare.Checked=true;Pump(80);var screenCard=card.Bounds;screenCard.Offset(bounds.Left,bounds.Top);Check(work.Contains(screenCard)&&!screenCard.IntersectsWith(region),"comparison on puts full translation beside the original");Check(!card.Bounds.IntersectsWith(Field<Panel>(window,"toolbar").Bounds),"toolbar does not cover comparison text");Check(saves==1,"comparison preference changes only on user toggle");Save(window,"screenshot-compare.png");
   Check(Field<Button>(window,"copy").Enabled&&Field<Button>(window,"copySource").Enabled,"English and source copy actions enabled");window.Close();
  }
  using(var engine=new Translator(new MockHandler()))using(var window=new ScreenTranslation(new Settings(),engine,Fixture(bounds,region),bounds,region,null,token=>Task.FromResult("你好，世界"))){window.Show();Wait(()=>Field<TextBox>(window,"english").Text.Contains("尚未配置"),2000);Check(!Field<Button>(window,"copy").Enabled&&Field<Button>(window,"copySource").Enabled,"API error stays visible on screenshot without pretending to translate");window.Close();}
  using(var engine=new Translator(new DirectionHandler()))using(var window=new ScreenTranslation(config,engine,Fixture(bounds,region),bounds,region,null,token=>Task.FromResult("Hello, world."))){window.Show();Wait(()=>Field<bool>(window,"ready"),2000);Check(Translator.HasChinese(Field<TextBox>(window,"english").Text)&&Field<Label>(window,"status").Text.Contains("英 → 中"),"English screenshot result visibly switches to Chinese");Check(Field<Button>(window,"copy").Text=="复制译文","copy action does not hard-code English target");window.Close();}
  string longText="";for(int i=0;i<300;i++)longText+="The complete translation remains readable beside the original screenshot. ";var longHandler=new MockHandler{Reply=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{choices=new[]{new{message=new{content=longText}}}})};
  using(var engine=new Translator(longHandler))using(var window=new ScreenTranslation(new Settings{ProtectedKey=config.ProtectedKey,CaptureComparison=true},engine,Fixture(bounds,region),bounds,region,null,token=>Task.FromResult("长段落"))){window.Show();Wait(()=>Field<bool>(window,"ready"),2000);var card=Field<Panel>(window,"card");var screenCard=card.Bounds;screenCard.Offset(bounds.Left,bounds.Top);Check(work.Contains(screenCard)&&!screenCard.IntersectsWith(region),"long comparison fits available screen space without covering source");Check(!card.Bounds.IntersectsWith(Field<Panel>(window,"toolbar").Bounds),"toolbar does not obscure a long translation");Check(Field<TextBox>(window,"english").Text==longText.Trim()&&Field<TextBox>(window,"english").ScrollBars==ScrollBars.Vertical,"full long translation retained with scrolling");window.Close();}
 }
 static Bitmap Fixture(Rectangle bounds,Rectangle region){var image=new Bitmap(bounds.Width,bounds.Height);using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(225,229,222));var local=region;local.Offset(-bounds.Left,-bounds.Top);g.FillRectangle(Brushes.White,local);using(var font=new Font("Microsoft YaHei UI",18))g.DrawString("你好，世界。今天下午三点开会。",font,Brushes.Black,local.Left+12,local.Top+20);}return image;}
 static void Live(){string before=File.Exists(Settings.FilePath)?Convert.ToBase64String(File.ReadAllBytes(Settings.FilePath)):"";try{var config=Settings.Load();Check(config.Ready,"configured API available without exposing credentials");var bounds=SystemInformation.VirtualScreen;var work=Screen.PrimaryScreen.WorkingArea;var region=new Rectangle(work.Left+160,work.Top+300,520,100);
   using(var engine=new Translator())using(var window=new ScreenTranslation(config,engine,Fixture(bounds,region),bounds,region,null)){window.Show();Wait(()=>Field<bool>(window,"ready")||Field<Label>(window,"status").Text.Contains("未完成"),38000);Check(Field<bool>(window,"ready"),"real OCR to configured API completes in English");Check(Field<string>(window,"source").Contains("下午"),"actual screenshot Chinese pixels recognized");Check(!Translator.HasChinese(Field<string>(window,"translated")),"actual configured API output has English target language");Save(window,"live-screenshot-cover.png");Field<CheckBox>(window,"compare").Checked=true;Pump(40);Save(window,"live-screenshot-compare.png");window.Close();}
  }finally{Check(before==(File.Exists(Settings.FilePath)?Convert.ToBase64String(File.ReadAllBytes(Settings.FilePath)):""),"actual API configuration unchanged after live fixture");}}
 static void Save(Form window,string file){using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,window.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file));}}
 static void LiveAuto(){string before=File.Exists(Settings.FilePath)?Convert.ToBase64String(File.ReadAllBytes(Settings.FilePath)):"";try{var config=Settings.Load();Check(config.Ready,"configured API available without exposing credentials");using(var translator=new Translator())foreach(bool englishSource in new[]{false,true}){
    var image=new Bitmap(1100,200);using(var g=Graphics.FromImage(image)){g.Clear(Color.White);using(var font=new Font("Microsoft YaHei UI",22))g.DrawString(englishSource?"Hello, world. Meeting at three this afternoon.":"你好，世界。今天下午三点开会。",font,Brushes.Black,20,25);}
    using(var timeout=new CancellationTokenSource(35000)){var ocr=LocalOcr.Recognize(image,timeout.Token);Wait(()=>ocr.IsCompleted,20000);var source=ocr.GetAwaiter().GetResult();Check(Translator.ReadingTargetChinese(source)==englishSource,englishSource?"real OCR detects English source":"real OCR detects Chinese source");var request=translator.TranslateReading(config,source,timeout.Token);Wait(()=>request.IsCompleted,30000);var result=request.GetAwaiter().GetResult();Check(!String.IsNullOrWhiteSpace(result)&&Translator.HasChinese(result)==englishSource,englishSource?"real configured API translates English screenshot to Chinese":"real configured API translates Chinese screenshot to English");}
   }}finally{Check(before==(File.Exists(Settings.FilePath)?Convert.ToBase64String(File.ReadAllBytes(Settings.FilePath)):""),"actual API settings unchanged after bidirectional fixture");}}
 class DirectionHandler:HttpMessageHandler {public int Calls;public string LastBody;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;LastBody=await request.Content.ReadAsStringAsync();return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(LastBody.Contains("Simplified Chinese")?"{\"choices\":[{\"message\":{\"content\":\"你好，世界。\"}}]}":"{\"choices\":[{\"message\":{\"content\":\"Hello, world.\"}}]}")};}}
 static async Task Direction(){
  Check(Translator.ReadingTargetChinese("Hello, world."),"English source automatically targets Chinese");
  Check(!Translator.ReadingTargetChinese("你好，世界"),"Chinese source automatically targets English");
  Check(!Translator.ReadingTargetChinese("请检查 API 接口配置"),"Chinese sentence with technical English still targets English");
  var config=new Settings();config.SetKey("fixture-only-key");var handler=new DirectionHandler();using(var translator=new Translator(handler)){
   Check(Translator.HasChinese(await translator.TranslateReading(config,"Hello",CancellationToken.None)),"English reading request returns Chinese");
   Check(!Translator.HasChinese(await translator.Translate(config,"Hello",CancellationToken.None)),"ordinary input translator retains English target");
   Check(handler.Calls==2,"translation cache separates Chinese and English targets");
   await translator.TranslateReading(config,"Hello",CancellationToken.None);Check(handler.Calls==2,"automatic-direction result cache reused");
   Check(!Translator.HasChinese(await translator.TranslateReading(config,"你好世界",CancellationToken.None)),"Chinese reading request returns English");
   var value=await translator.TranslateReading(config,"Hello,",CancellationToken.None);Check(value.EndsWith("，")&&handler.Calls==3,"Chinese target uses Chinese trailing punctuation without repeat request");
  }
 }
}
