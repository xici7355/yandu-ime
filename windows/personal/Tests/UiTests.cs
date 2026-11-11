using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PersonalIme;
class Endpoint : NativeWindow {
 public string Source="你好",Replacement;public long Revision=42;public int Writes;
 public Endpoint(){CreateHandle(new CreateParams {Caption="PersonalIme.Test.Endpoint",Parent=new IntPtr(-3)});}
 public void Publish(IntPtr agent,string mode="legacy"){Native.Send(agent,Handle,"S|"+Revision+"|"+Handle.ToInt64()+"|"+Native.GetForegroundWindow().ToInt64()+"|100|200|"+mode+"\n"+Source);}
 protected override void WndProc(ref Message m){if(m.Msg==Native.WM_COPYDATA){string s=Native.Read(m.LParam);if(s!=null&&s.StartsWith("R|")){int n=s.IndexOf('\n');var fields=s.Substring(0,n).Split('|');long rev=Int64.Parse(fields[1]);int count=Int32.Parse(fields[2]);string expected=s.Substring(n+1,count);Replacement=s.Substring(n+1+count);bool ok=rev==Revision&&expected==Source;if(ok){Source=Replacement;Writes++;Revision++;}Native.Send(m.WParam,Handle,"A|"+rev+"|"+(ok?"1":"0"));m.Result=new IntPtr(1);return;}}base.WndProc(ref m);}
}
class UiTests {
 static Form fixtureHost;static int checks;static void Check(bool b,string title){if(!b){Console.WriteLine("FAIL "+title);throw new Exception(title);}Console.WriteLine("PASS "+title);checks++;}
 static object Field(object o,string field){return o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static void Call(object o,string method,params object[] args){o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
 static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(5);}}
 [STAThread]static void Main(){try{Run();}catch(Exception error){Console.WriteLine("FAIL "+error.GetType().Name+": "+error.Message);Environment.ExitCode=1;}finally{if(fixtureHost!=null)fixtureHost.Dispose();}}
 static void Run(){Application.EnableVisualStyles();fixtureHost=new Form{Text="Yandu isolated input regression",StartPosition=FormStartPosition.CenterScreen,Width=500,Height=180,TopMost=true};fixtureHost.Controls.Add(new Label{Dock=DockStyle.Fill,Text="固定输入状态验证，不发送消息",TextAlign=ContentAlignment.MiddleCenter});fixtureHost.Show();fixtureHost.Activate();Pump(150);var config=new Settings();config.SetKey("sk-ui-fixture");var handler=new MockHandler();using(var engine=new Translator(handler))using(var agent=new Agent(config,engine,true)){
  var endpoint=new Endpoint();endpoint.Publish(agent.Handle);Pump(750);
  Check((string)Field(agent,"translated")=="Hello.","live preview");Check(handler.Calls==1,"debounce one request");
  endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(100);
  Check((string)Field(agent,"translated")=="Hello." && handler.Calls==1,"revision-only change keeps preview without request");
  endpoint.Source="";endpoint.Revision++;endpoint.Publish(agent.Handle,"preedit");Pump(100);
  Check((string)Field(agent,"translated")=="Hello." && (bool)Field(agent,"composing"),"transient preedit retains preview but revokes write target");
  Call(agent,"Apply",false);Check(endpoint.Writes==0,"suspended preview cannot write");
  endpoint.Source="你好";endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(100);
  Check((string)Field(agent,"translated")=="Hello." && handler.Calls==1,"same source revalidates without flash or network");
  endpoint.Source="你好，";endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(700);Check((string)Field(agent,"translated")=="Hello,","preview terminal punctuation");Check(handler.Calls==1,"preview punctuation cache");
  ((PanelForm)Field(agent,"panel")).Translation.InvokeMouse(MouseButtons.Left,20,40);Pump(120);Check(endpoint.Source=="Hello,"&&endpoint.Writes==1,"shared translation action replaces source");
  endpoint.Publish(agent.Handle);Pump(50);Check(((PanelForm)Field(agent,"panel")).UndoAvailable,"undo available");
  ((PanelForm)Field(agent,"panel")).Translation.InvokeMouse(MouseButtons.Left,20,40);Pump(100);Check(endpoint.Source=="你好，"&&endpoint.Writes==2,"same translation action restores original punctuation");
  endpoint.Publish(agent.Handle);Pump(650);Call(agent,"ResetTranslation");Call(agent,"Apply",false);Check(endpoint.Writes==2,"stale preview cannot replace");
  config.PauseTranslation=true;endpoint.Source="新的文字";endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(650);Check(!((PanelForm)Field(agent,"panel")).Visible,"privacy hides preview");Check(handler.Calls==1,"privacy no request");
  endpoint.DestroyHandle();
 }
 OfflineInput();
 InlineExpansion();
 var settings=new Settings();using(var engine=new Translator(new MockHandler()))using(var form=new SettingsForm(settings,engine,delegate{})){
  form.Show();Pump(100);var layout=(TableLayoutPanel)form.Controls[0];var tabs=(Panel)layout.GetControlFromPosition(0,2);Check(tabs.Controls.Count==4,"four settings categories");
  Panel translationPage=null;foreach(Panel page in tabs.Controls)if((string)page.Tag=="翻译")translationPage=page;
  Check(translationPage!=null&&translationPage.Visible,"first configuration opens translation page");
  using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save("personal/bin/settings-preview.png");}
  var nav=(FlowLayoutPanel)layout.GetControlFromPosition(0,1);((Button)nav.Controls[2]).PerformClick();Pump(50);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save("personal/bin/api-preview.png");}
  form.Close();
 }
 Console.WriteLine("OK "+checks+" UI checks");}
 static void OfflineInput(){var handler=new MockHandler();var dictionary=new LocalDictionary(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionary"));using(var engine=new Translator(handler,dictionary))using(var agent=new Agent(new Settings(),engine,true)){
  var endpoint=new Endpoint();endpoint.Source="输入法";endpoint.Publish(agent.Handle);Pump(1300);var panel=(PanelForm)Field(agent,"panel");Check(((string)Field(agent,"translated")).Contains("input method")&&panel.Visible&&handler.Calls==0,"input preview uses real dictionary without API");Check(panel.Hint.Text.Contains("本地词典"),"input preview identifies local origin");
  endpoint.Source="输入法，";endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(700);Check(((string)Field(agent,"translated")).EndsWith(",")&&handler.Calls==0,"offline input terminal punctuation stays local");
  endpoint.Source="这段文字没有完整的词典匹配项";endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(700);Check(!panel.Visible&&handler.Calls==0,"offline dictionary miss leaves ordinary input without error card");
  endpoint.Source="password=fixture";endpoint.Revision++;endpoint.Publish(agent.Handle);Pump(80);Check(!panel.Visible&&Field(agent,"snapshot")==null&&handler.Calls==0,"sensitive input clears preview before translation");endpoint.DestroyHandle();
 }}
 static void InlineExpansion(){using(var engine=new Translator(new MockHandler()))using(var agent=new Agent(new Settings(),engine,true)){
  var panel=(PanelForm)Field(agent,"panel");Check(typeof(PanelForm).GetField("Original")==null,"input card has no duplicate original-text label");var text="This translation stays beside the input field. Right-click to expand the remaining lines without opening another window. The keyboard focus stays in the original editor, and clicking the translation still replaces the matching source. ";
  typeof(Agent).GetField("translated",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(agent,text);panel.Translation.Text=text;panel.PositionAt(100,100);panel.ShowPassive();Pump(50);
  panel.SavePreview("personal/bin/v212-compact-preview.png");var handle=panel.Handle;int initialHeight=panel.Height,forms=Application.OpenForms.Count;var foreground=Native.GetForegroundWindow();
  panel.Translation.InvokeMouse(MouseButtons.Right,20,40);Pump(30);
  Check(panel.Expanded&&panel.Height>initialHeight,"right-click expands existing translation card");
  Check(panel.Handle==handle&&Application.OpenForms.Count==forms,"expansion creates no separate window");
  Check(Native.GetForegroundWindow()==foreground,"inline expansion keeps editor focus");
  panel.SavePreview("personal/bin/translation-inline-expanded.png");
  panel.Translation.InvokeMouse(MouseButtons.Right,20,40);Check(!panel.Expanded&&panel.Height==initialHeight,"second right-click restores compact preview");
  var longText="";for(int i=0;i<100;i++)longText+=text;typeof(Agent).GetField("translated",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(agent,longText);panel.Translation.Text=longText;
  panel.Translation.InvokeMouse(MouseButtons.Right,20,40);Check(panel.Expanded&&panel.ScrollDown.Visible,"very long translation exposes in-card scrolling");
  panel.ScrollDown.InvokeMouse(MouseButtons.Left,5,5);Check(panel.ScrollOffset>0,"in-card arrow reveals remaining translation");panel.ScrollUp.InvokeMouse(MouseButtons.Left,5,5);Check(panel.ScrollOffset==0,"in-card arrow returns to translation start");
  panel.Scroll(2);Check(panel.ScrollOffset>0,"expanded card supports wheel scrolling");
  panel.Hide();Check(!panel.Expanded&&panel.ScrollOffset==0&&panel.Height==initialHeight,"hidden card resets expansion and scrolling");
 }}
}

