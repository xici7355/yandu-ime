using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using PersonalIme;

[ComImport,Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface NotepadProfiles{[PreserveSig]int ActivateProfile(uint type,ushort lang,ref Guid id,ref Guid profile,IntPtr layout,uint flags);}
class NotepadLiveTest {
 delegate bool WindowCallback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumWindows(WindowCallback callback,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int length);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern void keybd_event(byte a,byte b,uint flags,UIntPtr extra);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadKeyboardLayout(string name,uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 struct Rect{public int Left,Top,Right,Bottom;}
 struct Point{public int X,Y;}
 static IntPtr main;static string name,owned="",fixture;static int checks;
 static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
 static string Title(IntPtr h){var b=new StringBuilder(512);GetWindowText(h,b,512);return b.ToString();}
 static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks++;Console.WriteLine("PASS "+label);}
 static AutomationElement Editor(){if(main==IntPtr.Zero||!Title(main).Contains(name))throw new InvalidOperationException("Fixture tab changed; stopped");var root=AutomationElement.FromHandle(main);var docs=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));foreach(AutomationElement doc in docs){var r=doc.Current.BoundingRectangle;object p;if(doc.Current.IsKeyboardFocusable&&r.Width>200&&r.Height>120&&doc.TryGetCurrentPattern(TextPattern.Pattern,out p))return doc;}throw new InvalidOperationException("Notepad document provider unavailable");}
 static string Read(){return ((TextPattern)Editor().GetCurrentPattern(TextPattern.Pattern)).DocumentRange.GetText(-1).TrimEnd('\r','\n');}
 static void Guard(){if(Native.GetForegroundWindow()!=main||!Title(main).Contains(name))throw new InvalidOperationException("Fixture lost focus; stopped");}
 static void Focus(){Native.ShowWindow(main,9);if(!SetForegroundWindow(main)){keybd_event(0x12,0,0,UIntPtr.Zero);SetForegroundWindow(main);keybd_event(0x12,0,2,UIntPtr.Zero);}Editor().SetFocus();Pump(250);Guard();}
 static void Keys(string keys){Guard();SendKeys.SendWait(keys);Pump(500);}
 static void Panel(bool undo){Guard();var h=Native.FindWindow(null,"言渡 · 译文");Check(h!=IntPtr.Zero&&Native.IsWindowVisible(h),"Notepad translation panel visible");Rect r;GetWindowRect(h,out r);double scale=(r.Right-r.Left)/530.0;var p=new Point{X=r.Left+(int)(45*scale),Y=r.Top+(int)(53*scale)};Check(GetAncestor(WindowFromPoint(p),2)==h,"fixture panel click unobstructed");SetCursorPos(p.X,p.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(600);Guard();}
 static void ClearOwned(){Guard();Check(Read()==owned,"cleanup matches exact owned Notepad fixture");Keys("^a");Keys("{BACKSPACE}");Check(Read()=="","Notepad fixture cleared");owned="";Keys("^s");Check(new FileInfo(fixture).Length==0,"only empty fixture file saved");}
 [STAThread]static int Main(){Console.SetOut(new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","v3-notepad-live.log"),false){AutoFlush=true});Native.SetProcessDPIAware();Application.EnableVisualStyles();try{
  name="Yandu-test-"+Guid.NewGuid().ToString("N")+".txt";fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);File.WriteAllText(fixture,"");Process.Start(new ProcessStartInfo("notepad.exe","\""+fixture+"\""){UseShellExecute=true});
  var deadline=DateTime.UtcNow.AddSeconds(25);while(main==IntPtr.Zero&&DateTime.UtcNow<deadline){EnumWindows(delegate(IntPtr h,IntPtr p){if(Native.IsWindowVisible(h)&&Title(h).Contains(name)){main=h;return false;}return true;},IntPtr.Zero);Pump(200);}Check(main!=IntPtr.Zero,"new uniquely named Notepad fixture located");Focus();Check(Read()=="","fixture starts empty; existing documents untouched");
  var clsid=new Guid("54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A");var profile=new Guid("D6F4BC2E-A620-46E4-BA20-1C9267529467");var manager=(NotepadProfiles)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED")));Native.PostMessage(main,0x50,IntPtr.Zero,LoadKeyboardLayout("00000804",0));Pump(150);Check(manager.ActivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x20000005)==0,"installed profile activated");Pump(400);Focus();Keys("nihaoshijie ");Check(Read()=="你好世界","actual Notepad Chinese input");owned="你好世界";Pump(2000);Panel(false);var english=Read();Check(english!=owned&&Regex.IsMatch(english,"hello",RegexOptions.IgnoreCase),"actual Notepad API translation replaces exact source");owned=english;Panel(true);Check(Read()=="你好世界","actual Notepad undo restores Chinese");owned="你好世界";ClearOwned();Keys("^w");Marshal.ReleaseComObject(manager);Console.WriteLine("OK "+checks+" Notepad checks; existing tabs untouched");return 0;
 }catch(Exception ex){Console.WriteLine("FAIL "+(ex is InvalidOperationException?ex.Message:ex.GetType().Name));try{if(owned.Length>0&&Native.GetForegroundWindow()==main&&Read()==owned)ClearOwned();}catch{}return 1;}}
}

