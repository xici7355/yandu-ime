using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text;
class InstallerFlowUiTest {
 static int count;
 static void Check(bool value,string message){if(!value)throw new Exception("FAIL "+message);count++;Console.WriteLine("PASS "+message);}
 static FieldInfo Field(string name){return typeof(Setup).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);}
 static object Get(object form,string name){return Field(name).GetValue(form);}
 static void Call(object form,string name){typeof(Setup).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);Application.DoEvents();}
 static Form Create(){return (Form)Activator.CreateInstance(typeof(Setup),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[0],null);}
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string existing=@"C:\Program Files\PersonalTranslateIme";var installed=typeof(Setup).GetField("installed",BindingFlags.Static|BindingFlags.NonPublic);installed.SetValue(null,existing);Check(!Setup.ValidInstallation(@"D:\MissingYanduFixture"),"stale registry location is not an installed product");
  using(var form=Create()){form.Show();Application.DoEvents();Check(((TextBox)Get(form,"location")).Text==existing,"upgrade defaults to original path");Check(!((TextBox)Get(form,"location")).ReadOnly,"existing installation path remains editable");Check(((Button)Get(form,"browse")).Enabled,"existing installation can select a new empty path");Check(((Button)Get(form,"action")).Text=="安装言渡","installer uses installation action instead of upgrade-only option");form.Close();}
  installed.SetValue(null,null);
  using(var form=Create()){form.Show();Application.DoEvents();Check(!((TextBox)Get(form,"location")).ReadOnly,"new install path editable");Check(((Button)Get(form,"browse")).Enabled,"new install folder picker enabled");Check(((ProgressBar)Get(form,"progress")).Style==ProgressBarStyle.Continuous,"determinate progress instead of marquee");
   string report=Path.Combine(Path.GetTempPath(),"YanduUiFixture-"+Guid.NewGuid().ToString("N")+".txt");Field("progressFile").SetValue(form,report);
   try{File.WriteAllText(report,"YANDU_PROGRESS|20|copying\nYANDU_PROGRESS|75|copied\n");Call(form,"ReadProgress");Check(((ProgressBar)Get(form,"progress")).Value==75,"actual worker progress reflected");
    File.AppendAllText(report,"YANDU_PROGRESS|25|late\nYANDU_PROGRESS|100|premature\n");Call(form,"ReadProgress");Check(((ProgressBar)Get(form,"progress")).Value==75,"late and premature completion reports ignored");
    File.AppendAllText(report,"YANDU_PROGRESS|99|registered\n");Call(form,"ReadProgress");Check(((ProgressBar)Get(form,"progress")).Value==99,"worker completion held at 99 until exit success");
    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save("personal/bin/installer-custom-preview.png");}
    var reportLine=typeof(Setup).GetMethod("ReportLine",BindingFlags.Static|BindingFlags.NonPublic);
    var writer=Task.Run(delegate{for(int i=0;i<500;i++)reportLine.Invoke(null,new object[]{report,"YANDU_PROGRESS|99|concurrent fixture"});});
    while(!writer.IsCompleted)Call(form,"ReadProgress");writer.GetAwaiter().GetResult();Call(form,"ReadProgress");Check(((ProgressBar)Get(form,"progress")).Value==99,"500 concurrent progress writes and UI reads avoid file-sharing conflict");
    File.AppendAllText(report,"YANDU_ERROR|"+Convert.ToBase64String(Encoding.UTF8.GetBytes("请选择空文件夹"))+"\n");Call(form,"ReadProgress");Check((string)Get(form,"lastError")=="请选择空文件夹","worker failure detail reaches UI without false completion");
    Call(form,"ShowCompleted");Check(((Button)Get(form,"action")).Text=="配置翻译","success offers actual settings entry");Check(!((TextBox)Get(form,"location")).Visible,"success hides path editor");
   }finally{File.Delete(report);}form.Close();}
  installed.SetValue(null,existing);
  Check(Setup.NormalizePath(@"D:\Apps\言渡 & $ [x]")==@"D:\Apps\言渡 & $ [x]","UI accepts Unicode and metacharacters as literal path");
  foreach(string value in new[]{@"D:\",@"..\path",@"\\server\share",@"D:\bad?path",@"D:\bad:stream"}){bool rejected=false;try{Setup.NormalizePath(value);}catch(ArgumentException){rejected=true;}Check(rejected,"UI rejects invalid path fixture");}
  Console.WriteLine("OK "+count+" installer UI checks");
 }
}
