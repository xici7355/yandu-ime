using System;
using System.Drawing;
using System.Globalization;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace PersonalIme {
 internal sealed class ActionLabel : Label {
  public void InvokeMouse(MouseButtons button,int x,int y){OnMouseClick(new MouseEventArgs(button,1,x,y,0));if(button==MouseButtons.Left)OnClick(EventArgs.Empty);}
 }
 internal sealed class PanelForm : IDisposable {
  public readonly Label Original=new Label(),Hint=new Label();
  public readonly ActionLabel Translation=new ActionLabel(),Speaker=new ActionLabel(),ScrollUp=new ActionLabel(),ScrollDown=new ActionLabel();
  public bool UndoAvailable;
  readonly Popup window;
  readonly Label[] labels;
  readonly float scale=SystemScale();
  static float SystemScale(){using(var g=Graphics.FromHwnd(IntPtr.Zero))return Math.Max(1,g.DpiX/96f);}
  public IntPtr Handle {get{return window.Handle;}}
  public bool Visible {get{return Native.IsWindowVisible(Handle);}}
  public int Width {get{return (int)(530*scale);}} public int Height {get{return panelHeight>0?panelHeight:(int)(112*scale);}}
  public bool Expanded {get;private set;}
  public int ScrollOffset {get{return scrollOffset;}}
  int left,top,anchorX,anchorY,panelHeight,fullTextHeight,scrollOffset;
  sealed class Popup : NativeWindow {
   readonly PanelForm owner;
   public Popup(PanelForm panel){owner=panel;CreateHandle(new CreateParams {Caption="言渡 · 译文",Style=unchecked((int)0x80000000),ExStyle=0x08000000|0x80|0x8,Width=panel.Width,Height=panel.Height});}
   protected override void WndProc(ref Message message){
    if(message.Msg==0x21){message.Result=new IntPtr(3);return;}
    if(message.Msg==0x0f){owner.Paint();message.Result=IntPtr.Zero;return;}
    if(message.Msg==0x20a){owner.Scroll(-(short)((message.WParam.ToInt64()>>16)&0xffff)/120);message.Result=IntPtr.Zero;return;}
    if(message.Msg==0x202||message.Msg==0x205){int x=(short)(message.LParam.ToInt64()&0xffff),y=(short)((message.LParam.ToInt64()>>16)&0xffff);owner.ClickAt(x,y,message.Msg==0x202?MouseButtons.Left:MouseButtons.Right);return;}
    base.WndProc(ref message);
   }
  }
  public PanelForm(){
   Original.SetBounds(18,8,490,21);Original.Font=new Font("Microsoft YaHei UI",9);Original.ForeColor=Palette.Secondary;
   Translation.SetBounds(18,33,435,45);Translation.Font=new Font("Segoe UI",12);
   Speaker.SetBounds(469,34,45,40);Speaker.Text="🔊";Speaker.Font=new Font("Segoe UI Emoji",18);
   Hint.SetBounds(18,85,494,19);Hint.Font=new Font("Microsoft YaHei UI",8.5f);Hint.ForeColor=Palette.Secondary;
   ScrollUp.SetBounds(469,80,22,24);ScrollDown.SetBounds(493,80,22,24);ScrollUp.Text="↑";ScrollDown.Text="↓";ScrollUp.Font=ScrollDown.Font=new Font("Segoe UI",12);ScrollUp.ForeColor=ScrollDown.ForeColor=Palette.Secondary;ScrollUp.Visible=ScrollDown.Visible=false;ScrollUp.Click+=delegate{Scroll(-3);};ScrollDown.Click+=delegate{Scroll(3);};
   Translation.ForeColor=Speaker.ForeColor=Palette.Text;
   labels=new Label[]{Original,Translation,Speaker,Hint,ScrollUp,ScrollDown};
   foreach(var label in labels){var b=label.Bounds;label.Bounds=new Rectangle((int)(b.X*scale),(int)(b.Y*scale),(int)(b.Width*scale),(int)(b.Height*scale));}
   window=new Popup(this);
   var region=Native.CreateRoundRectRgn(0,0,Width+1,Height+1,(int)(12*scale),(int)(12*scale));
   if(region!=IntPtr.Zero&&!Native.SetWindowRgn(Handle,region,true))Native.DeleteObject(region);
   foreach(var label in labels){label.TextChanged+=delegate{Native.InvalidateRect(Handle,IntPtr.Zero,false);};label.VisibleChanged+=delegate{Native.InvalidateRect(Handle,IntPtr.Zero,false);};}
  }
  void Paint(){
   Native.PaintInfo paint;IntPtr hdc=Native.BeginPaint(Handle,out paint);
   try{using(var graphics=Graphics.FromHdc(hdc)){DrawSurface(graphics);}}finally{Native.EndPaint(Handle,ref paint);}
  }
  void DrawSurface(Graphics graphics){
    graphics.Clear(Palette.Surface);
    using(var pen=new Pen(Palette.Dark?Color.FromArgb(62,70,64):Color.FromArgb(236,240,235)))graphics.DrawLine(pen,18*scale,30*scale,Width-18*scale,30*scale);
    graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.LineLimit})
    foreach(var label in labels){if(!label.Visible)continue;if(label==Speaker){DrawSpeaker(graphics);continue;}using(var brush=new SolidBrush(label.ForeColor)){
     if(label==Translation&&Expanded){var saved=graphics.Save();graphics.SetClip(label.Bounds);using(var fullFormat=new StringFormat{Trimming=StringTrimming.None})graphics.DrawString(label.Text,label.Font,brush,new RectangleF(label.Left,label.Top-scrollOffset,label.Width,Math.Max(fullTextHeight,label.Height)),fullFormat);graphics.Restore(saved);}
     else graphics.DrawString(label.Text,label.Font,brush,label.Bounds,format);
    }}
    if(Expanded&&fullTextHeight>Translation.Height){using(var brush=new SolidBrush(Palette.Secondary)){float track=Translation.Height,thumb=Math.Max(18*scale,track*track/fullTextHeight);float y=Translation.Top+(track-thumb)*scrollOffset/Math.Max(1,fullTextHeight-Translation.Height);graphics.FillRectangle(brush,Translation.Right+5*scale,y,2*scale,thumb);}}
    using(var pen=new Pen(Palette.Dark?Color.FromArgb(62,70,64):Color.FromArgb(218,225,216)))graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
  }
  void DrawSpeaker(Graphics graphics){
   graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   bool active=Speaker.Text=="■";float x=Speaker.Left+(Speaker.Width-36*scale)/2,y=Speaker.Top+(Speaker.Height-36*scale)/2;
   var background=Palette.Dark?(active?Color.FromArgb(61,88,64):Color.FromArgb(43,54,46)):(active?Color.FromArgb(217,235,220):Color.FromArgb(239,246,240));
   var ink=Palette.Dark?Color.FromArgb(210,235,214):Color.FromArgb(58,98,68);
   using(var brush=new SolidBrush(background))using(var button=Rounded(new RectangleF(x,y,36*scale,36*scale),11*scale))graphics.FillPath(brush,button);
   using(var brush=new SolidBrush(ink)){
    if(active){using(var stop=Rounded(new RectangleF(x+13*scale,y+13*scale,10*scale,10*scale),2.5f*scale))graphics.FillPath(brush,stop);return;}
    var points=new[]{new PointF(x+9.5f*scale,y+15*scale),new PointF(x+13*scale,y+15*scale),new PointF(x+18*scale,y+11*scale),new PointF(x+18*scale,y+25*scale),new PointF(x+13*scale,y+21*scale),new PointF(x+9.5f*scale,y+21*scale)};
    graphics.FillPolygon(brush,points);
    using(var pen=new Pen(ink,1.5f*scale)){pen.StartCap=pen.EndCap=System.Drawing.Drawing2D.LineCap.Round;graphics.DrawArc(pen,x+17*scale,y+15*scale,5*scale,6*scale,-60,120);graphics.DrawArc(pen,x+17*scale,y+12*scale,10*scale,12*scale,-55,110);}
   }
  }
  static System.Drawing.Drawing2D.GraphicsPath Rounded(RectangleF r,float radius){var path=new System.Drawing.Drawing2D.GraphicsPath();float d=radius*2;path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;}
  public void SavePreview(string path){using(var bitmap=new Bitmap(Width,Height)){bitmap.SetResolution(96*scale,96*scale);using(var graphics=Graphics.FromImage(bitmap))DrawSurface(graphics);bitmap.Save(path);}}
  void ClickAt(int x,int y,MouseButtons button){foreach(var label in new[]{Translation,Speaker,ScrollUp,ScrollDown})if(label.Visible&&label.Bounds.Contains(x,y)){label.InvokeMouse(button,x,y);return;}}
  public void Hide(){Native.ShowWindow(Handle,0);Collapse();}
  public void ShowPassive(){Native.ShowWindow(Handle,4);Native.SetWindowPos(Handle,new IntPtr(-1),left,top,Width,Height,0x0010|0x0040);Native.InvalidateRect(Handle,IntPtr.Zero,false);}
  public void PositionAt(int x,int y){anchorX=x;anchorY=y;var area=Screen.FromPoint(new Point(x,y)).WorkingArea;left=Math.Max(area.Left,Math.Min(x,area.Right-Width));int target=y+35;if(target+Height>area.Bottom)target=y-Height-12;top=Math.Max(area.Top,Math.Min(target,area.Bottom-Height));Native.SetWindowPos(Handle,IntPtr.Zero,left,top,Width,Height,0x0010|0x0004);}
  public void ToggleExpanded(){Expanded=!Expanded;scrollOffset=0;LayoutTranslation();}
  public void Collapse(){if(!Expanded)return;Expanded=false;scrollOffset=0;LayoutTranslation();}
  void LayoutTranslation(){
   int textHeight=(int)(45*scale);
   if(Expanded){using(var graphics=Graphics.FromHwnd(Handle))using(var format=new StringFormat{Trimming=StringTrimming.None})fullTextHeight=(int)Math.Ceiling(graphics.MeasureString(Translation.Text,Translation.Font,new SizeF(Translation.Width,1000000),format).Height)+6;
    var area=Screen.FromPoint(new Point(anchorX,anchorY)).WorkingArea;
    int maximum=Math.Max((int)(45*scale),Math.Min((int)(420*scale),area.Height-(int)(110*scale)));
    textHeight=Math.Max((int)(45*scale),Math.Min(fullTextHeight,maximum));
   }
   Translation.Height=textHeight;Hint.Top=Expanded?Translation.Bottom+(int)(7*scale):(int)(85*scale);panelHeight=Expanded?Hint.Bottom+(int)(8*scale):(int)(112*scale);
   ScrollUp.Visible=ScrollDown.Visible=Expanded&&fullTextHeight>textHeight;
   var region=Native.CreateRoundRectRgn(0,0,Width+1,Height+1,(int)(12*scale),(int)(12*scale));if(region!=IntPtr.Zero&&!Native.SetWindowRgn(Handle,region,true))Native.DeleteObject(region);
   PositionAt(anchorX,anchorY);Native.InvalidateRect(Handle,IntPtr.Zero,false);
  }
  public void Scroll(int steps){if(!Expanded)return;scrollOffset=Math.Max(0,Math.Min(Math.Max(0,fullTextHeight-Translation.Height),scrollOffset+(int)(steps*48*scale)));Native.InvalidateRect(Handle,IntPtr.Zero,false);}
  public void Dispose(){window.DestroyHandle();foreach(var label in labels)label.Dispose();}
 }
 internal sealed class Agent : Form {
  sealed class TranslationJob {
   public IntPtr Target,Foreground;public string Core;public DateTime Started,Deadline;
   public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
  }
  readonly Settings config;readonly Translator translator;readonly PanelForm panel=new PanelForm();
  readonly SpeechSynthesizer speech=new SpeechSynthesizer();readonly NotifyIcon tray=new NotifyIcon();
  readonly System.Windows.Forms.Timer debounce=new System.Windows.Forms.Timer(),watch=new System.Windows.Forms.Timer();
  TranslationJob job;SettingsForm settingsWindow;Snapshot snapshot;
  SelectionMonitor selectionMonitor;ReadingResult readingWindow;bool capturing;
  ScreenTranslation screenTranslation;
  bool captureHotkey;
  IntPtr previewForeground,currentTarget,pendingTarget;DateTime lastSignal,pendingUntil;
  bool leased,composing,failed,speaking,applying,cleaned,pendingUndo;
  readonly System.Collections.Generic.List<object> diagnosticHistory=new System.Collections.Generic.List<object>();
  void Note(string state){diagnosticHistory.Add(new{time=DateTime.UtcNow.ToString("o"),state=state,nativeMode=snapshot==null?"none":snapshot.Mode,sourceCharacters=snapshot==null?0:snapshot.Source.Length,translationCharacters=translated.Length,requestActive=job!=null});while(diagnosticHistory.Count>32)diagnosticHistory.RemoveAt(0);}
  void ExportDiagnostic(){try{System.IO.Directory.CreateDirectory(Settings.Folder);var data=new{version=typeof(Agent).Assembly.GetName().Version.ToString(),configured=config.Ready,composing=composing,failed=failed,applying=applying,wechatAttached=wechatState!=null,hasNativeTarget=snapshot!=null,requestActive=job!=null,requestAgeMs=job==null?0:(int)(DateTime.UtcNow-job.Started).TotalMilliseconds,history=diagnosticHistory.ToArray()};System.IO.File.WriteAllText(System.IO.Path.Combine(Settings.Folder,"diagnostic-state.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(data));}catch{}}
    WeChatEditor.State wechatState;
  Task<WeChatEditor.State> wechatRead;
  DateTime wechatReadStarted, wechatNextRead;
  IntPtr wechatReadingTarget;
  bool wechatClearPending;
  void BeginWeChatRead(Snapshot value){
   if(wechatRead!=null||value.Mode=="uia")return;
   uint pid;Native.GetWindowThreadProcessId(value.Foreground,out pid);
   try{var name=System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;if(name!="Weixin"&&name!="WeChat")return;}catch{return;}
   var fg=value.Foreground;var identity=wechatState==null?null:wechatState.Identity;
   wechatReadingTarget=value.Target;wechatReadStarted=DateTime.UtcNow;
   wechatRead=Task.Run(()=>WeChatEditor.Read(fg,identity));
  }
  void CheckWeChat(){
   if(applying)return;
   if(wechatRead!=null){
    if(!wechatRead.IsCompleted){if((DateTime.UtcNow-wechatReadStarted).TotalSeconds>2)ResetTranslationReason("wechat_provider_timeout");return;}
    var pending=wechatRead;wechatRead=null;
    var state=pending.Status==TaskStatus.RanToCompletion?pending.Result:null;
    if(snapshot==null||snapshot.Target!=wechatReadingTarget)return;
    if(state==null||state.Foreground!=previewForeground||state.Text.Length==0){if(wechatState!=null||wechatClearPending)ResetTranslationReason("wechat_editor_changed");return;}
    if(wechatState==null&&state.Text!=snapshot.Source){if(wechatClearPending)ResetTranslationReason("wechat_source_mismatch");return;}
    wechatState=state;wechatClearPending=false;leased=false;wechatNextRead=DateTime.UtcNow.AddMilliseconds(300);
    if(!composing)Accept(new Snapshot{Revision=snapshot.Revision+1,Target=currentTarget,Foreground=state.Foreground,Source=state.Text,X=state.X,Y=state.Y,Mode="uia"});
   }
   if(wechatState!=null&&!applying&&wechatRead==null&&DateTime.UtcNow>=wechatNextRead&&snapshot!=null)BeginWeChatRead(new Snapshot{Target=currentTarget,Foreground=previewForeground,Mode="text"});
  }
  internal int TranslationTimeoutMilliseconds=20000;
  string translated="",translatedSource="",undoSource="",undoTarget="";
  IntPtr undoWindow;long pendingRevision;string pendingExpected,pendingReplacement;
  public Agent():this(Settings.Load(),new Translator(),false){}
  internal Agent(Settings settings,Translator engine,bool testing){
   config=settings;translator=engine;Text="PersonalTranslateIme.Agent.v1";ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.FixedToolWindow;WindowState=FormWindowState.Minimized;Opacity=0;
   var handle=Handle;
   panel.Translation.MouseClick+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right)Expand();else if(CanUndo())Apply(true);else if(failed)Retry();else Apply(false);};
   panel.Speaker.Click+=delegate{Speak();};
   speech.SpeakCompleted+=delegate{if(!cleaned&&!IsDisposed&&IsHandleCreated)try{BeginInvoke((Action)delegate{if(!cleaned){speaking=false;panel.Speaker.Text="🔊";}});}catch(InvalidOperationException){}};
   debounce.Interval=400;debounce.Tick+=async delegate{debounce.Stop();await TranslateCurrent();};
   watch.Interval=150;watch.Tick+=delegate{CheckContext();};watch.Start();
   var menu=new ContextMenuStrip();menu.Items.Add("言渡设置",null,delegate{OpenSettings();});menu.Items.Add("截屏翻译",null,delegate{BeginInvoke((Action)CaptureTranslation);});menu.Items.Add("划词翻译",null,delegate{config.SelectionTranslation=!config.SelectionTranslation;config.Save();});menu.Opening+=delegate{((ToolStripMenuItem)menu.Items[2]).Checked=config.SelectionTranslation;};menu.Items.Add("清空翻译缓存",null,delegate{translator.Clear();});menu.Items.Add("退出言渡助手",null,delegate{Close();});
   tray.Icon=Brand.Icon;tray.Text="言渡输入法 · 右键打开言渡设置";tray.ContextMenuStrip=menu;tray.Visible=!testing;tray.DoubleClick+=delegate{OpenSettings();};
   Shown+=delegate{Hide();if(!config.Ready)OpenSettings();};
   if(!testing){selectionMonitor=new SelectionMonitor(this,()=>config.SelectionTranslation&&!config.PauseTranslation&&!capturing,ShowSelectedTranslation);captureHotkey=Native.RegisterHotKey(Handle,1101,0x4003,(uint)Keys.Z);menu.Items[1].Text=captureHotkey?"截屏翻译（Ctrl＋Alt＋Z）":"截屏翻译（快捷键已被占用）";if(!captureHotkey)Shown+=delegate{tray.ShowBalloonTip(6000,"言渡截屏翻译","Ctrl＋Alt＋Z 已被其他软件占用，可右键言渡图标使用截屏翻译。",ToolTipIcon.Info);};}
  }
  void ShowSelectedTranslation(SelectedText selected){ResetTranslation();ShowReading(selected.Text,null);}
  void ShowReading(string text,Func<CancellationToken,Task<string>> ocr){if(readingWindow!=null&&!readingWindow.IsDisposed)readingWindow.Close();readingWindow=new ReadingResult(config,translator,text,ocr);readingWindow.Show();readingWindow.Activate();}
  async void CaptureTranslation(){if(capturing)return;capturing=true;ResetTranslation();if(selectionMonitor!=null)selectionMonitor.Hide();if(readingWindow!=null&&!readingWindow.IsDisposed)readingWindow.Hide();if(screenTranslation!=null&&!screenTranslation.IsDisposed)screenTranslation.Hide();
   try{await Task.Delay(160);if(cleaned||IsDisposed)return;var bounds=SystemInformation.VirtualScreen;using(var bitmap=CaptureOverlay.CaptureDesktop(bounds))using(var overlay=new CaptureOverlay(bitmap,bounds)){
     if(overlay.ShowDialog()==DialogResult.OK){overlay.Captured.Dispose();if(screenTranslation!=null&&!screenTranslation.IsDisposed)screenTranslation.Close();screenTranslation=new ScreenTranslation(config,translator,new Bitmap(bitmap),bounds,overlay.ScreenRegion,value=>{config.CaptureComparison=value;try{config.Save();}catch{}});screenTranslation.Show();screenTranslation.Activate();}
     else{if(screenTranslation!=null&&!screenTranslation.IsDisposed)screenTranslation.Show();else if(readingWindow!=null&&!readingWindow.IsDisposed)readingWindow.Show();}
    }
   }catch{MessageBox.Show("无法截取屏幕，请退出受保护桌面后重试。","言渡截屏翻译",MessageBoxButtons.OK,MessageBoxIcon.Information);}
   finally{capturing=false;}
  }
  void OpenSettings(){if(settingsWindow!=null&&!settingsWindow.IsDisposed){settingsWindow.WindowState=FormWindowState.Normal;settingsWindow.Show();settingsWindow.BringToFront();settingsWindow.Activate();return;}ResetTranslation();settingsWindow=new SettingsForm(config,translator,delegate{translator.Clear();ResetTranslation();});settingsWindow.ScreenTranslationRequested+=CaptureTranslation;settingsWindow.Show();settingsWindow.BringToFront();settingsWindow.Activate();}
  protected override void WndProc(ref Message message){
   if(message.Msg==Native.WM_HOTKEY&&message.WParam.ToInt32()==1101){BeginInvoke((Action)CaptureTranslation);return;}
   if(message.Msg==0x8000+279){ExportDiagnostic();message.Result=new IntPtr(1);return;}
   if(message.Msg==0x8000+272){BeginInvoke((Action)OpenSettings);message.Result=new IntPtr(1);return;}
   if(message.Msg==Native.WM_COPYDATA){
    string data=Native.Read(message.LParam);
    if(data!=null&&data.StartsWith("S|")){
     var value=Snapshot.Parse(data);uint senderPid,targetPid;
     Native.GetWindowThreadProcessId(message.WParam,out senderPid);Native.GetWindowThreadProcessId(value==null?IntPtr.Zero:value.Target,out targetPid);
     if(value!=null&&message.WParam==value.Target&&targetPid!=0&&senderPid==targetPid){BeginInvoke((Action)delegate{Accept(value);});message.Result=new IntPtr(1);return;}
    }else if(data!=null&&data.StartsWith("A|")){
     var parts=data.Split('|');long revision;
     if(parts.Length==3&&Int64.TryParse(parts[1],out revision)&&applying&&revision==pendingRevision&&message.WParam==pendingTarget){bool ok=parts[2]=="1";BeginInvoke((Action)delegate{Applied(ok);});message.Result=new IntPtr(1);return;}
    }
    message.Result=IntPtr.Zero;return;
   }
   base.WndProc(ref message);
  }
  void CheckContext(){
   CheckWeChat();
   if(currentTarget!=IntPtr.Zero&&(Native.GetForegroundWindow()!=previewForeground||(leased&&(DateTime.UtcNow-lastSignal).TotalMilliseconds>1800))){ResetTranslationReason(Native.GetForegroundWindow()!=previewForeground?"foreground_changed":"native_lease_expired");return;}
   if(job!=null){
    if(DateTime.UtcNow>=job.Deadline){CancelRequest();ShowFailure("翻译超时，点击重试");}
    else if(!composing)panel.Hint.Text="正在翻译 · "+Math.Max(1,(int)(DateTime.UtcNow-job.Started).TotalSeconds)+" 秒";
   }
   if(applying&&DateTime.UtcNow>pendingUntil){applying=false;panel.Hint.Text="输入框未确认替换，请检查原文";}
  }
  void Accept(Snapshot value){
   if(value.Mode=="detached"&&value.Target==currentTarget&&(wechatState!=null||wechatRead!=null)){wechatClearPending=true;return;}
   if(wechatState!=null&&value.Target==currentTarget&&value.Mode=="text"){composing=false;wechatNextRead=DateTime.MinValue;return;}
   if(value.Mode=="inactive"||value.Mode=="clear"||value.Source.Length==0&&value.Mode!="preedit"){
    if(currentTarget==IntPtr.Zero||value.Target==currentTarget)ResetTranslationReason("native_"+value.Mode);return;
   }
   if(value.Foreground!=Native.GetForegroundWindow())return;
   if(value.Mode=="preedit"){
    if(value.Target!=currentTarget)return;
    if(!composing)Note("preedit");composing=true;lastSignal=DateTime.UtcNow;leased=true;
    if(panel.Visible)panel.Hint.Text="继续输入中 · 完成选词后更新译文";
    return;
   }
   bool newScope=value.Target!=currentTarget||value.Foreground!=previewForeground;
   if(newScope)ResetTranslation();
   bool changed=snapshot==null||snapshot.Source!=value.Source;
   currentTarget=value.Target;previewForeground=value.Foreground;lastSignal=DateTime.UtcNow;leased=value.Mode!="legacy";composing=false;snapshot=value;
   if(changed){if(undoWindow==value.Target&&value.Source!=undoTarget)undoWindow=IntPtr.Zero;Note("source_changed");}
   BeginWeChatRead(value);
   panel.PositionAt(value.X,value.Y);
   if(!changed){if(panel.Visible&&translatedSource==value.Source)panel.Hint.Text=panel.Expanded?"右键收起 · 滚轮/箭头查看 · 点击替换":"点击替换原文 · 右键展开全文";return;}
   if(!config.Ready){CancelRequest();translated=translatedSource="";panel.Hide();return;}
   panel.Collapse();
   StopSpeech();failed=false;translated=translatedSource="";panel.Speaker.Visible=false;
   panel.UndoAvailable=value.Target==undoWindow&&value.Source==undoTarget;panel.Original.Text=panel.UndoAvailable?undoSource:value.Source;
   if(panel.UndoAvailable){CancelRequest();debounce.Stop();panel.Translation.Text=value.Source;panel.Speaker.Visible=true;panel.Hint.Text="已替换 · 再点译文撤回";panel.ShowPassive();return;}
   if(!Translator.HasChinese(value.Source)){CancelRequest();debounce.Stop();panel.Hide();return;}
   // A different source must never display the previous sentence's translation.
   panel.Translation.Text="正在翻译……";panel.Hint.Text="原文已保留";panel.ShowPassive();
   string core,tail;Translator.SplitTail(value.Source,out core,out tail);
   if(job!=null&&job.Target==value.Target&&job.Foreground==value.Foreground&&job.Core==core)return;
   CancelRequest();debounce.Stop();debounce.Start();
  }
  async Task TranslateCurrent(){
   if(snapshot==null||composing||!config.Ready)return;
   string core,tail;Translator.SplitTail(snapshot.Source,out core,out tail);
   var active=new TranslationJob{Target=snapshot.Target,Foreground=snapshot.Foreground,Core=core,Started=DateTime.UtcNow,Deadline=DateTime.UtcNow.AddMilliseconds(TranslationTimeoutMilliseconds)};
   CancelRequest();job=active;active.Cancel.CancelAfter(TranslationTimeoutMilliseconds);
   Note("request_started");
   try{
    string result=await translator.Translate(config,active.Core,active.Cancel.Token);
    if(job!=active||active.Cancel.IsCancellationRequested||snapshot==null||snapshot.Target!=active.Target||previewForeground!=active.Foreground)return;
    string currentCore,currentTail;Translator.SplitTail(snapshot.Source,out currentCore,out currentTail);
    if(currentCore!=active.Core)return;
    translated=Translator.AttachTail(result,currentTail);translatedSource=snapshot.Source;failed=false;
    Note("request_completed");
    panel.Translation.Text=translated;panel.Speaker.Visible=true;panel.Hint.Text=composing?"继续输入中 · 完成选词后可替换":"点击替换原文 · 右键展开全文";
   }catch(OperationCanceledException){if(job==active)ShowFailure("翻译超时，点击重试");}
   catch(Exception ex){if(job==active)ShowFailure(ex is InvalidOperationException?ex.Message:"连接失败，点击重试");}
   finally{if(job==active)job=null;active.Cancel.Dispose();}
  }
  void ShowFailure(string message){failed=true;translated=translatedSource="";Note("request_failed");panel.Speaker.Visible=false;panel.Translation.Text=message;panel.Hint.Text="原文已保留 · 点击此处重试";}
  void Retry(){if(snapshot==null||composing||!config.Ready)return;CancelRequest();failed=false;panel.Translation.Text="正在翻译……";panel.Hint.Text="原文已保留";debounce.Stop();debounce.Start();}
  bool CanUndo(){return panel.UndoAvailable&&snapshot!=null&&snapshot.Target==undoWindow&&snapshot.Source==undoTarget;}
  void Apply(bool undo){
   Note(undo?"undo_clicked":"apply_clicked");
   if(snapshot==null){Note("apply_no_snapshot");return;}
   if(composing){Note("apply_composing");return;}
   if(applying){Note("apply_busy");return;}
   if(Native.GetForegroundWindow()!=previewForeground){Note("apply_foreground_changed");return;}
   string expected=undo?undoTarget:translatedSource,replacement=undo?undoSource:translated;
   if(undo&&(snapshot.Target!=undoWindow))return;
   if(String.IsNullOrEmpty(replacement)||snapshot.Source!=expected){Note("apply_source_not_ready");return;}
   applying=true;pendingRevision=snapshot.Revision;pendingTarget=snapshot.Target;pendingExpected=expected;pendingReplacement=replacement;pendingUndo=undo;pendingUntil=DateTime.UtcNow.AddSeconds(2);
      if(wechatState!=null){wechatRead=null;ApplyWeChat(wechatState,pendingExpected,pendingReplacement,pendingRevision);return;}
   string command="R|"+pendingRevision+"|"+expected.Length+"|"+replacement.Length+"\n"+expected+replacement;
   if(!Native.Send(pendingTarget,Handle,command)){applying=false;panel.Hint.Text="输入框未响应，原文未替换";}
  }
  async void ApplyWeChat(WeChatEditor.State state,string expected,string replacement,long revision){
   bool ok=await Task.Run(()=>WeChatEditor.Replace(state,expected,replacement));
   Note(ok?"uia_write_succeeded":"uia_write_rejected");
   if(cleaned||snapshot==null||!applying||pendingRevision!=revision||previewForeground!=state.Foreground)return;
   if(ok){state.Text=replacement;wechatState=state;snapshot.Source=replacement;snapshot.Revision++;panel.Original.Text=replacement;}
   Applied(ok);
  }
  void Applied(bool ok){
   if(!applying)return;applying=false;
   Note(ok?"apply_acknowledged":"apply_rejected");
   if(!ok){panel.Hint.Text="原文或光标已变化，未执行替换";return;}
   if(currentTarget!=pendingTarget)return;
   panel.Collapse();
   if(!pendingUndo){undoSource=pendingExpected;undoTarget=pendingReplacement;undoWindow=pendingTarget;}else{undoWindow=IntPtr.Zero;}
   panel.UndoAvailable=!pendingUndo;
   if(pendingUndo){translated=pendingExpected;translatedSource=pendingReplacement;panel.Original.Text=pendingReplacement;panel.Translation.Text=translated;panel.Speaker.Visible=true;panel.Hint.Text="已还原中文 · 点击译文可再次替换";panel.ShowPassive();}
   else{translated=translatedSource="";panel.Original.Text=pendingExpected;panel.Translation.Text=pendingReplacement;panel.Hint.Text="已替换 · 再点译文撤回";panel.ShowPassive();}
  }
  void Speak(){if(speaking){StopSpeech();return;}string text=!String.IsNullOrWhiteSpace(translated)?translated:(snapshot!=null&&snapshot.Source==undoTarget?undoTarget:"");if(String.IsNullOrWhiteSpace(text))return;try{
   speech.Rate=config.SpeechRate;speech.Volume=config.SpeechVolume;
   if(!String.IsNullOrEmpty(config.Voice))speech.SelectVoice(config.Voice);else speech.SelectVoiceByHints(VoiceGender.NotSet,VoiceAge.NotSet,0,CultureInfo.GetCultureInfo("en-US"));
   speaking=true;panel.Speaker.Text="■";speech.SpeakAsync(text);
  }catch{speaking=false;panel.Speaker.Text="🔊";panel.Hint.Text="请在设置中选择可用英文音色";}}
  void StopSpeech(){if(speaking){speech.SpeakAsyncCancelAll();speaking=false;}panel.Speaker.Text="🔊";}
  void Expand(){if(String.IsNullOrEmpty(translated)&&!CanUndo())return;panel.ToggleExpanded();panel.Hint.Text=CanUndo()?(panel.Expanded?"右键收起 · 再点译文撤回":"再点译文撤回 · 右键展开全文"):(panel.Expanded?"右键收起 · 滚轮/箭头查看 · 点击替换":"点击替换原文 · 右键展开全文");}
  void CancelRequest(){var old=job;job=null;if(old!=null)old.Cancel.Cancel();}
  void ResetTranslation(){ResetTranslationReason("context_cleared");}
  void ResetTranslationReason(string reason){wechatState=null;wechatRead=null;wechatClearPending=false;if(currentTarget!=IntPtr.Zero)Note(reason);debounce.Stop();CancelRequest();snapshot=null;currentTarget=previewForeground=IntPtr.Zero;translated=translatedSource="";failed=composing=applying=false;undoWindow=IntPtr.Zero;undoSource=undoTarget="";panel.Original.Text=panel.Translation.Text=panel.Hint.Text="";panel.UndoAvailable=false;panel.Speaker.Visible=false;panel.Hide();StopSpeech();}
  void Cleanup(){if(cleaned)return;cleaned=true;if(captureHotkey){Native.UnregisterHotKey(Handle,1101);captureHotkey=false;}if(selectionMonitor!=null)selectionMonitor.Dispose();if(readingWindow!=null&&!readingWindow.IsDisposed)readingWindow.Close();if(screenTranslation!=null&&!screenTranslation.IsDisposed)screenTranslation.Close();watch.Dispose();debounce.Dispose();CancelRequest();tray.Visible=false;tray.Dispose();speech.SpeakAsyncCancelAll();speech.Dispose();panel.Dispose();translator.Dispose();}
  protected override void OnFormClosed(FormClosedEventArgs e){Cleanup();base.OnFormClosed(e);}
  protected override void Dispose(bool disposing){if(disposing)Cleanup();base.Dispose(disposing);}
 }
 internal static class Program {
  [STAThread] static void Main(string[] args) {
   if(args.Length==3&&args[0]=="--ocr-worker"){Environment.ExitCode=LocalOcr.Worker(args[1],args[2]);return;}
   bool created;using(var mutex=new Mutex(true,"Local\\PersonalTranslateIme.Agent.v1",out created)) {
    if(!created){if(Array.IndexOf(args,"--diagnostics")>=0){var diagnostic=Native.FindWindow(null,"PersonalTranslateIme.Agent.v1");if(diagnostic!=IntPtr.Zero)Native.PostMessage(diagnostic,0x8000+279,IntPtr.Zero,IntPtr.Zero);}if(Array.IndexOf(args,"--settings")>=0){var window=Native.FindWindow(null,"PersonalTranslateIme.Agent.v1");if(window!=IntPtr.Zero){uint owner;Native.GetWindowThreadProcessId(window,out owner);Native.AllowSetForegroundWindow(owner);Native.PostMessage(window,0x8000+272,IntPtr.Zero,IntPtr.Zero);}}return;}System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
    Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var agent=new Agent();if(Array.IndexOf(args,"--settings")>=0)agent.Shown+=delegate{Native.PostMessage(agent.Handle,0x8000+272,IntPtr.Zero,IntPtr.Zero);};Application.Run(agent);
   }
  }
 }
}












