using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
[assembly:AssemblyTitle("言渡输入法 Windows 卸载程序")]
[assembly:AssemblyVersion("2.1.2.0")]
[assembly:AssemblyFileVersion("2.1.2.0")]
class Uninstall : Form {
 readonly CheckBox purge=new CheckBox();readonly Label status=new Label();readonly Button remove=new PersonalIme.SoftButton(),cancel=new PersonalIme.SoftButton();bool busy,done;
 internal Uninstall(){Text="卸载言渡输入法";Icon=PersonalIme.Brand.Icon;Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(560,390);BackColor=PersonalIme.Palette.Background;ForeColor=PersonalIme.Palette.Text;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
  Controls.Add(new PictureBox{Image=PersonalIme.Brand.CreateImage(),SizeMode=PictureBoxSizeMode.Zoom,Left=32,Top=28,Width=62,Height=62});Controls.Add(new Label{Text="卸载言渡输入法",Left=112,Top=36,Width=410,Height=36,Font=new Font(Font.FontFamily,20,FontStyle.Bold)});
  Controls.Add(new Label{Text="移除输入法程序、启动入口和专属注册项。\r\n默认保留个人配置和学习词库，方便以后重装。",Left=32,Top=118,Width=496,Height=62,ForeColor=PersonalIme.Palette.Secondary});
  purge.Text="彻底卸载，同时删除 API 配置、学习词库和缓存";purge.SetBounds(32,190,496,35);Controls.Add(purge);
  status.SetBounds(32,235,496,60);status.ForeColor=PersonalIme.Palette.Secondary;status.Text="不会删除你手动保存的安装包、源码备份或 Windows 系统日志。";Controls.Add(status);
  remove.Text="卸载言渡";remove.SetBounds(32,315,340,44);remove.BackColor=Color.FromArgb(174,255,50);Controls.Add(remove);cancel.Text="取消";cancel.SetBounds(388,315,140,44);Controls.Add(cancel);
  remove.Click+=async delegate{if(done){Close();return;}await BeginRemove();};cancel.Click+=delegate{if(!busy)Close();};FormClosing+=delegate(object o,FormClosingEventArgs e){if(busy)e.Cancel=true;};
 }
 async Task BeginRemove(){busy=true;purge.Enabled=remove.Enabled=cancel.Enabled=false;status.Text="正在卸载；如出现系统确认，请允许继续。";string report=Path.Combine(Path.GetTempPath(),"YanduUninstall-"+Guid.NewGuid().ToString("N")+".txt");
  try{File.WriteAllText(report,"");var start=new ProcessStartInfo{FileName=Application.ExecutablePath,Arguments="--worker "+Convert.ToBase64String(Encoding.UTF8.GetBytes(report))+" "+(purge.Checked?"1":"0")+" "+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};using(var process=Process.Start(start)){await Task.Run(()=>process.WaitForExit());string result=File.ReadAllText(report,Encoding.UTF8);if(process.ExitCode!=0)throw new Exception(String.IsNullOrWhiteSpace(result)?"卸载未完成，请关闭正在使用言渡的应用后重试。":result);status.Text=result.Replace("YANDU_UNINSTALL_RESTART","").Trim();done=true;remove.Text="完成";cancel.Visible=false;}}
  catch(System.ComponentModel.Win32Exception ex){status.Text=ex.NativeErrorCode==1223?"已取消系统确认，尚未卸载。":"无法启动卸载程序，请稍后重试。";}catch(Exception ex){status.Text=ex.Message;}finally{busy=false;purge.Enabled=!done;remove.Enabled=cancel.Enabled=true;try{File.Delete(report);}catch{}}
 }
 [STAThread]static int Main(string[] args){if(args.Length==4&&args[0]=="--worker")return Worker(args[1],args[2]=="1",args[3]);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Uninstall());return 0;}
 static int Worker(string encoded,bool purge,string ownerSid){string report=null;try{report=Encoding.UTF8.GetString(Convert.FromBase64String(encoded));string full=Path.GetFullPath(report);if(!String.Equals(Path.GetDirectoryName(full).TrimEnd('\\'),Path.GetTempPath().TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full),@"^YanduUninstall-[a-f0-9]{32}\.txt$"))return 2;
  if(purge&&ownerSid!=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value){File.WriteAllText(report,"彻底卸载需使用同一 Windows 账号进行管理员授权，以免清理其他账号的数据。",Encoding.UTF8);return 2;}
  string folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);var start=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(folder,"Uninstall.ps1")+"\" -Uninstall"+(purge?" -PurgeData":""),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};using(var process=Process.Start(start)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();File.WriteAllText(report,process.ExitCode==0?((purge?"言渡已卸载，API 配置、学习词库和缓存已删除。":"言渡已卸载，个人配置和词库已保留。")+(output.Result.Contains("YANDU_UNINSTALL_RESTART")?"\r\n被占用的程序文件将在重启后删除。":"")):"卸载未完成：部分文件可能被占用，请关闭相关软件后重试。",Encoding.UTF8);return process.ExitCode;}
 }catch{if(report!=null)try{File.WriteAllText(report,"卸载未完成，请关闭相关应用后重试。",Encoding.UTF8);}catch{}return 2;}}
}
