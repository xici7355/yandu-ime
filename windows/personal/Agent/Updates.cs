using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace PersonalIme {
 internal sealed class ReleaseInfo {
  public string tag_name,html_url;
  public bool draft,prerelease;
  public ReleaseAsset[] assets;
  public Version Version;
  public ReleaseAsset Installer;
 }
 internal sealed class ReleaseAsset {
  public string name,browser_download_url,digest;
  public long size;
 }
 internal sealed class Updates : IDisposable {
  readonly HttpClient client;
  public static readonly Version CurrentVersion=typeof(Updates).Assembly.GetName().Version;
  public static string CurrentText { get {return CurrentVersion.ToString(3);} }
  public Updates():this(new HttpClientHandler{AllowAutoRedirect=false}){}
  internal Updates(HttpMessageHandler handler){client=new HttpClient(handler);client.Timeout=TimeSpan.FromMinutes(5);client.DefaultRequestHeaders.UserAgent.ParseAdd("Yandu-IME/"+CurrentText);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2026-03-10");}
  public static string Repository(string input){
   string value=(input??"").Trim().TrimEnd('/');
   if(value.Length==0)return "";
   if(value.StartsWith("https://github.com/",StringComparison.OrdinalIgnoreCase))value=value.Substring(19);
   if(value.EndsWith(".git",StringComparison.OrdinalIgnoreCase))value=value.Substring(0,value.Length-4);
   if(!Regex.IsMatch(value,@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9_.-]{1,100}$")||value.EndsWith("/.")||value.EndsWith("/.."))throw new ArgumentException("请填写 GitHub 仓库地址或 用户名/仓库名。");
   return value;
  }
  internal static Version ParseVersion(string tag){Version version;if(tag==null||!Regex.IsMatch(tag,@"^v?\d+\.\d+\.\d+(?:\.\d+)?$")||!Version.TryParse(tag.TrimStart('v'),out version))throw new InvalidDataException("发布版本号需使用 v0.3.4 这样的格式。");return new Version(version.Major,version.Minor,version.Build,Math.Max(0,version.Revision));}
  internal static bool AllowedDownload(Uri uri){return uri!=null&&uri.Scheme=="https"&&String.IsNullOrEmpty(uri.UserInfo)&&(uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".githubusercontent.com",StringComparison.OrdinalIgnoreCase));}
  public async Task<ReleaseInfo> Check(string repository,CancellationToken token){
   string repo=Repository(repository);if(repo.Length==0)throw new ArgumentException("请先填写 GitHub 更新仓库。");
   using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
    deadline.CancelAfter(TimeSpan.FromSeconds(20));
    using(var response=await client.GetAsync("https://api.github.com/repos/"+repo+"/releases/latest",HttpCompletionOption.ResponseHeadersRead,deadline.Token)){
     if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidOperationException("仓库不存在、尚未公开，或还没有正式发布版本。");
     if(response.StatusCode==HttpStatusCode.Forbidden||(int)response.StatusCode==429)throw new InvalidOperationException("GitHub 请求暂时受限，请稍后再试。");
     response.EnsureSuccessStatusCode();
     if(response.Content.Headers.ContentLength>1024*1024)throw new InvalidDataException("更新信息过大。");
     string json;using(var stream=await response.Content.ReadAsStreamAsync())using(var memory=new MemoryStream()){var buffer=new byte[16384];int n;while((n=await stream.ReadAsync(buffer,0,buffer.Length,deadline.Token))>0){if(memory.Length+n>1024*1024)throw new InvalidDataException("更新信息过大。");memory.Write(buffer,0,n);}json=System.Text.Encoding.UTF8.GetString(memory.ToArray());}
     var release=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<ReleaseInfo>(json);
     if(release==null||release.draft||release.prerelease)throw new InvalidDataException("没有可用的正式发布版本。");
     release.Version=ParseVersion(release.tag_name);
     Uri page;if(!Uri.TryCreate(release.html_url,UriKind.Absolute,out page)||!page.Scheme.Equals("https")||!page.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||!page.AbsolutePath.StartsWith("/"+repo+"/releases/",StringComparison.OrdinalIgnoreCase)||!String.IsNullOrEmpty(page.UserInfo))throw new InvalidDataException("发布页面地址无效。");
     foreach(var asset in release.assets??new ReleaseAsset[0])if(asset.name=="Yandu-Setup-x64.exe"){
      Uri url;if(!Uri.TryCreate(asset.browser_download_url,UriKind.Absolute,out url)||!url.Scheme.Equals("https")||!url.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||!url.AbsolutePath.StartsWith("/"+repo+"/releases/download/",StringComparison.OrdinalIgnoreCase)||!String.IsNullOrEmpty(url.UserInfo))throw new InvalidDataException("安装包地址与更新仓库不一致。");
      if(asset.size<=0||asset.size>200L*1024*1024)throw new InvalidDataException("安装包大小无效。");
      release.Installer=asset;break;
     }
     return release;
    }
   }
  }
  public async Task<string> Download(ReleaseInfo release,string folder,IProgress<int> progress,CancellationToken token){
   if(release==null||release.Version<=CurrentVersion||release.Installer==null)throw new InvalidOperationException("没有可下载的新版安装包。");
   var asset=release.Installer;
   if(!Regex.IsMatch(asset.digest??"",@"^sha256:[A-Fa-f0-9]{64}$"))throw new InvalidDataException("该发布缺少 SHA-256 校验信息，请联系维护者重新上传安装包。");
   Uri url;if(!Uri.TryCreate(asset.browser_download_url,UriKind.Absolute,out url)||!AllowedDownload(url))throw new InvalidDataException("下载地址无效。");
   Directory.CreateDirectory(folder);string part=Path.Combine(folder,"Yandu-Setup-x64.exe.part"),target=Path.Combine(folder,"Yandu-Setup-x64.exe");
   try{
    using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
     deadline.CancelAfter(TimeSpan.FromMinutes(5));
     HttpResponseMessage response=null;
     try{
      for(int i=0;i<6;i++){
       response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
       int code=(int)response.StatusCode;
       if(code==301||code==302||code==303||code==307||code==308){Uri redirect=response.Headers.Location;if(redirect!=null&&!redirect.IsAbsoluteUri)redirect=new Uri(url,redirect);response.Dispose();response=null;if(!AllowedDownload(redirect))throw new InvalidDataException("下载跳转地址无效。");url=redirect;continue;}break;
      }
      if(response==null)throw new InvalidDataException("下载跳转次数过多。");response.EnsureSuccessStatusCode();
      long done=0;
      using(var input=await response.Content.ReadAsStreamAsync())using(var output=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
       byte[] buffer=new byte[65536];int count;while((count=await input.ReadAsync(buffer,0,buffer.Length,deadline.Token))>0){done+=count;if(done>asset.size)throw new InvalidDataException("安装包大小与发布记录不一致。");await output.WriteAsync(buffer,0,count,deadline.Token);if(progress!=null)progress.Report((int)(99*done/asset.size));}
      }
      if(done!=asset.size)throw new InvalidDataException("安装包下载不完整，请重试。");
     }finally{if(response!=null)response.Dispose();}
    }
    string hash;using(var sha=SHA256.Create())using(var file=File.OpenRead(part))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
    if(!String.Equals(hash,asset.digest.Substring(7),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("安装包校验失败，已取消更新，请重新下载。");
    File.Move(part,target);if(progress!=null)progress.Report(100);return target;
   }catch{try{File.Delete(part);}catch{}throw;}
  }
  public void Dispose(){client.Dispose();}
 }
}
