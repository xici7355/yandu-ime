using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
[assembly:AssemblyTitle("个人翻译输入法 Windows 卸载程序")]
[assembly:AssemblyVersion("0.1.0.0")]
[assembly:AssemblyFileVersion("0.1.0.0")]
class Uninstall {
 [STAThread]static void Main(){Application.EnableVisualStyles();try {
  string folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
  var start=new ProcessStartInfo {FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(folder,"Uninstall.ps1")+"\" -Uninstall",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  using(var process=Process.Start(start)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();if(process.ExitCode!=0)throw new Exception(error.Result);MessageBox.Show(output.Result,"个人翻译输入法 · 卸载",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 }catch(Exception ex){MessageBox.Show(ex.Message,"卸载未完成",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
}
