using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace PersonalIme {
 public sealed class Settings {
  public string Endpoint = "https://api.deepseek.com/v1";
  public string Model = "deepseek-chat";
  public string ProtectedKey = "";
  public string Voice = "";
  public string ScreenshotHotkey = "Ctrl+Alt+Z";
  public int SpeechRate = -1;
  public int SpeechVolume = 90;
  public bool PauseTranslation = false;
  public bool SelectionTranslation = true;
  public bool CaptureComparison = false;
  public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalTranslateIme");
  public static readonly string FilePath = Path.Combine(Folder, "settings.json");
  public static Settings Load() {
   try { return new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new Settings(); }
   catch { return new Settings(); }
  }
  public string Key() {
   if (String.IsNullOrEmpty(ProtectedKey)) return "";
   try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedKey), null, DataProtectionScope.CurrentUser)); }
   catch { return ""; }
  }
  public void SetKey(string key) {
   ProtectedKey = String.IsNullOrWhiteSpace(key) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
  }
  public bool Ready { get { return !PauseTranslation && !String.IsNullOrWhiteSpace(Model) && !String.IsNullOrWhiteSpace(Key()); } }
  public void Save() {
   Directory.CreateDirectory(Folder);
   string temp = FilePath + ".tmp";
   File.WriteAllText(temp, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
   if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
  }
  public static Uri ChatUri(string input) {
   Uri uri;
   if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
    throw new ArgumentException("接口地址需以 https:// 开头；sk- 开头的是 API 密钥，请填写在密钥栏。");
   if (uri.Scheme == "http" && !uri.IsLoopback) throw new ArgumentException("远程接口请使用 HTTPS；HTTP 仅允许本机接口。");
   string value = uri.AbsoluteUri.TrimEnd('/');
   if (!value.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) value += "/chat/completions";
   return new Uri(value);
  }
 }
}
