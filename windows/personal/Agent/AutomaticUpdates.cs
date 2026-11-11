using System;using System.Drawing;using System.IO;using System.Diagnostics;using System.Threading;using System.Threading.Tasks;using System.Windows.Forms;using System.Web.Script.Serialization;
namespace PersonalIme {
 internal sealed class UpdateNoticeState {
  public DateTime LastCheckUtc;public string IgnoredVersion="";
  public static UpdateNoticeState Load(string file){try{return new JavaScriptSerializer().Deserialize<UpdateNoticeState>(File.ReadAllText(file))??new UpdateNoticeState();}catch{return new UpdateNoticeState();}}
  public void Save(string file){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,new JavaScriptSerializer().Serialize(this));}
  public bool Due(DateTime now){return LastCheckUtc>now||now-LastCheckUtc>=TimeSpan.FromHours(24);}
  public bool ShouldNotify(ReleaseInfo release){return release!=null&&release.Version>Updates.CurrentVersion&&release.Installer!=null&&release.tag_name!=IgnoredVersion;}
 }
 internal sealed class UpdateNotice : Form {
  readonly Label status=new Label();readonly Button upgrade=new SoftButton(),ignore=new SoftButton(),cancel=new SoftButton();readonly CancellationTokenSource lifetime=new CancellationTokenSource();bool busy;
  public UpdateNotice(ReleaseInfo release,Func<IProgress<int>,CancellationToken,Task> install,Action dismiss){Text="言渡更新";Icon=Brand.Icon;ShowInTaskbar=false;TopMost=true;FormBorderStyle=FormBorderStyle.FixedToolWindow;ClientSize=new Size(390,215);Font=new Font("Microsoft YaHei UI",10);BackColor=Palette.Surface;ForeColor=Palette.Text;StartPosition=FormStartPosition.Manual;var work=Screen.PrimaryScreen.WorkingArea;Location=new Point(work.Right-Width-18,work.Bottom-Height-18);
   Controls.Add(new Label{Text="发现言渡新版 "+release.tag_name,Left=20,Top=20,Width=350,Height=32,Font=new Font(Font.FontFamily,14,FontStyle.Bold)});status.Text="升级会保留 API 配置和本地词库。\r\n下载安装后仍需 Windows 管理员确认。";status.SetBounds(20,64,350,72);status.ForeColor=Palette.Secondary;Controls.Add(status);
   upgrade.Text="升级";upgrade.SetBounds(20,155,150,38);upgrade.BackColor=Color.FromArgb(174,255,50);ignore.Text="忽略此版本";ignore.SetBounds(180,155,110,38);cancel.Text="取消";cancel.SetBounds(300,155,70,38);Controls.AddRange(new Control[]{upgrade,ignore,cancel});
   ignore.Click+=delegate{dismiss();Close();};cancel.Click+=delegate{Close();};upgrade.Click+=async delegate{if(busy)return;busy=true;upgrade.Enabled=ignore.Enabled=false;try{await install(new Progress<int>(n=>{if(!IsDisposed&&!Disposing)status.Text=n+"% · 正在下载并校验更新";}),lifetime.Token);if(!IsDisposed&&!Disposing)Close();}catch(OperationCanceledException){if(!IsDisposed&&!Disposing)status.Text="下载已取消。";}catch{if(!IsDisposed&&!Disposing)status.Text="更新未完成，请检查网络或稍后从设置重试。";}finally{busy=false;if(!IsDisposed&&!Disposing)upgrade.Enabled=ignore.Enabled=true;}};
   FormClosed+=delegate{lifetime.Cancel();};
  }
  protected override bool ShowWithoutActivation {get{return true;}}
 }
 internal sealed class AutomaticUpdates : IDisposable {
  readonly Updates client=new Updates();readonly CancellationTokenSource lifetime=new CancellationTokenSource();readonly string stateFile=Path.Combine(Settings.Folder,"update-state.json");UpdateNotice notice;bool disposed;
  bool started;
  public async void Start(){if(started)return;started=true;try{await Task.Delay(5000,lifetime.Token);while(!disposed){try{await CheckOnce();}catch(OperationCanceledException){if(lifetime.IsCancellationRequested)return;}catch{/* No interruption when offline. */}await Task.Delay(TimeSpan.FromHours(1),lifetime.Token);}}catch(OperationCanceledException){}}
  async Task CheckOnce(){if(disposed||notice!=null&&!notice.IsDisposed)return;var state=UpdateNoticeState.Load(stateFile);if(!state.Due(DateTime.UtcNow))return;var release=await client.Check(lifetime.Token);state.LastCheckUtc=DateTime.UtcNow;state.Save(stateFile);if(disposed||!state.ShouldNotify(release))return;
   notice=new UpdateNotice(release,async(progress,token)=>{string folder=Path.Combine(Settings.Folder,"Updates",Guid.NewGuid().ToString("N"));string installer=await client.Download(release,folder,progress,token);token.ThrowIfCancellationRequested();Process.Start(new ProcessStartInfo{FileName=installer,Arguments="--update",UseShellExecute=true});},()=>{state.IgnoredVersion=release.tag_name;state.Save(stateFile);});notice.Show();
  }
  public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();if(notice!=null&&!notice.IsDisposed)notice.Close();client.Dispose();}
 }
}
