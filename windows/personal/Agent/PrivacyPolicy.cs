using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Automation;
namespace PersonalIme {
 public sealed class SensitiveContentException:InvalidOperationException {public SensitiveContentException():base("检测到疑似密码、密钥或个人敏感信息。本次不发送、不加入翻译缓存。保护不可关闭。") {}}
 // This gate is unconditional and has no preference or command-line bypass.
 public static class PrivacyPolicy {
  static readonly object FocusGate=new object();static Task<bool> focusCheck;
  public static bool ProtectedElement(AutomationElement element){if(element==null)return false;var current=element.Current;if(current.IsPassword)return true;if(current.ControlType!=ControlType.Edit)return false;return Regex.IsMatch((current.Name??"")+" "+(current.AutomationId??""),@"password|passwd|api[_ -]?key|secret|token|credit.?card|身份证|银行卡|密码|口令|密钥|验证码",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
  public static async Task<bool> ProtectedFocus(){Task<bool> check;lock(FocusGate){if(focusCheck!=null&&!focusCheck.IsCompleted)return true;check=focusCheck=Task.Run(()=>{try{return ProtectedElement(AutomationElement.FocusedElement);}catch{return true;}});}if(await Task.WhenAny(check,Task.Delay(700))!=check)return true;return check.Result;}
  static readonly Regex Credentials=new Regex(@"\b(?:sk-[A-Za-z0-9_-]*|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{24,}|AKIA[0-9A-Z]{16}|eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,})\b|-----BEGIN(?: RSA| EC| OPENSSH)? PRIVATE KEY-----|(?:password|passwd|pwd|api[_ -]?key|secret|token|authorization|密码|口令|密钥|验证码)\s*(?:[:=：]|是|为)|https?://[^\s/]+:[^\s/@]+@",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
  static readonly Regex Email=new Regex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
  static readonly Regex Numbers=new Regex(@"(?<!\d)1[3-9]\d{9}(?!\d)|(?<!\d)\d{17}[\dX](?!\d)|(?<!\d)(?:\d[ -]?){13,}(?!\d)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
  static bool Matches(string value){return Credentials.IsMatch(value)||Numbers.IsMatch(value)||(value.IndexOf('@')>=0&&Email.IsMatch(value));}
  public static bool IsSensitive(string source){if(String.IsNullOrEmpty(source))return false;try{if(Matches(source))return true;int digits=0;foreach(char c in source)if(c>='0'&&c<='9')digits++;if(digits<10&&source.IndexOf('@')<0&&source.IndexOf("sk-",StringComparison.OrdinalIgnoreCase)<0&&source.IndexOf("eyJ",StringComparison.Ordinal)<0)return false;string compact=Regex.Replace(source,@"\s+","");return compact!=source&&Matches(compact);}catch(RegexMatchTimeoutException){return true;}}
  public static void RequireSafe(string source){if(IsSensitive(source))throw new SensitiveContentException();}
 }
}
