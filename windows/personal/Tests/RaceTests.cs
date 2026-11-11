using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using PersonalIme;
class DelayedHandler:HttpMessageHandler {
 public readonly List<TaskCompletionSource<HttpResponseMessage>> Pending=new List<TaskCompletionSource<HttpResponseMessage>>();
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t){var completion=new TaskCompletionSource<HttpResponseMessage>();Pending.Add(completion);return completion.Task;}
 public void Reply(int index,string text){var json=new JavaScriptSerializer().Serialize(new{choices=new[]{new{message=new{content=text}}}});Pending[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}
}
class RaceTests {
 static int checks;static void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static void Call(object o,string name,params object[] a){o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);}
 static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
 static Settings Config(){var c=new Settings();c.SetKey("sk-delayed-fixture");return c;}
 [STAThread]static int Main(){try{Application.EnableVisualStyles();DifferentSources();Preedit();Timeout();Console.WriteLine("OK "+checks+" delayed-state checks");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
 static void DifferentSources(){var handler=new DelayedHandler();using(var engine=new Translator(handler))using(var agent=new Agent(Config(),engine,true)){
  var a=new Endpoint();a.Source="甲句";a.Publish(agent.Handle);Pump(470);Check(handler.Pending.Count==1,"first slow request started");
  a.Source="乙句";a.Revision++;a.Publish(agent.Handle);Pump(30);var panel=(PanelForm)Field(agent,"panel");Check(((Snapshot)Field(agent,"snapshot")).Source=="乙句"&&panel.Translation.Text.Contains("正在翻译"),"new source clears previous preview immediately");
  handler.Reply(0,"WRONG OLD RESPONSE");Pump(440);Check(handler.Pending.Count==2,"new source gets its own request");Check((string)Field(agent,"translated")=="","late response cannot attach to new source");
  handler.Reply(1,"SECOND RESPONSE");Pump(60);Check((string)Field(agent,"translated")=="SECOND RESPONSE"&&(string)Field(agent,"translatedSource")=="乙句","response is bound to immutable source");
  a.Source="丙句";a.Revision++;a.Publish(agent.Handle);Pump(470);Check(!panel.Speaker.Visible,"old translation cannot be read during new request");
  var b=new Endpoint();b.Source="丙句";b.Publish(agent.Handle);Pump(470);Check(handler.Pending.Count==4,"same text in another input target starts independently");
  handler.Reply(2,"WRONG OTHER TARGET");Pump(50);Check((string)Field(agent,"translated")=="","previous target cannot overwrite active target");handler.Reply(3,"CORRECT TARGET");Pump(60);Check((string)Field(agent,"translated")=="CORRECT TARGET","active target finishes without getting stuck");
  b.Source="";b.Publish(agent.Handle,"inactive");Pump(50);Check(!panel.Visible&&panel.Translation.Text=="","switching IME hides and clears translation");a.DestroyHandle();b.DestroyHandle();
 }}
 static void Preedit(){var handler=new DelayedHandler();using(var engine=new Translator(handler))using(var agent=new Agent(Config(),engine,true)){
  var a=new Endpoint();a.Source="组合词";a.Publish(agent.Handle);Pump(470);
  for(int i=0;i<6;i++){a.Revision++;a.Publish(agent.Handle,"preedit");Pump(100);}Check(handler.Pending.Count==1,"repeated preedit signals do not cancel slow request");handler.Reply(0,"Combined phrase");Pump(60);Check((string)Field(agent,"translated")=="Combined phrase","slow request completes while preediting");Call(agent,"Apply",false);Pump(30);Check(a.Writes==0,"preedit blocks replacement even with a result");
  a.Publish(agent.Handle);Pump(40);Check((string)Field(agent,"translated")=="Combined phrase"&&handler.Pending.Count==1,"commit resume does not refetch same source");
  a.Source="组合词。";a.Revision++;a.Publish(agent.Handle);var punctuationDeadline=DateTime.UtcNow.AddSeconds(2);while((string)Field(agent,"translated")!="Combined phrase."&&DateTime.UtcNow<punctuationDeadline)Pump(20);if((string)Field(agent,"translated")!="Combined phrase.")Console.WriteLine(new JavaScriptSerializer().Serialize(Field(agent,"diagnosticHistory")));Check((string)Field(agent,"translated")=="Combined phrase."&&handler.Pending.Count==1,"punctuation after slow response stays local");
  a.Source="";a.Publish(agent.Handle,"clear");Pump(50);var panel=(PanelForm)Field(agent,"panel");Check(!panel.Visible&&panel.Translation.Text=="","cleared input cannot retain an unrelated preview");a.DestroyHandle();
 }}
 static void Timeout(){var handler=new DelayedHandler();using(var engine=new Translator(handler))using(var agent=new Agent(Config(),engine,true)){
  agent.TranslationTimeoutMilliseconds=200;var a=new Endpoint();a.Source="超时句";a.Publish(agent.Handle);Pump(900);var panel=(PanelForm)Field(agent,"panel");Check(panel.Translation.Text.Contains("超时")&&(bool)Field(agent,"failed"),"nonresponsive request has bounded visible timeout");
  panel.Translation.InvokeMouse(MouseButtons.Left,20,40);Pump(470);Check(handler.Pending.Count==2,"timeout can be retried without retyping");handler.Reply(1,"Retry succeeded");Pump(60);Check((string)Field(agent,"translated")=="Retry succeeded","retry completes normally");handler.Reply(0,"OLD TIMED OUT RESULT");Pump(40);Check((string)Field(agent,"translated")=="Retry succeeded","timed-out late result cannot replace retry result");
  agent.TranslationTimeoutMilliseconds=20000;a.Source="上下文失效";a.Revision++;a.Publish(agent.Handle,"text");Pump(2050);Check(!panel.Visible&&Field(agent,"snapshot")==null,"missing native heartbeat revokes stale panel and write target");a.DestroyHandle();
 }}
}

