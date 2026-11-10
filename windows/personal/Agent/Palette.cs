using System.Drawing;
using Microsoft.Win32;
namespace PersonalIme {
 internal static class Palette {
  public static bool Dark {
   get {try {using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key!=null&&(int)key.GetValue("AppsUseLightTheme",1)==0;}catch{return false;}}
  }
  public static Color Background {get{return Dark?Color.FromArgb(23,26,24):Color.FromArgb(248,250,247);}}
  public static Color Text {get{return Dark?Color.WhiteSmoke:Color.FromArgb(30,34,29);}}
  public static Color Surface {get{return Dark?Color.FromArgb(31,35,32):Color.White;}}
  public static Color Secondary {get{return Dark?Color.Silver:Color.FromArgb(90,96,86);}}
  public static Color AccentText {get{return Dark?Color.FromArgb(174,255,46):Color.FromArgb(55,100,5);}}
 }
}
