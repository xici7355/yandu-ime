using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PersonalIme;
class MockHandler : HttpMessageHandler {
 public int Calls; public string Body,Auth; public HttpStatusCode Status=HttpStatusCode.OK;public string Reply="{\"choices\":[{\"message\":{\"content\":\"Hello.\"}}]}";
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;Body=await request.Content.ReadAsStringAsync();Auth=request.Headers.Authorization.ToString();token.ThrowIfCancellationRequested();return new HttpResponseMessage(Status){Content=new StringContent(Reply,Encoding.UTF8,"application/json")};}
}
class Tests {
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static void Reject(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception(name);}
 static void Main(){Run().GetAwaiter().GetResult();Console.WriteLine("OK "+passed+" checks");}
 static async Task Run(){
  Check(Settings.ChatUri("https://api.deepseek.com/v1").AbsoluteUri=="https://api.deepseek.com/v1/chat/completions","base URL");
  Check(Settings.ChatUri("https://api.deepseek.com/chat/completions").AbsoluteUri=="https://api.deepseek.com/chat/completions","full URL");
  Check(Settings.ChatUri("http://localhost:1234/v1").IsLoopback,"local model URL");
  Reject(()=>Settings.ChatUri("sk-example"),"key is not endpoint");Reject(()=>Settings.ChatUri("http://remote.example/v1"),"remote HTTP rejected");Reject(()=>Settings.ChatUri("https://x.example/v1?key=test"),"query secrets rejected");
  var config=new Settings();config.SetKey("sk-fixture-not-a-real-key");Check(config.Key()=="sk-fixture-not-a-real-key","DPAPI roundtrip");Check(!config.ProtectedKey.Contains("sk-fixture"),"key encrypted");Check(config.Ready,"configured");config.PauseTranslation=true;Check(!config.Ready,"privacy pause");config.PauseTranslation=false;
  string core,tail;Translator.SplitTail("你好，。",out core,out tail);Check(core=="你好"&&tail==",.","trailing punctuation");Translator.SplitTail("你好，朋友",out core,out tail);Check(core=="你好，朋友"&&tail=="","interior punctuation preserved");Check(Translator.HasChinese("你好")&&!Translator.HasChinese("hello"),"language detection");
  var handler=new MockHandler();using(var engine=new Translator(handler)){
   Check(await engine.Translate(config,"你好。",CancellationToken.None)=="Hello.","translation response");
   Check(await engine.Translate(config,"你好，",CancellationToken.None)=="Hello,","punctuation local conversion");Check(handler.Calls==1,"punctuation no request");
   Check(await engine.Translate(config,"你好",CancellationToken.None)=="Hello.","cached sentence");Check(handler.Calls==1,"cache hit");
   Check(handler.Auth=="Bearer sk-fixture-not-a-real-key","auth header");Check(handler.Body.Contains("stream")&&handler.Body.Contains("user"),"OpenAI compatible payload");
   await engine.Translate(config,"你，好",CancellationToken.None);Check(handler.Calls==2,"interior edit new request");engine.Clear();await engine.Translate(config,"你好",CancellationToken.None);Check(handler.Calls==3,"cache clear");
   handler.Status=HttpStatusCode.Unauthorized;try{await engine.Translate(config,"失败",CancellationToken.None);throw new Exception("HTTP status");}catch(InvalidOperationException ex){Check(ex.Message.Contains("401"),"HTTP error status");}
   handler.Status=HttpStatusCode.OK;handler.Reply="{}";try{await engine.Translate(config,"格式",CancellationToken.None);throw new Exception("format");}catch(InvalidOperationException){Check(true,"malformed response");}
   var cts=new CancellationTokenSource();cts.Cancel();try{await engine.Translate(config,"取消",cts.Token);throw new Exception("cancel");}catch(OperationCanceledException){Check(true,"cancellation");}
  }
  var sentenceHandler=new MockHandler();sentenceHandler.Reply="{\"choices\":[{\"message\":{\"content\":\"I'm still testing this version to see how well it works.\"}}]}";
  using(var engine=new Translator(sentenceHandler,LocalDictionary.Shared)){
   string sentence="我还在测试这个版本使用起来怎么样";
   string result=await engine.Translate(config,sentence,CancellationToken.None);
   var payload=new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(sentenceHandler.Body) as System.Collections.Generic.Dictionary<string,object>;
   var messages=(object[])payload["messages"];var user=(System.Collections.Generic.Dictionary<string,object>)messages[1];var system=(System.Collections.Generic.Dictionary<string,object>)messages[0];
   Check((string)user["content"]==sentence,"entire reported sentence reaches API without dropping first-person prefix");
   Check(((string)system["content"]).Contains("declarative statement")&&((string)system["content"]).Contains("tense/aspect"),"translation instruction preserves statement and aspect");
   Check(result.StartsWith("I'm still testing")&&sentenceHandler.Calls==1,"sentence bypasses word dictionary and keeps full model response");
  }
  Check(WeChatEditor.CanAttach("我还在测试这个版本使用起来怎么样","测试这个版本使用起来怎么样"),"WeChat full draft can attach to verified current suffix");
  Check(!WeChatEditor.CanAttach("另一个输入框的文字","之前的文字")&&!WeChatEditor.CanAttach("password=fixture你好","你好"),"WeChat mismatched or sensitive full draft cannot attach");
  var snap=Snapshot.Parse("S|42|1234|5678|100|200\n你好");Check(snap!=null&&snap.Revision==42&&snap.Source=="你好"&&snap.X==100,"native snapshot");Check(Snapshot.Parse("S|bad\ntext")==null,"invalid snapshot");
 }
}
