using System.Drawing;
using Microsoft.Win32;
namespace PersonalIme {
 internal static class Palette {
  public static bool Dark {
   get {try {using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key!=null&&(int)key.GetValue("AppsUseLightTheme",1)==0;}catch{return false;}}
  }
  public static Color Background {get{return Dark?Color.FromArgb(16,18,17):Color.FromArgb(245,246,242);}}
  public static Color Text {get{return Dark?Color.FromArgb(241,243,237):Color.FromArgb(32,37,29);}}
  public static Color Surface {get{return Dark?Color.FromArgb(27,30,27):Color.White;}}
  public static Color Secondary {get{return Dark?Color.FromArgb(156,164,151):Color.FromArgb(102,112,95);}}
  public static Color AccentText {get{return Dark?Color.FromArgb(174,255,50):Color.FromArgb(55,100,5);}}
 }
}
