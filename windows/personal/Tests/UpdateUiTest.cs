using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Security.Cryptography;
using PersonalIme;
class UpdateUiTest {
 static int count;
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);count++;Console.WriteLine("PASS "+label);}
 static Control Find(Control root,string text){foreach(Control c in root.Controls){if(c.Text==text)return c;var found=Find(c,text);if(found!=null)return found;}return null;}
 static string HashConfig(){if(!File.Exists(Settings.FilePath))return "";using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(Settings.FilePath)));}
 [STAThread]static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string before=HashConfig();var config=new Settings();using(var engine=new Translator())using(var form=new SettingsForm(config,engine,delegate{})){
  form.Show();Application.DoEvents();((Button)Find(form,"隐私")).PerformClick();Application.DoEvents();
  Check(Find(form,"当前版本：0.3.3")!=null,"privacy shows installed application version");
  Check(Find(form,"GitHub 更新仓库（地址或 用户名/仓库名）")!=null,"privacy exposes configurable GitHub repository");
  var check=(Button)Find(form,"检查更新");Check(check!=null,"manual check button visible");
  Check(!Find(form,"下载并更新").Enabled,"download disabled until newer release found");
  check.PerformClick();Application.DoEvents();Check(Find(form,"请先填写 GitHub 更新仓库。")!=null,"empty repository clearly explained without network request");
  Check(config.UpdateRepository=="","repository remains empty until user supplies one");
  using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save("personal/bin/privacy-update-preview.png");}
  form.Close();}
  Check(before==HashConfig(),"opening version UI does not alter actual API configuration");Console.WriteLine("OK "+count+" privacy update UI checks; no remote request");return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
}
