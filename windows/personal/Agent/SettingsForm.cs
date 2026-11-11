using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Windows.Forms;
namespace PersonalIme {
 internal sealed class SettingsForm : Form {
  readonly Settings config;
  readonly TextBox endpoint=new TextBox(),model=new TextBox(),key=new TextBox(),captureKey=new TextBox();
  public Func<string,bool> CaptureShortcutAvailable;
  readonly ComboBox voice=new ComboBox();
  readonly TrackBar rate=new TrackBar(),volume=new TrackBar();
  readonly CheckBox pause=new CheckBox();
  readonly CheckBox selectionTranslation=new CheckBox();
  public event Action ScreenTranslationRequested;
  readonly Label status=new Label();
  readonly Translator translator;
  readonly Action saved;
  readonly CancellationTokenSource lifetime = new CancellationTokenSource();
  readonly SpeechSynthesizer synth=new SpeechSynthesizer();
  readonly Updates updates=new Updates();
  readonly Label updateStatus=new Label();
  ReleaseInfo availableRelease;
  public SettingsForm(Settings settings,Translator engine,Action onSaved) {
   config=settings;translator=engine;saved=onSaved;
   Text="言渡输入法 · 设置";Icon=Brand.Icon; Font=new Font("Microsoft YaHei UI",10); Size=new Size(810,790); MinimumSize=new Size(810,790); StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
   BackColor=Palette.Background; ForeColor=Palette.Text;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(24)};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,108));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));Controls.Add(layout);
   var introduction=new Panel{Dock=DockStyle.Fill};
   introduction.Controls.Add(new PictureBox{Image=Brand.CreateImage(),SizeMode=PictureBoxSizeMode.Zoom,Left=0,Top=0,Width=64,Height=64});
   introduction.Controls.Add(new Label{Text="言渡输入法",Font=new Font("Microsoft YaHei UI",18),ForeColor=Palette.Text,AutoSize=true,Left=80,Top=4});
   introduction.Controls.Add(new Label{Text="落笔此岸，言达彼岸。",Font=new Font("Microsoft YaHei UI",10),ForeColor=Palette.Secondary,AutoSize=true,Left=81,Top=43});
   layout.Controls.Add(introduction,0,0);layout.SetColumnSpan(introduction,1);
   var nav=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(0,4,0,4)};layout.Controls.Add(nav,0,1);
   var tabs=new Panel {Dock=DockStyle.Fill,BackColor=Palette.Background};layout.Controls.Add(tabs,0,2);
   var keyboard=Page(tabs,"键盘");
   AddText(keyboard,"中文输入",22,20,550,28,true);
   AddText(keyboard,"全拼中文＋英文。通过 Win＋空格切换输入法，Shift 切换中英文。\r\n支持整句组合，本地学习已选词句并调整候选排序。",22,60,540,65,false);
   AddText(keyboard,"常用操作",22,150,550,28,true);
   AddText(keyboard,"右键任务栏的言渡图标，选择“言渡设置”。\r\n点击译文替换，再点同一位置撤回。\r\n也可从开始菜单或托盘打开言渡设置\r\n也可以直接点击译文或小喇叭。",22,190,520,150,false);
   var input=Page(tabs,"输入");
   AddText(input,"输入与替换",22,20,550,28,true);
   AddText(input,"在当前行末确认完整原文。\r\n替换前核对原文、输入框和光标位置。\r\n移动到其他位置或切换输入框后，旧译文失效。\r\n中文原文标点保持不变；译文末尾逗号、句号本地转换。\r\n中文候选词不显示英文释义。\r\n用户词库学习在本机完成，重启后保留。",22,65,550,170,false);
   var translate=Page(tabs,"翻译");
   LabelField(translate,"API 接口地址",endpoint,20); endpoint.Text=config.Endpoint;
   LabelField(translate,"模型名称",model,95); model.Text=config.Model;
   LabelField(translate,"API 密钥（sk- 开头填这里）",key,170); key.UseSystemPasswordChar=true; key.Text=config.Key();
   AddText(translate,"译文朗读音色",22,245,550,25,false);
   voice.SetBounds(22,274,550,30); voice.DropDownStyle=ComboBoxStyle.DropDownList;
   voice.Items.Add("系统默认"); foreach(var item in synth.GetInstalledVoices()) if(item.Enabled) voice.Items.Add(item.VoiceInfo.Name);
   voice.SelectedIndex=0; if(voice.Items.Contains(config.Voice)) voice.SelectedItem=config.Voice; translate.Controls.Add(voice);
   AddText(translate,"语速",22,315,60,25,false);rate.SetBounds(85,310,205,45);rate.Minimum=-5;rate.Maximum=5;rate.Value=Math.Max(-5,Math.Min(5,config.SpeechRate));translate.Controls.Add(rate);
   AddText(translate,"音量",315,315,60,25,false);volume.SetBounds(370,310,205,45);volume.Minimum=0;volume.Maximum=100;volume.Value=Math.Max(0,Math.Min(100,config.SpeechVolume));translate.Controls.Add(volume);
   var test=Button(translate,"验证连接",22,370,120);test.Click+=async delegate {
    try {var temporary=Read();test.Enabled=false;status.Text="正在验证连接……";
     string result=await translator.Translate(temporary,"你好，很高兴认识你。",lifetime.Token);status.Text=result;
    } catch(OperationCanceledException) { if(!IsDisposed) status.Text="请求已取消或超时。"; }
    catch(Exception ex) {if(!IsDisposed) status.Text=ex is System.Net.Http.HttpRequestException ? "连接失败，请检查网络和接口地址。" : ex.Message;}
    finally {if(!IsDisposed) test.Enabled=true;}
   };
   var listen=Button(translate,"试听",155,370,100);listen.Click+=delegate {try {synth.SpeakAsyncCancelAll();synth.Rate=rate.Value;synth.Volume=volume.Value;if(voice.SelectedIndex>0)synth.SelectVoice((string)voice.SelectedItem);synth.SpeakAsync("Hello. It's nice to meet you.");}catch{status.Text="该音色暂不可用，请选择其他音色。";}};
   status.SetBounds(22,415,550,44);translate.Controls.Add(status);
   AddText(translate,"阅读翻译",22,480,550,28,true);
   selectionTranslation.Text="划词后显示“译”按钮，点击才发送翻译";selectionTranslation.Checked=config.SelectionTranslation;selectionTranslation.SetBounds(22,520,650,32);translate.Controls.Add(selectionTranslation);
   AddText(translate,"截图与划词自动中英互译；密码框不读取。\r\n截图后先复制图片；点击工具栏翻译才在本机识别。\r\n只将待译文字发送到配置的接口；截图可复制，不提供朗读。",22,562,680,86,false);
   Button(translate,"截图／截图翻译",22,660,150).Click+=delegate{if(ScreenTranslationRequested!=null){Close();ScreenTranslationRequested();}};
   AddText(translate,"截图／截图翻译共用快捷键（可修改，留空关闭快捷键）",22,712,680,28,false);
   captureKey.SetBounds(22,748,330,30);captureKey.Text=config.ScreenshotHotkey;translate.Controls.Add(captureKey);
   Button(translate,"检查冲突",365,744,130).Click+=delegate{try{string value=CaptureShortcut.Normalize(captureKey.Text);bool available=CaptureShortcutAvailable!=null?CaptureShortcutAvailable(value):CaptureShortcut.Probe(Handle,value);MessageBox.Show(this,available?"快捷键可用，保存设置后生效。":"该快捷键已被占用，请更换后再保存。","截图快捷键");}catch(Exception ex){MessageBox.Show(this,ex.Message,"截图快捷键");}};
   AddText(translate,"框选后先自动复制图片；需要翻译时点击截图工具栏“翻译”。",22,795,680,44,false);
   AddText(translate,"本地词典优先",22,855,550,28,true);
   AddText(translate,LocalDictionary.Description()+"\r\n完整命中单词或短语时本地返回，未命中再请求 AI。\r\n多义词可点“AI精译”；词库随版本更新，不采集个人词汇。",22,895,680,100,false);
   var privacy=Page(tabs,"隐私");
   pause.Text="暂停 AI 联网翻译，保留本地词典查询";pause.Checked=config.PauseTranslation;pause.SetBounds(22,25,550,35);privacy.Controls.Add(pause);
   AddText(privacy,"强制密码保护不可关闭；敏感凭据不发送、不缓存。\r\n不上传用户词库或缓存，不收集输入内容用于改进或训练。\r\n缓存仅在内存，退出即清空；截图识别不生成临时文件。\r\nAPI 密钥加密保存在本机。AI 翻译仅发送本次普通待译文字。\r\n第三方 API 的留存规则由其服务商决定。",22,70,680,110,false);
   Button(privacy,"清空翻译缓存",22,195,170).Click+=delegate {translator.Clear();MessageBox.Show(this,"翻译缓存已清空。");};
   AddText(privacy,"版本与更新",22,250,550,30,true);
   AddText(privacy,"当前版本："+Updates.CurrentText,22,290,550,25,false);
   AddText(privacy,"更新源：言渡官方 GitHub 仓库（每日自动检查）",22,327,550,26,false);
   AddText(privacy,"手动检查正式版本。下载后校验文件，确认后安装，保留原路径和 API 配置。",22,364,680,42,false);
   var checkUpdate=Button(privacy,"检查更新",22,420,135);
   var downloadUpdate=Button(privacy,"下载并更新",171,420,145);downloadUpdate.Enabled=false;
   var releasePage=Button(privacy,"查看发布页",330,420,145);releasePage.Enabled=true;
   updateStatus.SetBounds(22,473,680,65);updateStatus.ForeColor=Palette.Text;privacy.Controls.Add(updateStatus);
   checkUpdate.Click+=async delegate{
    try{
     checkUpdate.Enabled=downloadUpdate.Enabled=releasePage.Enabled=false;availableRelease=null;updateStatus.Text="正在检查 GitHub 正式发布版本……";privacy.ScrollControlIntoView(updateStatus);
     var release=await updates.Check(lifetime.Token);if(IsDisposed)return;availableRelease=release;releasePage.Enabled=true;
     if(release.Version<=Updates.CurrentVersion){updateStatus.Text="当前已是最新版本（"+Updates.CurrentText+"）。";return;}
     updateStatus.Text="发现新版 "+release.tag_name+(release.Installer==null?"，维护者尚未上传 Windows 安装包。":"，可下载后确认安装。");downloadUpdate.Enabled=release.Installer!=null;
    }catch(OperationCanceledException){if(!IsDisposed)updateStatus.Text="检查已取消或超时，请稍后重试。";}
    catch(Exception ex){if(!IsDisposed)updateStatus.Text=ex is System.Net.Http.HttpRequestException?"无法连接 GitHub，请检查网络后重试。":ex.Message;}
    finally{if(!IsDisposed){checkUpdate.Enabled=true;releasePage.Enabled=true;}}
   };
   releasePage.Click+=delegate{try{Process.Start(new ProcessStartInfo{FileName=availableRelease!=null?availableRelease.html_url:"https://github.com/"+Updates.OfficialRepository+"/releases",UseShellExecute=true});}catch{updateStatus.Text="无法打开浏览器。";}};
   downloadUpdate.Click+=async delegate{
    if(availableRelease==null)return;
    string folder=Path.Combine(Settings.Folder,"Updates",Guid.NewGuid().ToString("N"));
    try{
     checkUpdate.Enabled=downloadUpdate.Enabled=releasePage.Enabled=false;
     updateStatus.Text="正在下载更新……";
     var progress=new Progress<int>(delegate(int value){if(!IsDisposed)updateStatus.Text=value+"% · 下载更新";});
     string installer=await updates.Download(availableRelease,folder,progress,lifetime.Token);if(IsDisposed)return;
     updateStatus.Text="下载及校验完成。";
     if(MessageBox.Show(this,"新版已下载并校验。现在打开安装程序吗？\r\n升级会沿用原路径并保留已保存的 API 配置。", "言渡更新",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes){Process.Start(new ProcessStartInfo{FileName=installer,UseShellExecute=true});updateStatus.Text="安装程序已打开，请在其中确认升级。";}
    }catch(OperationCanceledException){if(!IsDisposed)updateStatus.Text="下载已取消或超时，请重试。";}
    catch(Exception ex){if(!IsDisposed)updateStatus.Text=ex is System.Net.Http.HttpRequestException?"下载失败，请检查 GitHub 网络连接后重试。":ex.Message;}
    finally{if(!IsDisposed){checkUpdate.Enabled=releasePage.Enabled=true;downloadUpdate.Enabled=availableRelease!=null&&availableRelease.Version>Updates.CurrentVersion&&availableRelease.Installer!=null;}}
   };
   foreach(Panel page in tabs.Controls){page.AutoScroll=true;var navButton=new SoftButton{Text=(string)page.Tag,Font=new Font("Microsoft YaHei UI",12),Width=160,Height=48,BackColor=Palette.Background,ForeColor=Palette.Text,Margin=new Padding(0,0,12,0)};nav.Controls.Add(navButton);navButton.Click+=delegate{foreach(Control content in tabs.Controls)content.Visible=content==page;page.BringToFront();foreach(Control item in nav.Controls){item.BackColor=Palette.Background;item.ForeColor=Palette.Text;}navButton.BackColor=Color.FromArgb(174,255,50);navButton.ForeColor=Color.Black;};}
   tabs.Controls[0].Visible=true;tabs.Controls[0].BringToFront();nav.Controls[0].BackColor=Color.FromArgb(174,255,50);nav.Controls[0].ForeColor=Color.Black;
   Shown+=delegate{if(!config.Ready)((Button)nav.Controls[2]).PerformClick();};
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,14,0,0)};layout.Controls.Add(footer,0,3);
   Button(footer,"保存设置",0,0,150).Click+=delegate {try {
    Settings value=Read();config.Endpoint=value.Endpoint;config.Model=value.Model;config.ProtectedKey=value.ProtectedKey;config.Voice=value.Voice;config.SpeechRate=value.SpeechRate;config.SpeechVolume=value.SpeechVolume;config.PauseTranslation=value.PauseTranslation;config.SelectionTranslation=value.SelectionTranslation;config.ScreenshotHotkey=value.ScreenshotHotkey;
    config.Save();saved();Close();
   }catch(Exception ex){MessageBox.Show(this,ex.Message,"请检查设置");}};
   FormClosed+=delegate{lifetime.Cancel();updates.Dispose();synth.SpeakAsyncCancelAll();synth.Dispose();};
  }
  Settings Read() {
   Settings.ChatUri(endpoint.Text);if(String.IsNullOrWhiteSpace(model.Text))throw new ArgumentException("请填写模型名称。");
   string shortcut=CaptureShortcut.Normalize(captureKey.Text);if(shortcut!=CaptureShortcut.Normalize(config.ScreenshotHotkey)&&!(CaptureShortcutAvailable!=null?CaptureShortcutAvailable(shortcut):CaptureShortcut.Probe(Handle,shortcut)))throw new ArgumentException("截图快捷键已被占用，请更换。");
   var value=new Settings {ScreenshotHotkey=shortcut,Endpoint=endpoint.Text.Trim().TrimEnd('/'),Model=model.Text.Trim(),Voice=voice.SelectedIndex>0?(string)voice.SelectedItem:"",SpeechRate=rate.Value,SpeechVolume=volume.Value,PauseTranslation=pause.Checked,SelectionTranslation=selectionTranslation.Checked};value.SetKey(key.Text);return value;
  }
  static Panel Page(Panel tabs,string title){var page=new SoftPanel{Tag=title,Dock=DockStyle.Fill,Visible=false,BackColor=Palette.Surface,ForeColor=Palette.Text};tabs.Controls.Add(page);return page;}
  static void LabelField(Control root,string title,TextBox box,int y){AddText(root,title,22,y,520,25,false);var surface=new SoftPanel{Left=22,Top=y+27,Width=520,Height=43,BackColor=Palette.Background};box.SetBounds(12,9,496,28);box.Font=new Font("Microsoft YaHei UI",11);box.BorderStyle=BorderStyle.None;box.BackColor=Palette.Background;box.ForeColor=Palette.Text;surface.Controls.Add(box);root.Controls.Add(surface);}
  static void AddText(Control root,string text,int x,int y,int w,int h,bool heading){root.Controls.Add(new Label{Text=text,Left=x,Top=y,Width=w,Height=h,ForeColor=heading?Palette.AccentText:Palette.Text,Font=new Font("Microsoft YaHei UI",heading?14:10)});}
  static Button Button(Control root,string text,int x,int y,int width){var b=new SoftButton{Text=text,Left=x,Top=y,Width=width,Height=38,BackColor=Color.FromArgb(174,255,50),ForeColor=Color.Black};root.Controls.Add(b);return b;}
 }
}




