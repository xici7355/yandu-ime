using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PersonalIme;
using System.IO;
using System.Speech.Synthesis;
[ComImport,Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ProfileManager {
 [PreserveSig]int ActivateProfile(uint type,ushort lang,ref Guid id,ref Guid profile,IntPtr layout,uint flags);
 [PreserveSig]int DeactivateProfile(uint type,ushort lang,ref Guid id,ref Guid profile,IntPtr layout,uint flags);
}
[ComImport,Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface Profiles {
 [PreserveSig]int Register(ref Guid id);
 [PreserveSig]int Unregister(ref Guid id);
 [PreserveSig]int AddLanguageProfile(ref Guid id,ushort lang,ref Guid profile,[MarshalAs(UnmanagedType.LPWStr)]string description,uint length,[MarshalAs(UnmanagedType.LPWStr)]string file,uint fileLength,uint icon);
 [PreserveSig]int RemoveLanguageProfile(ref Guid id,ushort lang,ref Guid profile);
 [PreserveSig]int EnumInputProcessorInfo(out IntPtr value);
 [PreserveSig]int GetDefaultLanguageProfile(ushort lang,ref Guid category,out Guid id,out Guid profile);
 [PreserveSig]int SetDefaultLanguageProfile(ushort lang,ref Guid id,ref Guid profile);
 [PreserveSig]int ActivateLanguageProfile(ref Guid id,ushort lang,ref Guid profile);
}
class ShortcutProbe:NativeWindow,IDisposable {
 public bool Seen;public const uint Vk=0x87;
 [StructLayout(LayoutKind.Sequential)]struct Keyboard{public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)]struct Mouse{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit)]struct Union{[FieldOffset(0)]public Keyboard Keyboard;[FieldOffset(0)]public Mouse Mouse;}
 [StructLayout(LayoutKind.Sequential)]struct Input{public uint Type;public Union Data;}
 [DllImport("user32.dll")]static extern uint SendInput(uint count,Input[] input,int size);
 public ShortcutProbe(){CreateHandle(new CreateParams{Caption="Yandu shortcut probe",Parent=new IntPtr(-3)});}
 public void Press(){var keys=new Input[4];for(int i=0;i<4;i++){keys[i].Type=1;keys[i].Data.Keyboard.Vk=(ushort)((i==0||i==3)?0x12:Vk);keys[i].Data.Keyboard.Flags=(uint)(i>=2?2:0);}if(SendInput(4,keys,Marshal.SizeOf(typeof(Input)))!=4)throw new Exception("Physical shortcut injection failed");}
 protected override void WndProc(ref Message m){if(m.Msg==Native.WM_HOTKEY){Seen=true;return;}base.WndProc(ref m);}
 public void Dispose(){Native.UnregisterHotKey(Handle,91);DestroyHandle();}
}
class NativeIntegration {
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd); [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)]struct TestRect{public int Left,Top,Right,Bottom;} [StructLayout(LayoutKind.Sequential)]struct TestPoint{public int X,Y;} [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out TestRect r);[DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);[DllImport("user32.dll")]static extern IntPtr WindowFromPoint(TestPoint p);[DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr h,uint f);[DllImport("user32.dll")]static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
 static void FocusFixture(IntPtr h){Native.ShowWindow(h,9);if(!Native.IsWindowVisible(h))Native.ShowWindow(h,9);SetForegroundWindow(h);if(Native.GetForegroundWindow()!=h){keybd_event(0x12,0,0,UIntPtr.Zero);SetForegroundWindow(h);keybd_event(0x12,0,2,UIntPtr.Zero);}Native.SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x13);Pump(100);TestRect r;GetWindowRect(h,out r);var p=new TestPoint{X=r.Left+200,Y=r.Top+120};if(GetAncestor(WindowFromPoint(p),2)==h){SetCursorPos(p.X,p.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(150);}Native.SetWindowPos(h,new IntPtr(-2),0,0,0,0,0x13);var until=DateTime.UtcNow.AddMinutes(4);while(Native.GetForegroundWindow()!=h && DateTime.UtcNow<until)Pump(100);Check(Native.GetForegroundWindow()==h,"isolated test host has keyboard focus");}

 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
 static object Field(object o,string field){return o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static void Call(object o,string method,params object[] args){o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
 static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(5);}}
 static IntPtr fixture;static void TypeFixture(string text){if(Native.GetForegroundWindow()!=fixture)throw new Exception("Test window lost focus; typing stopped");SendKeys.SendWait(text);}static void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);}
 [STAThread]static int Main(string[] args){if(args.Length>0)Console.SetOut(new StreamWriter(args[0],false){AutoFlush=true});if(args.Length>1)Environment.SetEnvironmentVariable("PERSONAL_IME_EXPECTED_TSF",args[1]);try{Run();return 0;}catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}}
 static void Run(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  var principal=new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent());Console.WriteLine("administrator="+principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
  var config=new Settings();config.SetKey("sk-local-native-test");var handler=new MockHandler();
  using(var engine=new Translator(handler))using(var agent=new Agent(config,engine,true))using(var host=new Form {Text="言渡输入测试 · 点击输入区后停留，自动完成",Size=new Size(700,400),StartPosition=FormStartPosition.CenterScreen}){
   var editor=new RichTextBox {Dock=DockStyle.Fill,Font=new Font("Microsoft YaHei UI",16)};host.Controls.Add(editor);host.Show();host.Activate();SetForegroundWindow(host.Handle);editor.Focus();Pump(300);FocusFixture(host.Handle);fixture=host.Handle;host.Deactivate+=delegate{uint focusPid;Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out focusPid);string focusName="none";try{focusName=System.Diagnostics.Process.GetProcessById((int)focusPid).ProcessName;}catch{}Console.WriteLine("host_deactivated_to="+focusName);};
   var clsid=new Guid("54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A");var profile=new Guid("D6F4BC2E-A620-46E4-BA20-1C9267529467");
   var profiles=(ProfileManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED")));
   int hr=profiles.ActivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x10000005);Console.WriteLine("ActivateProfile HRESULT="+hr.ToString("X8"));Marshal.ThrowExceptionForHR(hr);Check(hr==0,"custom profile activated for test process");Pump(700);
   string expectedDll=Environment.Is64BitProcess?"weaselx64.dll":"weasel.dll";Console.WriteLine("process architecture="+(Environment.Is64BitProcess?"x64":"x86"));
   var installedExpected=Environment.GetEnvironmentVariable("PERSONAL_IME_EXPECTED_TSF");bool loaded=false;foreach(System.Diagnostics.ProcessModule module in System.Diagnostics.Process.GetCurrentProcess().Modules)if(installedExpected==null?module.ModuleName.Equals(expectedDll,StringComparison.OrdinalIgnoreCase):String.Equals(module.FileName,installedExpected,StringComparison.OrdinalIgnoreCase)){loaded=true;Console.WriteLine("loaded test DLL="+module.FileName);}
   Check(loaded,"custom TSF DLL loaded in test process");
   editor.Clear();editor.Focus();Pump(100);TypeFixture("nihao ");Pump(1200);
   Console.WriteLine("fixture text="+editor.Text);
   Check(editor.Text=="你好","TSF Chinese input in RichEdit");
   var snapshot=(Snapshot)Field(agent,"snapshot");if(snapshot==null)Console.WriteLine("state="+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(Field(agent,"diagnosticHistory")));Check(snapshot!=null&&snapshot.Source=="你好","TSF committed-range snapshot");
   Check((string)Field(agent,"translated")=="Hello.","TSF live translation preview");
   ((PanelForm)Field(agent,"panel")).SavePreview("personal/bin/translation-preview.png");
   Call(agent,"Apply",false);Pump(500);Check(editor.Text=="Hello.","TSF verified range replacement");
   Call(agent,"Apply",true);Pump(500);Check(editor.Text=="你好","TSF undo replacement");
   editor.SelectionStart=0;Pump(250);Check(Field(agent,"snapshot")==null,"caret move invalidates old translation");
   Check(handler.Calls==1,"no real API calls; cache reused");
   editor.Text="前缀 后缀";editor.SelectionStart=3;Pump(150);TypeFixture("nihao ");Pump(850);
   Check(editor.Text=="前缀 你好后缀","Chinese inserted between surrounding text");
   snapshot=(Snapshot)Field(agent,"snapshot");Check(snapshot!=null&&snapshot.Source=="你好","only newly committed source tracked");
   var panel=(PanelForm)Field(agent,"panel");
   SendMessage(panel.Handle,0x202,IntPtr.Zero,new IntPtr(((panel.Translation.Top+panel.Translation.Height/2)<<16)|25));Pump(250);
   Check(editor.Text=="前缀 Hello.后缀","mouse click replaces source and preserves surroundings");
   Check(Native.GetForegroundWindow()==host.Handle,"translation mouse click keeps input focus");
   SendMessage(panel.Handle,0x202,IntPtr.Zero,new IntPtr(((panel.Translation.Top+panel.Translation.Height/2)<<16)|(panel.Translation.Left+25)));Pump(600);
   Check(editor.Text=="前缀 你好后缀","same translation area restores only source");
   TypeFixture("shijie ");Pump(850);
   Check(editor.Text=="前缀 你好世界后缀","continuous Chinese commits accumulate");
   snapshot=(Snapshot)Field(agent,"snapshot");Check(snapshot!=null&&snapshot.Source=="你好世界","whole current source translated");
   int calls=handler.Calls;TypeFixture(",");Pump(700);
   Check(editor.Text=="前缀 你好世界，后缀","original Chinese comma retained");
   Check(((string)Field(agent,"translated")).EndsWith(","),"translated comma converted locally");Check(handler.Calls==calls,"trailing punctuation makes no new API call");
   TypeFixture("{BACKSPACE}");Pump(650);Check(editor.Text=="前缀 你好世界后缀","backspace removes trailing punctuation");Check(handler.Calls==calls,"deleting trailing punctuation uses cache");
   var actionPoint=new IntPtr(((panel.Translation.Top+panel.Translation.Height/2)<<16)|(panel.Translation.Left+25));
   SendMessage(panel.Handle,0x202,IntPtr.Zero,actionPoint);Pump(500);Check(editor.Text=="前缀 Hello.后缀","shared translation action replaces source");
   SendMessage(panel.Handle,0x202,IntPtr.Zero,actionPoint);Pump(650);Check(editor.Text=="前缀 你好世界后缀","same translation action restores source");
   var speech=(SpeechSynthesizer)Field(agent,"speech");using(var audio=new MemoryStream()){
    speech.SetOutputToWaveStream(audio);SendMessage(panel.Handle,0x202,IntPtr.Zero,new IntPtr(((panel.Speaker.Top+panel.Speaker.Height/2)<<16)|(panel.Speaker.Left+panel.Speaker.Width/2)));Check((bool)Field(agent,"speaking"),"speaker click starts synthesis");
    SendMessage(panel.Handle,0x202,IntPtr.Zero,new IntPtr(((panel.Speaker.Top+panel.Speaker.Height/2)<<16)|(panel.Speaker.Left+panel.Speaker.Width/2)));Check(!(bool)Field(agent,"speaking"),"second speaker click stops synthesis");
    Call(agent,"Speak");Pump(1300);speech.SetOutputToNull();Check(audio.Length>44,"system voice produces real WAV audio");
   }
   editor.Text="我还在测试这个版本使用起来怎么样";editor.SelectionStart=editor.TextLength;Pump(150);TypeFixture("nihao ");Pump(1000);
   Check(editor.Text=="我还在测试这个版本使用起来怎么样你好"&&((Snapshot)Field(agent,"snapshot")).Source==editor.Text,"RichEdit captures complete current line including existing subject");
   Call(agent,"Apply",false);Pump(400);Check(editor.Text=="Hello.","RichEdit replaces full captured line");Call(agent,"Apply",true);Pump(400);Check(editor.Text=="我还在测试这个版本使用起来怎么样你好","RichEdit undo restores full line");
   editor.Text="这个版本使用起来怎么样";editor.SelectionStart=0;Pump(150);TypeFixture("wo ");Pump(400);editor.SelectionStart=editor.TextLength;Pump(200);TypeFixture("nihao ");Pump(1000);Check(editor.Text=="我这个版本使用起来怎么样你好"&&((Snapshot)Field(agent,"snapshot")).Source==editor.Text,"RichEdit prefix insertion is retained on next full-line translation");
   editor.Text="你好";editor.SelectionStart=2;Pump(150);TypeFixture("nihao ");Pump(850);Console.WriteLine("duplicate fixture="+editor.Text);Check(editor.Text=="你好你好","duplicate prefix fixture");
   TypeFixture("{BACKSPACE 2}");Pump(500);Check(editor.Text=="你好"&&((Snapshot)Field(agent,"snapshot")).Source=="你好","deleting part of a complete current line refreshes exact source");editor.SelectionStart=0;Pump(200);
   Call(agent,"Apply",false);Check(editor.Text=="你好","stale replacement leaves earlier text intact");
   config.SetKey("");calls=handler.Calls;editor.Clear();Pump(150);TypeFixture("nihao ");Pump(800);Check(editor.Text=="你好","ordinary Chinese input without API");Check(handler.Calls==calls&&!panel.Visible,"no key means no request and no preview");
   config.SetKey("sk-local-native-test");
   host.Controls.Clear();editor.Dispose();var wpf=new System.Windows.Controls.TextBox{FontSize=24,AcceptsReturn=true};var bridge=new System.Windows.Forms.Integration.ElementHost{Dock=DockStyle.Fill,Child=wpf};host.Controls.Add(bridge);bridge.Focus();wpf.Focus();Pump(300);FocusFixture(host.Handle);wpf.Focus();Pump(300);Check(wpf.IsKeyboardFocused,"new WPF editor has actual keyboard focus");
   TypeFixture("nihao ");Pump(1000);if(wpf.Text!="你好")Console.WriteLine("wpfFocused="+wpf.IsKeyboardFocused+" inputCulture="+InputLanguage.CurrentInputLanguage.Culture.Name+" fixtureCharacters="+wpf.Text.Length+" rawPinyin="+(wpf.Text=="nihao "));Check(wpf.Text=="你好","WPF Chinese commit in TSF host");Check(Field(agent,"snapshot")!=null&&((Snapshot)Field(agent,"snapshot")).Source=="你好","WPF native source bookmark");Check((string)Field(agent,"translated")=="Hello.","WPF translation preview");
   TypeFixture("shi");Pump(250);Check(panel.Visible,"WPF preedit preserves preview");Call(agent,"Apply",false);Check(wpf.Text!="Hello.","WPF composition cannot be overwritten");TypeFixture("jie ");Pump(900);Check(wpf.Text=="你好世界","WPF continuous Chinese input");
   Call(agent,"Apply",false);Pump(400);Check(wpf.Text=="Hello.","WPF verified replacement");Call(agent,"Apply",true);Pump(500);Check(wpf.Text=="你好世界","WPF undo replacement");wpf.CaretIndex=0;Pump(200);Check(Field(agent,"snapshot")==null,"WPF caret move revokes replacement");
   wpf.Clear();wpf.Focus();Pump(200);TypeFixture("ni");Pump(150);
   using(var shortcut=new ShortcutProbe()){
    Check(Native.RegisterHotKey(shortcut.Handle,91,0x4001,ShortcutProbe.Vk),"application Alt shortcut registered outside IME");
    if(Native.GetForegroundWindow()!=host.Handle)throw new Exception("Test window lost focus; shortcut stopped");shortcut.Press();Pump(200);Check(shortcut.Seen,"application Alt shortcut works during pinyin composition");
   }
   TypeFixture("{ESC}");Pump(100);
   host.Close();Marshal.ReleaseComObject(profiles);Console.WriteLine("OK native TSF integration");
  }
 }
}







