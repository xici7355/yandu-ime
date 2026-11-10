using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
class Setup : Form {
 readonly Label heading=new Label(),description=new Label(),locationLabel=new Label(),status=new Label();
 readonly TextBox location=new TextBox();
 readonly Button browse=new PersonalIme.SoftButton(),action=new PersonalIme.SoftButton(),secondary=new PersonalIme.SoftButton();
 readonly ProgressBar progress=new ProgressBar();
 readonly Timer poll=new Timer{Interval=200};
 bool busy,finished;
 string destination,progressFile,lastError;
 static string installed=FindInstalled();
 internal static string FindInstalled(){
  foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view)){
   using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalTranslateIme")){if(key!=null){var path=key.GetValue("InstallLocation") as string;if(!String.IsNullOrWhiteSpace(path))return Path.GetFullPath(path).TrimEnd('\\');}}
   using(var key=root.OpenSubKey(@"Software\PersonalTranslateIme\Weasel")){if(key!=null){var path=key.GetValue("WeaselRoot") as string;if(!String.IsNullOrWhiteSpace(path))return Path.GetFullPath(path).TrimEnd('\\');}}
  }
  return null;
 }
 [STAThread]static int Main(string[] args){
  if(args.Length==3 && args[0]=="--worker")return Install(Decode(args[1]),Decode(args[2]));
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Setup());return 0;
 }
 Setup(){
  destination=installed??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PersonalTranslateIme");
  Text="安装言渡输入法";Icon=PersonalIme.Brand.Icon;ClientSize=new Size(600,510);AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",10);BackColor=PersonalIme.Palette.Background;ForeColor=PersonalIme.Palette.Text;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
  Controls.Add(new PictureBox{Image=PersonalIme.Brand.CreateImage(),SizeMode=PictureBoxSizeMode.Zoom,Left=36,Top=34,Width=66,Height=66});
  Controls.Add(new Label{Text="言渡输入法",Left=120,Top=36,Width=400,Height=36,Font=new Font("Microsoft YaHei UI",21,FontStyle.Bold)});
  Controls.Add(new Label{Text="Windows · V1.0",Left=122,Top=78,Width=350,Height=24,ForeColor=PersonalIme.Palette.Secondary});
  heading.SetBounds(36,130,530,42);heading.Font=new Font("Microsoft YaHei UI",18,FontStyle.Bold);heading.Text=installed==null?"落笔此岸，言达彼岸。":"升级言渡，保留原有配置";Controls.Add(heading);
  description.SetBounds(36,185,530,55);description.ForeColor=PersonalIme.Palette.Secondary;description.Text="中文全拼  /  实时翻译  /  译文朗读\r\n"+(installed==null?"配置 AI 接口后即可翻译；未配置时正常输入中文。":"已找到现有安装，将沿用原路径并保留 API 配置和词库。");Controls.Add(description);
  locationLabel.SetBounds(36,246,528,25);locationLabel.Text=installed==null?"安装位置":"安装位置（升级沿用原路径）";Controls.Add(locationLabel);
  location.SetBounds(36,277,420,30);location.Text=destination;location.BackColor=PersonalIme.Palette.Surface;location.ForeColor=PersonalIme.Palette.Text;location.ReadOnly=installed!=null;Controls.Add(location);
  browse.SetBounds(466,275,98,34);browse.Text="更改…";browse.BackColor=PersonalIme.Palette.Surface;browse.Enabled=installed==null;Controls.Add(browse);
  browse.Click+=delegate{using(var dialog=new FolderBrowserDialog{Description="选择安装文件夹的上级目录，程序将放入其中的 PersonalTranslateIme 文件夹",SelectedPath=Path.GetDirectoryName(location.Text),ShowNewFolderButton=true}){if(dialog.ShowDialog(this)==DialogResult.OK)location.Text=Path.Combine(dialog.SelectedPath,"PersonalTranslateIme");}};
  status.SetBounds(36,326,528,25);status.ForeColor=PersonalIme.Palette.Secondary;Controls.Add(status);
  progress.SetBounds(36,357,528,12);progress.Style=ProgressBarStyle.Continuous;progress.Visible=false;Controls.Add(progress);
  action.SetBounds(36,390,528,48);action.Text=installed==null?"安装言渡":"升级言渡";action.BackColor=Color.FromArgb(174,255,50);action.Font=new Font(Font,FontStyle.Bold);action.ForeColor=Color.FromArgb(28,41,14);Controls.Add(action);
  secondary.SetBounds(36,451,528,30);secondary.Text="取消";secondary.BackColor=BackColor;Controls.Add(secondary);
  action.Click+=async delegate{if(finished){Launch("PersonalTranslateIme.exe","--settings");return;}await BeginInstall();};
  secondary.Click+=delegate{if(finished){Launch("WeaselServer.exe","");Close();}else if(!busy)Close();};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
  poll.Tick+=delegate{ReadProgress();};FormClosed+=delegate{poll.Dispose();};
 }
 internal static string NormalizePath(string value){
  if(String.IsNullOrWhiteSpace(value)||!System.Text.RegularExpressions.Regex.IsMatch(value,@"^[A-Za-z]:\\")||value.IndexOfAny(new[]{'"','<','>','|','*','?'})>=0||value.Substring(2).Contains(":"))throw new ArgumentException("请选择本机磁盘中的有效完整路径。");
  string full=Path.GetFullPath(value).TrimEnd('\\');if(full==Path.GetPathRoot(full).TrimEnd('\\'))throw new ArgumentException("请选择专用文件夹，不能直接安装到磁盘根目录。");return full;
 }
 async Task BeginInstall(){
  try{destination=NormalizePath(location.Text);if(installed!=null&&!String.Equals(destination,installed,StringComparison.OrdinalIgnoreCase))throw new Exception("升级必须沿用原安装位置。");}
  catch(Exception ex){status.Text=ex.Message;return;}
  busy=true;action.Enabled=secondary.Enabled=browse.Enabled=location.Enabled=false;progress.Value=0;progress.Visible=true;heading.Text="正在安装言渡";status.Text="0% · 等待安装权限";description.Text="安装位置："+destination+"\r\n若出现管理员确认，请点击“是”。";
  progressFile=Path.Combine(Path.GetTempPath(),"YanduProgress-"+Guid.NewGuid().ToString("N")+".txt");lastError=null;
  try{
   File.WriteAllText(progressFile,"");poll.Start();
   var start=new ProcessStartInfo{FileName=Application.ExecutablePath,Arguments="--worker "+Encode(destination)+" "+Encode(progressFile),UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
   using(var process=Process.Start(start)){await Task.Run(()=>process.WaitForExit());ReadProgress();if(process.ExitCode!=0)throw new Exception(String.IsNullOrWhiteSpace(lastError)?"安装未完成（"+process.ExitCode+"）。请检查路径及磁盘空间后重试。详细原因见本机 install-error.log。":lastError);}
   progress.Value=100;status.Text="100% · 安装完成";ShowCompleted();
  }catch(System.ComponentModel.Win32Exception ex){heading.Text="安装已取消";status.Text="安装未完成";description.Text=ex.NativeErrorCode==1223?"未获得安装权限，可以再次点击安装。":"无法启动安装，请稍后重试。";}
  catch(Exception ex){heading.Text="安装尚未完成";status.Text=progress.Value+"% · 安装失败，未完成";description.Text=ex.Message;}
  finally{poll.Stop();busy=false;action.Enabled=secondary.Enabled=true;location.Enabled=true;browse.Enabled=!finished&&installed==null;try{File.Delete(progressFile);}catch{}}
 }
 void ReadProgress(){try{string[] lines;using(var file=new FileStream(progressFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(file,Encoding.UTF8))lines=reader.ReadToEnd().Split(new[]{'\n','\r'},StringSplitOptions.RemoveEmptyEntries);foreach(string line in lines){if(line.StartsWith("YANDU_ERROR|",StringComparison.Ordinal)){try{lastError=Decode(line.Substring(12));}catch(FormatException){}continue;}var parts=line.Split(new[]{'|'},3);int value;if(parts.Length==3&&parts[0]=="YANDU_PROGRESS"&&Int32.TryParse(parts[1],out value)&&value>=progress.Value&&value<=99){progress.Value=value;status.Text=value+"% · "+parts[2];}}}catch(IOException){}catch(UnauthorizedAccessException){}}
 void ShowCompleted(){finished=true;heading.Text="安装完成，欢迎使用言渡";description.SetBounds(36,185,530,135);description.Text="安装位置："+destination+"\r\nAPI 配置和本地词库保留。\r\n\r\n点击“配置翻译”打开设置；点击“开始使用”启动输入法。\r\n升级后请重开微信、GPT 等应用，加载新版模块。";location.Visible=browse.Visible=locationLabel.Visible=false;action.Text="配置翻译";secondary.Text="开始使用";}
 void Launch(string name,string args){try{Process.Start(new ProcessStartInfo{FileName=Path.Combine(destination,name),Arguments=args,WorkingDirectory=destination,UseShellExecute=true});}catch{MessageBox.Show("请从开始菜单打开言渡输入法。","言渡");}}
 static string Encode(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
 static string Decode(string value){return Encoding.UTF8.GetString(Convert.FromBase64String(value));}
 static void SaveFailure(string message){try{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PersonalTranslateIme");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"install-error.log"),DateTime.Now.ToString("s")+"\r\n"+message);}catch{}}
 static void ReportLine(string file,string line){using(var stream=new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.ReadWrite))using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))writer.WriteLine(line);}
 static void Report(string file,int value,string message){ReportLine(file,"YANDU_PROGRESS|"+value+"|"+message);}
 static int Install(string destination,string report){
  string temp=Path.Combine(Path.GetTempPath(),"YanduSetup-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(temp);Report(report,1,"正在读取安装包");string archive=Path.Combine(temp,"package.zip");
   using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("package.zip"))using(var file=File.Create(archive))input.CopyTo(file);
   using(var zip=ZipFile.OpenRead(archive)){
    long total=0,done=0;foreach(var entry in zip.Entries)total+=entry.Length;
    string folder=Path.GetFullPath(Path.Combine(temp,"package"))+Path.DirectorySeparatorChar;Directory.CreateDirectory(folder);
    foreach(var entry in zip.Entries){string target=Path.GetFullPath(Path.Combine(folder,entry.FullName));if(!target.StartsWith(folder,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("安装包路径无效");if(String.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(target);continue;}
     Directory.CreateDirectory(Path.GetDirectoryName(target));using(var source=entry.Open())using(var output=File.Create(target)){byte[] buffer=new byte[1024*1024];int count;while((count=source.Read(buffer,0,buffer.Length))>0){output.Write(buffer,0,count);done+=count;Report(report,1+(int)(19*done/Math.Max(1,total)),"正在解压程序和词库");}}
    }
   }
   // Pass paths as encoded values: shell metacharacters in a directory are never executed.
   string script=Path.Combine(temp,@"package\Install.ps1");
   string command="$ErrorActionPreference='Stop'; try { & ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Encode(script)+"'))) -InstallDirectory ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Encode(destination)+"'))) } catch { Write-Output ('YANDU_ERROR|'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_.Exception.Message))); exit 1 }";
   var start=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(command)),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   using(var p=Process.Start(start)){var stderr=p.StandardError.ReadToEndAsync();string workerError=null,line;while((line=p.StandardOutput.ReadLine())!=null){if((line.StartsWith("YANDU_PROGRESS|",StringComparison.Ordinal)||line.StartsWith("YANDU_ERROR|",StringComparison.Ordinal))){ReportLine(report,line);if(line.StartsWith("YANDU_ERROR|",StringComparison.Ordinal))workerError=Decode(line.Substring(12));}}p.WaitForExit();if(p.ExitCode!=0)SaveFailure((workerError??"")+"\r\n"+stderr.Result);return p.ExitCode;}
  }catch(Exception error){SaveFailure(error.ToString());try{ReportLine(report,"YANDU_ERROR|"+Encode(error.Message));}catch{}return 2;}finally{try{if(Directory.Exists(temp))Directory.Delete(temp,true);}catch{}}
 }
}
