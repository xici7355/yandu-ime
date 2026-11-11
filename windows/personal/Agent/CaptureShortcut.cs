using System;using System.Windows.Forms;
namespace PersonalIme {
 internal sealed class CaptureShortcut {
  public readonly uint Modifiers,Key;public readonly string Text;
  CaptureShortcut(uint modifiers,uint key,string text){Modifiers=modifiers;Key=key;Text=text;}
  public static CaptureShortcut Parse(string value){if(String.IsNullOrWhiteSpace(value))return null;uint modifiers=0;Keys key=Keys.None;foreach(string raw in value.Split('+')){string part=raw.Trim();if(part.Equals("Ctrl",StringComparison.OrdinalIgnoreCase))modifiers|=2;else if(part.Equals("Alt",StringComparison.OrdinalIgnoreCase))modifiers|=1;else if(part.Equals("Shift",StringComparison.OrdinalIgnoreCase))modifiers|=4;else{Keys parsed;if(key!=Keys.None||!Enum.TryParse(part,true,out parsed))throw new ArgumentException("快捷键格式示例：Ctrl+Alt+Z");key=parsed;}}if(modifiers==0||key==Keys.None||!((key>=Keys.A&&key<=Keys.Z)||(key>=Keys.D0&&key<=Keys.D9)||(key>=Keys.F1&&key<=Keys.F24)))throw new ArgumentException("请组合 Ctrl／Alt／Shift 与字母、数字或功能键。");string text=((modifiers&2)!=0?"Ctrl+":"")+((modifiers&1)!=0?"Alt+":"")+((modifiers&4)!=0?"Shift+":"")+key;return new CaptureShortcut(modifiers|0x4000,(uint)key,text);}
  public static string Normalize(string value){var binding=Parse(value);return binding==null?"":binding.Text;}
  public static bool Probe(IntPtr owner,string value){var key=Parse(value);if(key==null)return true;if(!Native.RegisterHotKey(owner,1188,key.Modifiers,key.Key))return false;Native.UnregisterHotKey(owner,1188);return true;}
 }
}
