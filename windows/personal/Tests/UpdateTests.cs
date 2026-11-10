using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PersonalIme;
class UpdateTests {
 static int checks;
 static readonly byte[] bytes=Encoding.ASCII.GetBytes("MZ public update download fixture; never executed");
 static readonly string root=Path.Combine("personal","bin","update-tests-"+Guid.NewGuid().ToString("N"));
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
 static async Task Reject(Func<Task> action,string label){bool rejected=false;try{await action();}catch(Exception){rejected=true;}Check(rejected,label);}
 static string Hash(byte[] value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
 static string Json(string tag,string assetDigest,long size,string url,bool prerelease){return "{\"tag_name\":\""+tag+"\",\"html_url\":\"https://github.com/test/yandu/releases/tag/"+tag+"\",\"prerelease\":"+(prerelease?"true":"false")+",\"assets\":[{\"name\":\"Yandu-Setup-x64.exe\",\"size\":"+size+",\"digest\":\""+assetDigest+"\",\"browser_download_url\":\""+url+"\"}]}";}
 static string GoodJson(){return Json("v9.0.0","sha256:"+Hash(bytes),bytes.Length,"https://github.com/test/yandu/releases/download/v9.0.0/Yandu-Setup-x64.exe",false);}
 sealed class Fake:HttpMessageHandler {
  public string Json=GoodJson();public HttpStatusCode Status=HttpStatusCode.OK;public byte[] Data=bytes;public bool Redirect,UnsafeRedirect;public int Calls;public bool PrivateDataSent;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   token.ThrowIfCancellationRequested();Calls++;if(request.Method!=HttpMethod.Get||request.Headers.Authorization!=null||request.Content!=null)PrivateDataSent=true;
   if(request.RequestUri.Host=="api.github.com")return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(Json,Encoding.UTF8,"application/json")});
   if(Redirect&&request.RequestUri.Host=="github.com"){var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=new Uri(UnsafeRedirect?"http://evil.invalid/setup.exe":"https://release-assets.githubusercontent.com/fixture/Yandu.exe");return Task.FromResult(response);}
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Data)});
  }
 }
 static string Folder(){return Path.Combine(root,Guid.NewGuid().ToString("N"));}
 static async Task Run(){
  Check(Updates.CurrentText=="1.1.1","current version from application assembly");
  var originalSettings=new Settings{Endpoint="https://example.invalid/v1",Model="fixture-model"};originalSettings.SetKey("fixture-only-key");
  var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();string legacy=serializer.Serialize(originalSettings);legacy=legacy.Substring(0,legacy.Length-1)+",\"UpdateRepository\":\"other/repository\"}";
  var migrated=serializer.Deserialize<Settings>(legacy);
  Check(migrated.Endpoint==originalSettings.Endpoint&&migrated.Model==originalSettings.Model&&migrated.Key()=="fixture-only-key","legacy settings preserve endpoint model and encrypted key");
  Check(typeof(Settings).GetField("UpdateRepository")==null&&Updates.OfficialRepository=="xici7355/yandu-ime","legacy custom repository cannot override built-in source");
  Check(Updates.Repository("")=="","unset repository remains empty");
  Check(Updates.Repository(" https://github.com/test/yandu.git/ ")=="test/yandu","GitHub URL normalized");
  Check(Updates.Repository("test/yandu")=="test/yandu","owner/repository accepted");
  foreach(string input in new[]{"https://evil.invalid/test/yandu","test/yandu?token=secret","test/yandu/releases","../test","test/.."}){await Reject(()=>Task.Run(()=>Updates.Repository(input)),"invalid repository rejected");}
  Check(Updates.ParseVersion("v1.10.0")>Updates.ParseVersion("v1.9.9"),"versions compared numerically");
  Check(Updates.ParseVersion("v1.1.1")==Updates.CurrentVersion,"three-part tag equals installed four-part assembly version");
  await Reject(()=>Task.Run(()=>Updates.ParseVersion("v9.0.0-beta")),"prerelease tag cannot become stable update");
  var handler=new Fake();using(var client=new Updates(handler)){
   var release=await client.Check("test/yandu",CancellationToken.None);Check(release.Installer!=null&&release.Version>Updates.CurrentVersion,"new stable x64 installer selected");
   string folder=Folder();var values=new System.Collections.Generic.List<int>();string file=await client.Download(release,folder,new ImmediateProgress(values),CancellationToken.None);Check(File.Exists(file)&&Hash(File.ReadAllBytes(file))==Hash(bytes),"downloaded executable bytes hash verified");Check(values.Count>0&&values[values.Count-1]==100,"download completion only after hash check");Check(!File.Exists(file+".part"),"partial suffix absent after success");Check(!handler.PrivateDataSent,"update requests contain no authorization, API key, input or POST body");
  }
  using(var client=new Updates(new Fake{Status=HttpStatusCode.NotFound})){await Reject(()=>client.Check("test/yandu",CancellationToken.None),"missing repository or release reported");}
  using(var client=new Updates(new Fake{Status=HttpStatusCode.Forbidden})){await Reject(()=>client.Check("test/yandu",CancellationToken.None),"GitHub rate limit reported");}
  using(var client=new Updates(new Fake{Json=Json("v9.0.0","sha256:"+Hash(bytes),bytes.Length,"https://github.com/test/yandu/releases/download/v9.0.0/x.exe",true)})){await Reject(()=>client.Check("test/yandu",CancellationToken.None),"prerelease release ignored");}
  using(var client=new Updates(new Fake{Json="{\"tag_name\":\"v9.0.0\",\"html_url\":\"https://github.com/test/yandu/releases/tag/v9.0.0\",\"assets\":[]}"})){var result=await client.Check("test/yandu",CancellationToken.None);Check(result.Installer==null,"release without Windows asset cannot download");}
  using(var client=new Updates(new Fake{Json=Json("v9.0.0","sha256:"+Hash(bytes),bytes.Length,"https://github.com/other/repo/releases/download/v9.0.0/x.exe",false)})){await Reject(()=>client.Check("test/yandu",CancellationToken.None),"cross-repository installer URL rejected");}
  using(var client=new Updates(new Fake{Json=Json("v9.0.0","",bytes.Length,"https://github.com/test/yandu/releases/download/v9.0.0/x.exe",false)})){var result=await client.Check("test/yandu",CancellationToken.None);await Reject(()=>client.Download(result,Folder(),null,CancellationToken.None),"missing SHA256 prevents download and execution");}
  using(var client=new Updates(new Fake{Json=Json("v9.0.0","sha256:"+new string('0',64),bytes.Length,"https://github.com/test/yandu/releases/download/v9.0.0/x.exe",false)})){var result=await client.Check("test/yandu",CancellationToken.None);string folder=Folder();await Reject(()=>client.Download(result,folder,null,CancellationToken.None),"corrupted hash rejected");Check(Directory.GetFiles(folder).Length==0,"failed hash removes incomplete file");}
  using(var client=new Updates(new Fake{Data=new byte[2]})){var result=await client.Check("test/yandu",CancellationToken.None);await Reject(()=>client.Download(result,Folder(),null,CancellationToken.None),"truncated download rejected");}
  using(var client=new Updates(new Fake{Data=new byte[200]})){var result=await client.Check("test/yandu",CancellationToken.None);await Reject(()=>client.Download(result,Folder(),null,CancellationToken.None),"oversized download rejected");}
  using(var client=new Updates(new Fake{Redirect=true,UnsafeRedirect=true})){var result=await client.Check("test/yandu",CancellationToken.None);await Reject(()=>client.Download(result,Folder(),null,CancellationToken.None),"insecure redirect rejected");}
  using(var client=new Updates(new Fake{Redirect=true})){var result=await client.Check("test/yandu",CancellationToken.None);Check(File.Exists(await client.Download(result,Folder(),null,CancellationToken.None)),"official GitHub release asset redirect supported");}
  using(var client=new Updates(new Fake()))using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();await Reject(()=>client.Check("test/yandu",cancellation.Token),"cancelled check stops");}
  Console.WriteLine("OK "+checks+" update checks; isolated fixtures, no external release installed");
 }
 class ImmediateProgress:IProgress<int>{readonly System.Collections.Generic.List<int> values;public ImmediateProgress(System.Collections.Generic.List<int> list){values=list;}public void Report(int value){values.Add(value);}}
 static int Main(){try{Run().GetAwaiter().GetResult();return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
}
