using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PersonalIme {
 public sealed class LocalDictionary {
  sealed class Data {public readonly Dictionary<string,string> English=new Dictionary<string,string>(StringComparer.Ordinal),Chinese=new Dictionary<string,string>(StringComparer.Ordinal);}
  readonly string folder;readonly Lazy<Data> data;
  public static readonly LocalDictionary Shared=new LocalDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionary"));
  public LocalDictionary(string path){folder=path;data=new Lazy<Data>(Load,LazyThreadSafetyMode.ExecutionAndPublication);}
  public static string Description(){try{var meta=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionary","manifest.json"),Encoding.UTF8));return "CC-CEDICT · "+Convert.ToString(meta["sourceDate"]).Substring(0,10)+" · "+Convert.ToInt32(meta["sourceEntries"]).ToString("N0")+" 条原始词条";}catch{return "CC-CEDICT · 词库未安装";}}
  public static string Normalize(string value){return Regex.Replace((value??"").Trim(),@"\s+"," ").ToLowerInvariant().Replace('’','\'');}
  Data Load(){var loaded=new Data();try{var manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"manifest.json"),Encoding.UTF8));string file=Path.Combine(folder,"cc-cedict.tsv.gz");using(var hash=SHA256.Create())using(var stream=File.OpenRead(file)){string actual=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(actual!=Convert.ToString(manifest["dataSha256"]))return loaded;}
    using(var stream=File.OpenRead(file))using(var gzip=new GZipStream(stream,CompressionMode.Decompress))using(var reader=new StreamReader(gzip,Encoding.UTF8)){string line;while((line=reader.ReadLine())!=null){var parts=line.Split(new[]{'\t'},3);if(parts.Length!=3)continue;(parts[0]=="zh"?loaded.Chinese:loaded.English)[parts[1]]=parts[2];}}
   }catch{loaded=new Data();}return loaded;
  }
  public bool TryLookup(string source,bool chineseTarget,bool multiple,out string result){result=null;string key=Normalize(source);if(key.Length==0||key.Length>120)return false;string raw;var entries=chineseTarget?data.Value.Chinese:data.Value.English;if(!entries.TryGetValue(key,out raw))return false;result=multiple?raw.Replace(" | ",chineseTarget?"；":"; "):raw.Split(new[]{" | "},StringSplitOptions.None)[0];return !String.IsNullOrWhiteSpace(result);}
  public string ReferenceTerms(string source,bool chineseTarget){if(String.IsNullOrEmpty(source)||source.Length>2000)return "";var terms=new List<string>();var seen=new HashSet<string>();
   if(!chineseTarget){foreach(Match chunk in Regex.Matches(source,@"[\u3400-\u9fff]{2,}")){string text=chunk.Value;for(int i=0;i<text.Length&&terms.Count<6;i++){for(int n=Math.Min(8,text.Length-i);n>=2;n--){string key=text.Substring(i,n),value;if(seen.Contains(key)||!TryLookup(key,false,false,out value))continue;terms.Add(key+" = "+value);seen.Add(key);i+=n-1;break;}}if(terms.Count>=6)break;}}
   else{var tokens=Regex.Matches(source,@"[A-Za-z][A-Za-z'-]*");for(int i=0;i<tokens.Count&&terms.Count<6;i++){for(int n=Math.Min(4,tokens.Count-i);n>=1;n--){string key="";for(int j=0;j<n;j++)key+=(j==0?"":" ")+tokens[i+j].Value;string value;if((n==1&&key.Length<4)||seen.Contains(Normalize(key))||!TryLookup(key,true,true,out value))continue;terms.Add(key+" = "+value);seen.Add(Normalize(key));i+=n-1;break;}}}
   return String.Join("; ",terms.ToArray());
  }
 }
 public sealed class TranslationResult {public readonly string Text,Origin;public TranslationResult(string text,string origin){Text=text;Origin=origin;}}
}
