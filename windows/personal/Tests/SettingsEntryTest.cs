using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using PersonalIme;
[ComImport,Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface EntryProfiles{[PreserveSig]int ActivateProfile(uint type,ushort lang,ref Guid id,ref Guid profile,IntPtr layout,uint flags);}
class SettingsEntryTest {
 [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
 [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadKeyboardLayout(string name,uint flags);
 static Form host;static int checks,exitCode=1;
 static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Console.WriteLine("PASS "+label);checks++;}
 static void Click(AutomationElement element,bool right){var r=element.Current.BoundingRectangle;if(r.IsEmpty||r.Width<1||r.Height<1||element.Current.IsOffscreen)throw new InvalidOperationException("Verified tray entry is not clickable");SetCursorPos((int)(r.Left+r.Width/2),(int)(r.Top+r.Height/2));mouse_event(right?8u:2u,0,0,0,UIntPtr.Zero);mouse_event(right?16u:4u,0,0,0,UIntPtr.Zero);}
 static AutomationElement Pick(AutomationElement root,bool modeOnly){
  var elements=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);AutomationElement fallback=null;
  foreach(AutomationElement e in elements){try{string n=e.Current.Name??"";if(e.Current.IsOffscreen||e.Current.BoundingRectangle.IsEmpty)continue;if(n.Contains("言渡")&&(n.Contains("中文输入")||n.Contains("English")))return e;if(n=="中"||n=="EN"||n.Contains("输入法模式"))return e;if(!modeOnly&&n.StartsWith("言渡输入法")&&n.Contains("右键"))fallback=e;}catch{}}
  return fallback;
 }
 static AutomationElement LocateEntry(){var taskbar=Native.FindWindow("Shell_TrayWnd",null);Check(taskbar!=IntPtr.Zero,"Windows taskbar located");var root=AutomationElement.FromHandle(taskbar);var found=Pick(root,true);if(found!=null){Console.WriteLine("entry=taskbar input mode");return found;}found=Pick(root,false);if(found!=null){Console.WriteLine("entry=assistant tray");return found;}
  var elements=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);foreach(AutomationElement e in elements){string n=e.Current.Name??"";if(n.Contains("隐藏的图标")&&!e.Current.IsOffscreen){Click(e,false);Thread.Sleep(400);break;}}
  foreach(var kind in new[]{"TopLevelWindowForOverflowXamlIsland","NotifyIconOverflowWindow"}){var h=Native.FindWindow(kind,null);if(h==IntPtr.Zero||!Native.IsWindowVisible(h))continue;found=Pick(AutomationElement.FromHandle(h),false);if(found!=null){Console.WriteLine("entry=assistant tray overflow");return found;}}
  return null;
 }
 static AutomationElement MenuItem(){var desktop=AutomationElement.RootElement;var children=desktop.FindAll(TreeScope.Children,Condition.TrueCondition);uint helperPid;Native.GetWindowThreadProcessId(Native.FindWindow(null,"PersonalTranslateIme.Agent.v1"),out helperPid);uint shellPid;Native.GetWindowThreadProcessId(Native.FindWindow("Shell_TrayWnd",null),out shellPid);foreach(AutomationElement root in children){if(root.Current.ProcessId!=Process.GetCurrentProcess().Id&&root.Current.ProcessId!=helperPid&&root.Current.ProcessId!=shellPid&&root.Current.ClassName!="#32768")continue;if(!root.Current.IsOffscreen){try{var items=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem));foreach(AutomationElement item in items)if((item.Current.Name??"").StartsWith("言渡设置"))return item;}catch{}}}return null;}
 static void Run(){IntPtr previous=Native.FindWindow(null,"言渡输入法 · 设置");try{
  ShowWindow(host.Handle,9);if(!Native.IsWindowVisible(host.Handle))ShowWindow(host.Handle,9);SetForegroundWindow(host.Handle);
  if(Native.GetForegroundWindow()!=host.Handle){keybd_event(0x12,0,0,UIntPtr.Zero);SetForegroundWindow(host.Handle);keybd_event(0x12,0,2,UIntPtr.Zero);}
  SetWindowPos(host.Handle,new IntPtr(-1),0,0,0,0,0x13);Thread.Sleep(120);var point=host.PointToScreen(new System.Drawing.Point(180,110));SetCursorPos(point.X,point.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Thread.Sleep(180);SetWindowPos(host.Handle,new IntPtr(-2),0,0,0,0,0x13);
  Check(Native.GetForegroundWindow()==host.Handle,"test editor foreground verified");
  var entry=LocateEntry();Check(entry!=null,"visible Yandu input-method entry found");
  var taskbar=AutomationElement.FromHandle(Native.FindWindow("Shell_TrayWnd",null)).Current.BoundingRectangle;
  int width=Math.Min(460,(int)taskbar.Width),height=(int)taskbar.Height;
  using(var bitmap=new Bitmap(width,height))using(var drawing=Graphics.FromImage(bitmap)){drawing.CopyFromScreen((int)taskbar.Right-width,(int)taskbar.Top,0,0,new Size(width,height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"taskbar-v033.png"));}
  Click(entry,true);AutomationElement menu=null;var deadline=DateTime.UtcNow.AddSeconds(8);while(menu==null&&DateTime.UtcNow<deadline){Thread.Sleep(100);menu=MenuItem();}Check(menu!=null,"right-click exposes clearly named Yandu Settings");Click(menu,false);
  IntPtr window=IntPtr.Zero;deadline=DateTime.UtcNow.AddSeconds(8);while(DateTime.UtcNow<deadline){window=Native.FindWindow(null,"言渡输入法 · 设置");if(window!=IntPtr.Zero&&Native.IsWindowVisible(window))break;Thread.Sleep(100);}Check(window!=IntPtr.Zero&&Native.IsWindowVisible(window),"right-click menu opens application settings");
  Thread.Sleep(300);Check(Native.GetForegroundWindow()==window,"settings brought to foreground instead of opening behind editor");var settings=AutomationElement.FromHandle(window);foreach(var title in new[]{"键盘","输入","翻译","隐私"})Check(settings.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,title))!=null,"settings category visible: "+title);
  if(previous==IntPtr.Zero){object p;if(settings.TryGetCurrentPattern(WindowPattern.Pattern,out p))((WindowPattern)p).Close();}
  Console.WriteLine("OK "+checks+" settings-entry checks; no configuration values read or changed");exitCode=0;
 }catch(Exception e){Console.WriteLine("FAIL "+(e is InvalidOperationException?e.Message:e.GetType().Name));}finally{if(!host.IsDisposed)host.BeginInvoke((Action)(()=>host.Close()));}}
 [STAThread]static int Main(){Console.SetOut(new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","v033-settings-entry.log"),false){AutoFlush=true});Native.SetProcessDPIAware();Application.EnableVisualStyles();host=new Form{Text="言渡设置入口测试 · 空白输入区",Width=640,Height=340,StartPosition=FormStartPosition.CenterScreen};var input=new RichTextBox{Dock=DockStyle.Fill};host.Controls.Add(input);var watchdog=new System.Windows.Forms.Timer{Interval=45000};watchdog.Tick+=delegate{Console.WriteLine("FAIL settings-entry test timeout");Environment.Exit(1);};host.Shown+=delegate{ShowWindow(host.Handle,9);SetWindowPos(host.Handle,new IntPtr(-1),0,0,0,0,3);SetForegroundWindow(host.Handle);input.Focus();
  var focusPoint=input.PointToScreen(new System.Drawing.Point(input.Width/2,input.Height/2));SetCursorPos(focusPoint.X,focusPoint.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);
Native.PostMessage(host.Handle,0x50,IntPtr.Zero,LoadKeyboardLayout("00000804",0));var clsid=new Guid("54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A");var profile=new Guid("D6F4BC2E-A620-46E4-BA20-1C9267529467");var manager=(EntryProfiles)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED")));Check(manager.ActivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x10000005)==0,"installed profile activated in empty test editor");Marshal.ReleaseComObject(manager);watchdog.Start();Task.Run(()=>{Thread.Sleep(1600);Run();});};Application.Run(host);watchdog.Dispose();return exitCode;}
}


