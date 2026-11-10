using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonalIme;

class DictionaryPrivacyTests {
 static int count;static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
 static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);}
 static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
 sealed class Api:HttpMessageHandler {public int Calls;public string Body,Reply="The meeting starts this afternoon.";protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;Body=await request.Content.ReadAsStringAsync();return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{choices=new[]{new{message=new{content=Reply}}}}),Encoding.UTF8,"application/json")};}}
 [STAThread]static int Main(){try{Native.SetProcessDPIAware();Application.EnableVisualStyles();Privacy().GetAwaiter().GetResult();Words().GetAwaiter().GetResult();Surface();Console.WriteLine("OK "+count+" dictionary and forced-privacy checks");return 0;}catch(Exception ex){Console.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message);return 1;}}
 static async Task Privacy(){
  string key="sk-"+new string('a',32);var cases=new[]{key,"sk- \n"+new string('a',32),"password = example-secret","我的密码是abc123","API_KEY: example-secret","Authorization: Bearer example","contact@example.invalid","13800138000","138 0013 8000","110101199001011234","4111 1111 1111 1111","https://"+"user:example@fixture.invalid/","-----BEGIN "+"PRIVATE KEY-----"};
  var words=new LocalDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionary"));var api=new Api();var settings=new Settings();settings.SetKey("fixture-only-key");using(var engine=new Translator(api,words)){
   foreach(var value in cases){bool blocked=false;try{await engine.TranslateReading(settings,value,CancellationToken.None);}catch(SensitiveContentException){blocked=true;}Check(blocked,"sensitive source blocked before request and cache");}
   Check(api.Calls==0,"blocked sources generate zero HTTP requests");Check(Field<IDictionary>(engine,"cache").Count==0,"blocked sources never enter translation cache");var lazy=typeof(LocalDictionary).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(words);Check(!(bool)lazy.GetType().GetProperty("IsValueCreated").GetValue(lazy,null),"sensitive input is rejected before dictionary lookup");
   bool refinementBlocked=false;try{await engine.TranslateReadingDetailed(settings,"password=example",CancellationToken.None,true);}catch(SensitiveContentException){refinementBlocked=true;}Check(refinementBlocked&&api.Calls==0,"AI refinement cannot bypass mandatory protection");
  }
  api=new Api{Reply="password = returned-secret"};using(var engine=new Translator(api)){bool blocked=false;try{await engine.Translate(settings,"这是一条普通的固定测试句",CancellationToken.None);}catch(SensitiveContentException){blocked=true;}Check(blocked&&Field<IDictionary>(engine,"cache").Count==0,"sensitive API output is not cached or displayed");}
  Check(!PrivacyPolicy.IsSensitive("请使用输入法翻译这句话")&&!PrivacyPolicy.IsSensitive("password"),"ordinary text and dictionary word remain usable");
  Check(typeof(Settings).GetField("DisablePasswordProtection")==null&&typeof(Settings).GetField("DisableSensitiveProtection")==null,"no settings switch can disable mandatory protection");
 }
 static async Task Words(){var api=new Api();var words=new LocalDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionary"));var watch=Stopwatch.StartNew();string value;
  Check(words.TryLookup("输入法",false,true,out value)&&value.Contains("input method"),"real CC-CEDICT Chinese word lookup");watch.Stop();Console.WriteLine("first_dictionary_load_ms="+watch.ElapsedMilliseconds);
  Check(words.TryLookup("input method",true,true,out value)&&value.Contains("输入法"),"real CC-CEDICT reverse English lookup");Check(words.TryLookup("一路平安",false,true,out value)&&value.Contains("journey"),"complete fixed phrase translated locally");Check(!words.TryLookup("这段完整句子没有对应的固定词条所以不能拆开拼接",false,true,out value),"unknown sentence is not assembled from separate words");
  var settings=new Settings();using(var engine=new Translator(api,words)){
   var result=await engine.TranslateReadingDetailed(settings,"你好",CancellationToken.None);Check(result.Origin=="本地词典"&&result.Text.Contains("hello")&&api.Calls==0,"dictionary works without API and makes no request");
   result=await engine.TranslateReadingDetailed(settings,"HELLO!",CancellationToken.None);Check(result.Origin=="本地词典"&&result.Text.Contains("你好")&&result.Text.EndsWith("！"),"English word lookup and target punctuation stay offline");
   bool missing=false;try{await engine.TranslateReadingDetailed(settings,"这句话在本地词典中没有完整匹配项",CancellationToken.None);}catch(InvalidOperationException){missing=true;}Check(missing&&api.Calls==0,"dictionary miss without API never creates an outgoing request");
   settings.SetKey("fixture-only-key");string source="请使用输入法发送完整的会议通知给同事";result=await engine.TranslateReadingDetailed(settings,source,CancellationToken.None);Check(result.Origin=="AI翻译"&&api.Calls==1,"whole-sentence miss falls back to configured API");Check(api.Body.Contains("CC-CEDICT")&&api.Body.Contains("input method"),"AI receives relevant public glossary rather than user history");
   result=await engine.TranslateReadingDetailed(settings,source,CancellationToken.None);Check(result.Origin=="内存缓存"&&api.Calls==1,"repeated sentence uses memory cache without another API call");
   result=await engine.TranslateReadingDetailed(settings,"输入法",CancellationToken.None,true);Check(result.Origin=="AI翻译"&&api.Calls==2,"explicit AI refinement bypasses dictionary only when requested");
  }
 }
 static void Surface(){var bounds=SystemInformation.VirtualScreen;var work=Screen.PrimaryScreen.WorkingArea;var region=new Rectangle(work.Left+130,work.Top+240,300,32);var api=new Api();var words=new LocalDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionary"));
  using(var engine=new Translator(api,words))using(var window=new ScreenTranslation(new Settings(),engine,new Bitmap(bounds.Width,bounds.Height),bounds,region,null,null,"hello")){
   window.Show();var end=DateTime.UtcNow.AddSeconds(5);while(!Field<bool>(window,"ready")&&DateTime.UtcNow<end)Pump(20);Check(Field<bool>(window,"ready")&&Field<Label>(window,"status").Text.Contains("本地词典")&&api.Calls==0,"overlay shows offline dictionary origin");Check(!Field<Button>(window,"retry").Enabled&&Field<Button>(window,"copy").Enabled,"offline result can be copied while unavailable AI refinement stays disabled");window.Close();
  }
  var settings=new Settings();settings.SetKey("fixture-only-key");using(var engine=new Translator(api,words))using(var window=new ScreenTranslation(settings,engine,new Bitmap(bounds.Width,bounds.Height),bounds,region,null,null,"password = source-secret")){
   window.Show();Pump(150);Check(Field<bool>(window,"blocked")&&!Field<Button>(window,"copy").Enabled&&!Field<Button>(window,"copySource").Enabled&&Field<string>(window,"source")=="","sensitive selection is masked and removed from result state");Check(!Field<Button>(window,"retry").Enabled&&api.Calls==0,"blocked content cannot be retried into an API request");window.Close();
  }
 }
}
