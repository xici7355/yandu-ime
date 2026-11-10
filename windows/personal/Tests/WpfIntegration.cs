using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Interop;
using PersonalIme;
class WpfIntegration {
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static void Call(object o,string n,params object[] a){o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);}
 static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
 static void Check(bool b,string n){if(!b)throw new Exception(n);Console.WriteLine("PASS "+n);}
 [STAThread]static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
 static void Run(){Application.EnableVisualStyles();var cfg=new Settings();cfg.SetKey("sk-mock");var mock=new MockHandler();using(var engine=new Translator(mock))using(var agent=new Agent(cfg,engine,true)){
  var window=new System.Windows.Window{Title="言渡 TSF 输入兼容性测试",Width=700,Height=400};var input=new System.Windows.Controls.TextBox{FontSize=24,AcceptsReturn=true};window.Content=input;window.Show();var handle=new WindowInteropHelper(window).Handle;SetForegroundWindow(handle);input.Focus();Pump(300);
  var clsid=new Guid("54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A");var profile=new Guid("D6F4BC2E-A620-46E4-BA20-1C9267529467");var manager=(ProfileManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED")));Marshal.ThrowExceptionForHR(manager.ActivateProfile(1,0x804,ref clsid,ref profile,IntPtr.Zero,0x10000005));Pump(600);
  SendKeys.SendWait("nihao ");Pump(1200);Check(input.Text=="你好","WPF full pinyin commit");var snap=(Snapshot)Field(agent,"snapshot");Check(snap!=null&&snap.Source=="你好","WPF TSF source bookmark");Check((string)Field(agent,"translated")=="Hello.","WPF live preview");
  SendKeys.SendWait("shi");Pump(300);Check(((PanelForm)Field(agent,"panel")).Visible,"preview remains while typing next preedit");Call(agent,"Apply",false);Check(input.Text!="Hello.","preedit cannot be overwritten");SendKeys.SendWait("jie ");Pump(1000);Check(input.Text=="你好世界","WPF continuous commits");Check(((Snapshot)Field(agent,"snapshot")).Source=="你好世界","WPF whole source tracked");
  Call(agent,"Apply",false);Pump(500);Check(input.Text=="Hello.","WPF verified replacement");Call(agent,"Apply",true);Pump(600);Check(input.Text=="你好世界","WPF undo replacement");
  input.CaretIndex=0;Pump(300);Check(Field(agent,"snapshot")==null,"WPF caret invalidates write target");window.Close();Marshal.ReleaseComObject(manager);
 }Console.WriteLine("OK WPF TSF integration");}
}
