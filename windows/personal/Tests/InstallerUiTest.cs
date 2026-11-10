using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class InstallerUiTest {
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var form=(Form)Activator.CreateInstance(typeof(Setup),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[0],null)){
  form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save("personal/bin/installer-preview.png");}
  foreach(Control c in form.Controls)if(c.Text.Contains("先把使用闭环做好"))throw new Exception("planning prose leaked");Console.WriteLine("PASS installer welcome UI rendered without planning prose");typeof(Setup).GetMethod("ShowCompleted",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save("personal/bin/installer-complete-preview.png");}Console.WriteLine("PASS completion UI rendered");form.Close();
 }}
}
