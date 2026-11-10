using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows.Forms;
using PersonalIme;
[ComImport,Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface LiveProfileManager{[PreserveSig]int ActivateProfile(uint type,ushort lang,ref Guid id,ref Guid profile,IntPtr layout,uint flags);[PreserveSig]int DeactivateProfile(uint type,ushort lang,ref Guid id,ref Guid profile,IntPtr layout,uint flags);}
class WeChatAltProbe:NativeWindow,IDisposable{public bool Seen;public WeChatAltProbe(){CreateHandle(new CreateParams{Caption="Yandu WeChat Alt probe",Parent=new IntPtr(-3)});}protected override void WndProc(ref Message m){if(m.Msg==Native.WM_HOTKEY)Seen=true;else base.WndProc(ref m);}public void Dispose(){Native.UnregisterHotKey(Handle,915);DestroyHandle();}}
class WeChatLiveTest {
 [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;public int Width{get{return Right-Left;}}public int Height{get{return Bottom-Top;}}}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")]static extern bool EnumWindows(WindowProc proc,IntPtr data);delegate bool WindowProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadKeyboardLayout(string name,uint flags);
 [StructLayout(LayoutKind.Sequential)]struct Keyboard{public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)]struct Mouse{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit)]struct Union{[FieldOffset(0)]public Keyboard Keyboard;[FieldOffset(0)]public Mouse Mouse;}
 [StructLayout(LayoutKind.Sequential)]struct Input{public uint Type;public Union Data;}
 [DllImport("user32.dll")]static extern uint SendInput(uint count,Input[] input,int size);
 static void PressAltA(){var keys=new Input[4];for(int i=0;i<4;i++){keys[i].Type=1;keys[i].Data.Keyboard.Vk=(ushort)((i==0||i==3)?0x12:0x41);keys[i].Data.Keyboard.Flags=(uint)(i>=2?2:0);}Check(SendInput(4,keys,Marshal.SizeOf(typeof(Input)))==4,"physical Alt+A injected");} [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool AttachThreadInput(uint a,uint b,bool c);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern void keybd_event(byte a,byte b,uint c,UIntPtr d);
 static void ForceForeground(){uint pid;var tid=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid);var own=GetCurrentThreadId();AttachThreadInput(own,tid,true);try{Native.ShowWindow(main,9);SetForegroundWindow(main);}finally{AttachThreadInput(own,tid,false);}if(Native.GetForegroundWindow()!=main){keybd_event(0x12,0,0,UIntPtr.Zero);SetForegroundWindow(main);keybd_event(0x12,0,2,UIntPtr.Zero);}} static AutomationElement input;static ValuePattern value;static IntPtr main,agent;static int processId,checks;static string owned="";
 static readonly string diagnostic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PersonalTranslateIme","diagnostic-state.json");
 static string hostVersion="0.3.2";
 static readonly JavaScriptSerializer json=new JavaScriptSerializer();
 static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(5);}}
 static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS "+name);checks++;}
 static bool IsTarget(){try{if(main==IntPtr.Zero)return false;var root=AutomationElement.FromHandle(main);if(root.Current.ProcessId!=processId)return false;var header=root.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text),new PropertyCondition(AutomationElement.NameProperty,"文件传输助手")));if(header==null)return false;var editor=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ClassNameProperty,"mmui::ChatInputField"));object pattern;if(editor==null||!editor.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))return false;input=editor;value=(ValuePattern)pattern;return !value.Current.IsReadOnly;}catch{return false;}} static string Read(){if(!IsTarget())throw new InvalidOperationException("Target chat changed; stopped");return value.Current.Value??"";}
 static void Guard(){if(!IsTarget())throw new InvalidOperationException("File Transfer editor identity check failed; stopped");if(Native.GetForegroundWindow()!=main)throw new InvalidOperationException("File Transfer foreground changed; stopped");}
 static void Click(Point point,IntPtr root,bool right=false){if(GetAncestor(WindowFromPoint(point),2)!=root)throw new InvalidOperationException("Click target is covered; stopped");SetCursorPos(point.X,point.Y);mouse_event(right?8u:2u,0,0,0,UIntPtr.Zero);mouse_event(right?16u:4u,0,0,0,UIntPtr.Zero);}
 static void FocusInput(){ForceForeground();if(!IsTarget())throw new InvalidOperationException("Target changed");Native.SetWindowPos(main,new IntPtr(-1),0,0,0,0,0x13);try{input.SetFocus();Pump(100);var r=input.Current.BoundingRectangle;Click(new Point{X=(int)r.Left+30,Y=(int)r.Top+35},main);}finally{Native.SetWindowPos(main,new IntPtr(-2),0,0,0,0,0x13);}Pump(150);if(Native.GetForegroundWindow()!=main){Console.WriteLine("waiting for user to focus verified File Transfer input");var focusDeadline=DateTime.UtcNow.AddSeconds(90);while(DateTime.UtcNow<focusDeadline && Native.GetForegroundWindow()!=main)Pump(100);}Guard();}
 static bool Locate(){foreach(var name in new[]{"Weixin","WeChat"})foreach(var p in Process.GetProcessesByName(name)){if(p.MainWindowHandle==IntPtr.Zero)continue;bool old=false;foreach(ProcessModule module in p.Modules)if(module.ModuleName.StartsWith("weasel",StringComparison.OrdinalIgnoreCase)&&!module.FileName.EndsWith("."+hostVersion+".dll",StringComparison.OrdinalIgnoreCase))old=true;if(old)continue;
  var root=AutomationElement.FromHandle(p.MainWindowHandle);if(root.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text),new PropertyCondition(AutomationElement.NameProperty,"文件传输助手")))==null)continue;var condition=new PropertyCondition(AutomationElement.ClassNameProperty,"mmui::ChatInputField");var editor=root.FindFirst(TreeScope.Descendants,condition);object pattern;if(editor==null||!editor.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))continue;var values=(ValuePattern)pattern;if(values.Current.IsReadOnly||!String.IsNullOrEmpty(values.Current.Value))continue;input=editor;value=values;main=p.MainWindowHandle;processId=p.Id;return true;}return false;}
 static Dictionary<string,object> Diagnostic(){Native.PostMessage(agent,0x8000+279,IntPtr.Zero,IntPtr.Zero);Pump(45);try{return json.Deserialize<Dictionary<string,object>>(File.ReadAllText(diagnostic));}catch{return null;}}
 static void WaitTranslation(int chars){var until=DateTime.UtcNow.AddSeconds(25);while(DateTime.UtcNow<until){Guard();var d=Diagnostic();if(d!=null&&Convert.ToString(d["version"])=="0.3.2.0"&&Convert.ToBoolean(d["hasNativeTarget"])&&!Convert.ToBoolean(d["requestActive"])&&!Convert.ToBoolean(d["composing"])&&!Convert.ToBoolean(d["failed"])){
   var history=d["history"] as ArrayList;if(history!=null&&history.Count>0){var last=history[history.Count-1] as Dictionary<string,object>;if(last!=null&&Convert.ToString(last["state"])=="request_completed"&&Convert.ToInt32(last["sourceCharacters"])==chars&&Convert.ToInt32(last["translationCharacters"])>0)return;}}
  Pump(100);}throw new InvalidOperationException("Live translation did not finish before deadline");}
 static void Type(string pinyin,string expected){Guard();Check(Read()=="","input is empty before fixture");SendKeys.SendWait(pinyin);Pump(700);Check(Read()==expected,"real WeChat pinyin produces intended Chinese");owned=expected;}
 static void ClearOwned(){Guard();Check(Read()==owned,"cleanup only touches known fixture");value.SetValue("");owned="";Pump(500);Check(Read()=="","test fixture removed");}
 static void PanelClick(bool undo,bool right=false){Guard();var panel=Native.FindWindow(null,"言渡 · 译文");Check(panel!=IntPtr.Zero&&Native.IsWindowVisible(panel),"live translation panel visible");uint pid;Native.GetWindowThreadProcessId(panel,out pid);uint owner;Native.GetWindowThreadProcessId(agent,out owner);Check(pid==owner,"panel belongs to active translation agent");Rect r;GetWindowRect(panel,out r);double scale=r.Width/530.0;Click(new Point{X=r.Left+(int)(45*scale),Y=r.Top+(int)(53*scale)},panel,right);Pump(450);Guard();}
 static string Replace(string expectedPattern){var before=Read();PanelClick(false);var replaceDeadline=DateTime.UtcNow.AddSeconds(2);while(Read()==before&&DateTime.UtcNow<replaceDeadline){Guard();Pump(40);}var result=Read();if(result==before)Console.WriteLine("replacementDiagnostic="+json.Serialize(Diagnostic()));Check(result!=before&&Regex.IsMatch(result,expectedPattern,RegexOptions.IgnoreCase),"live API translation replaces exact Chinese source");owned=result;return result;}
 static HashSet<IntPtr> VisibleChatWindows(){var set=new HashSet<IntPtr>();EnumWindows(delegate(IntPtr h,IntPtr d){if(Native.IsWindowVisible(h)){uint pid;Native.GetWindowThreadProcessId(h,out pid);try{var name=Process.GetProcessById((int)pid).ProcessName;if(name=="Weixin"||name=="WeChat")set.Add(h);}catch{}}return true;},IntPtr.Zero);return set;}
 static void ScreenshotShortcut(){Guard();using(var probe=new WeChatAltProbe()){if(Native.RegisterHotKey(probe.Handle,915,0x4001,0x41)){try{PressAltA();Pump(400);Check(probe.Seen,"external registered Alt+A works while typing in actual WeChat");Check(Read()==owned,"Alt+A does not insert stray text");Console.WriteLine("NOTE WeChat screenshot Alt+A was not registered; screenshot launch not claimed");return;}finally{Native.UnregisterHotKey(probe.Handle,915);}}}var before=VisibleChatWindows();PressAltA();Pump(600);var screen=Screen.FromHandle(main).Bounds;IntPtr overlay=IntPtr.Zero;foreach(var h in VisibleChatWindows()){if(before.Contains(h))continue;Rect r;GetWindowRect(h,out r);if(r.Width>=screen.Width*0.7&&r.Height>=screen.Height*0.7){overlay=h;break;}}
  Check(overlay!=IntPtr.Zero,"actual WeChat Alt+A opens screenshot overlay");if(Native.GetForegroundWindow()==overlay){SendKeys.SendWait("{ESC}");}else{Native.PostMessage(overlay,0x100,new IntPtr(0x1b),IntPtr.Zero);Native.PostMessage(overlay,0x101,new IntPtr(0x1b),IntPtr.Zero);}Pump(500);Check(!Native.IsWindowVisible(overlay),"screenshot overlay cancelled without saving image");FocusInput();}
 static void InlinePreview(){
  Type("nihaoshijie ","你好世界");WaitTranslation(4);
  string chinese="你好世界，今天下午三点开会，请大家提前十分钟到会议室。会议结束后，我们一起讨论下一步的项目安排，并确认每个人负责的任务。";
  Guard();value.SetValue(chinese);owned=chinese;var pattern=(TextPattern)input.GetCurrentPattern(TextPattern.Pattern);var end=pattern.DocumentRange;end.MoveEndpointByRange(System.Windows.Automation.Text.TextPatternRangeEndpoint.Start,end,System.Windows.Automation.Text.TextPatternRangeEndpoint.End);end.Select();Pump(500);WaitTranslation(chinese.Length);
  var panel=Native.FindWindow(null,"言渡 · 译文");Rect before,after;GetWindowRect(panel,out before);var foreground=Native.GetForegroundWindow();PanelClick(false,true);GetWindowRect(panel,out after);
  Check(after.Height>before.Height,"right-click expands long translation inside original WeChat card");Check(Native.FindWindow(null,"言渡 · 译文")==panel,"expanded translation keeps original popup handle");Check(Native.FindWindow(null,"完整译文")==IntPtr.Zero,"no separate centered translation window");Check(Native.GetForegroundWindow()==foreground&&Read()==owned,"inline preview preserves WeChat focus and original text");
  PanelClick(false,true);GetWindowRect(panel,out after);Check(after.Height==before.Height,"second right-click collapses actual WeChat card");
  PanelClick(false,true);Replace("world|meeting|afternoon");PanelClick(true);var undoDeadline=DateTime.UtcNow.AddSeconds(2);while(Read()!=chinese&&DateTime.UtcNow<undoDeadline){Guard();Pump(40);}Check(Read()==chinese,"expanded-card replacement can undo to exact long Chinese");owned=chinese;ClearOwned();
 } [STAThread]static int Main(string[] args){if(Array.IndexOf(args,"--legacy-host")>=0)hostVersion="0.3.1";Console.SetOut(new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","v3-wechat-live.log"),false){AutoFlush=true});Native.SetProcessDPIAware();Application.EnableVisualStyles();try{
  Console.WriteLine("nativeHostVersion="+hostVersion+" agentExpected=0.3.2.0");Console.WriteLine("waiting for verified WeChat and empty File Transfer input");var until=DateTime.UtcNow.AddMinutes(5);while(DateTime.UtcNow<until&&!Locate())Pump(400);Check(input!=null,"empty File Transfer input located without touching drafts");FocusInput();
  var clsid=new Guid("54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A");var profile=new Guid("D6F4BC2E-A620-46E4-BA20-1C9267529467");var manager=(LiveProfileManager)Activator.CreateInstance(System.Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED")));Native.PostMessage(main,0x50,IntPtr.Zero,LoadKeyboardLayout("00000804",0));Pump(200);Check(manager.ActivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x20000005)==0,"installed IME activated for live test");Pump(700);FocusInput();bool current=false;foreach(ProcessModule module in Process.GetProcessById(processId).Modules)if(module.FileName.EndsWith("weaselx64."+hostVersion+".dll",StringComparison.OrdinalIgnoreCase))current=true;Check(current,"real WeChat loaded third-version production DLL");
  agent=Native.FindWindow(null,"PersonalTranslateIme.Agent.v1");Check(agent!=IntPtr.Zero,"translation agent running");Check(Read()=="","original draft remains empty");
  var timer=Stopwatch.StartNew();Type("nihaoshijie ","你好世界");WaitTranslation(4);Console.WriteLine("liveGreetingElapsedMs="+timer.ElapsedMilliseconds);Replace("hello");PanelClick(true);var undoDeadline=DateTime.UtcNow.AddSeconds(2);while(Read()!="你好世界"&&DateTime.UtcNow<undoDeadline){Guard();Pump(40);}Check(Read()=="你好世界","undo in real WeChat restores original Chinese");owned="你好世界";ClearOwned();
  Type("jintianxiawusandiankaihui ","今天下午三点开会");Pump(200);ClearOwned();Type("pingguo ","苹果");WaitTranslation(2);Replace("apple");ScreenshotShortcut();ClearOwned();
  Type("nihaoshijie ","你好世界");WaitTranslation(4);
  var textPattern=(TextPattern)input.GetCurrentPattern(TextPattern.Pattern);var startRange=textPattern.DocumentRange;startRange.MoveEndpointByRange(System.Windows.Automation.Text.TextPatternRangeEndpoint.End,startRange,System.Windows.Automation.Text.TextPatternRangeEndpoint.Start);startRange.Select();Pump(850);
  var stalePanel=Native.FindWindow(null,"言渡 · 译文");Check(stalePanel==IntPtr.Zero||!Native.IsWindowVisible(stalePanel),"moving caret revokes WeChat compatibility target");Check(Read()==owned,"caret change leaves fixture untouched");ClearOwned();
  Type("nihaoshijie ","你好世界");WaitTranslation(4);
  Check(manager.DeactivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x20000000)==0,"live native profile deactivation accepted");Pump(850);stalePanel=Native.FindWindow(null,"言渡 · 译文");Check(stalePanel==IntPtr.Zero||!Native.IsWindowVisible(stalePanel),"deactivating IME clears actual WeChat compatibility panel");Check(Read()==owned,"deactivation never changes Chinese fixture");ClearOwned();
  Check(manager.ActivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x20000005)==0,"installed profile restored after test");
  InlinePreview();Marshal.ReleaseComObject(manager);Console.WriteLine("OK "+checks+" real WeChat checks; no messages sent");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+(e is InvalidOperationException?e.Message:e.GetType().Name));try{if(input!=null&&IsTarget()&&Native.GetForegroundWindow()==main&&owned.Length>0&&Read()==owned){value.SetValue("");Console.WriteLine("known test fixture removed after failure");}}catch{}return 1;}}
}















