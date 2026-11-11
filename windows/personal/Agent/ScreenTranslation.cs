using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonalIme {
 internal static class ScreenTranslationLayout {
  public static Rectangle Beside(Rectangle selection,Size requested,Rectangle work){
   int width=Math.Min(requested.Width,Math.Max(120,work.Width-24)),height=Math.Min(requested.Height,Math.Max(64,work.Height-130));
   int above=selection.Top-work.Top-12,below=work.Bottom-selection.Bottom-12;
   int outside=Math.Max(above,below)-104;if(height+104>above&&height+104>below&&outside>=80)height=Math.Min(height,outside);
   int y=above>=height?selection.Top-height-10:below>=height?selection.Bottom+10:above>below?work.Top+10:work.Bottom-height-10;
   int x=Math.Max(work.Left+10,Math.Min(selection.Left,work.Right-width-10));
   return new Rectangle(x,Math.Max(work.Top+10,Math.Min(y,work.Bottom-height-10)),width,height);
  }
 }
 [Flags]internal enum CaptureGrip {None=0,Left=1,Right=2,Top=4,Bottom=8,Move=16}
 internal static class CaptureRegionEdit {
  public static CaptureGrip Hit(Rectangle r,Point p,int tolerance){if(!Rectangle.Inflate(r,tolerance,tolerance).Contains(p))return CaptureGrip.None;CaptureGrip grip=CaptureGrip.None;if(Math.Abs(p.X-r.Left)<=tolerance)grip|=CaptureGrip.Left;else if(Math.Abs(p.X-r.Right)<=tolerance)grip|=CaptureGrip.Right;if(Math.Abs(p.Y-r.Top)<=tolerance)grip|=CaptureGrip.Top;else if(Math.Abs(p.Y-r.Bottom)<=tolerance)grip|=CaptureGrip.Bottom;return grip!=CaptureGrip.None?grip:r.Contains(p)?CaptureGrip.Move:CaptureGrip.None;}
  public static Rectangle Adjust(Rectangle original,CaptureGrip grip,Point delta,Rectangle bounds){const int minimum=8;if(grip==CaptureGrip.Move)return new Rectangle(Math.Max(bounds.Left,Math.Min(original.Left+delta.X,bounds.Right-original.Width)),Math.Max(bounds.Top,Math.Min(original.Top+delta.Y,bounds.Bottom-original.Height)),original.Width,original.Height);int l=original.Left,r=original.Right,t=original.Top,b=original.Bottom;if((grip&CaptureGrip.Left)!=0)l=Math.Max(bounds.Left,Math.Min(l+delta.X,r-minimum));if((grip&CaptureGrip.Right)!=0)r=Math.Min(bounds.Right,Math.Max(r+delta.X,l+minimum));if((grip&CaptureGrip.Top)!=0)t=Math.Max(bounds.Top,Math.Min(t+delta.Y,b-minimum));if((grip&CaptureGrip.Bottom)!=0)b=Math.Min(bounds.Bottom,Math.Max(b+delta.Y,t+minimum));return Rectangle.FromLTRB(l,t,r,b);}
 }
 // One screenshot surface, with child controls anchored to the selected region.
 // No separate centered result window; the original desktop pixels stay frozen.
 internal sealed class ScreenTranslation : Form {
  readonly Settings config;readonly Translator translator;readonly Bitmap screenshot;readonly Rectangle desktop;Rectangle selection;
  readonly Func<CancellationToken,Task<string>> recognize;readonly Action<bool> comparisonChanged;
  readonly CancellationTokenSource lifetime=new CancellationTokenSource();CancellationTokenSource request;
  readonly Panel card=new Panel(),toolbar=new Panel();readonly TextBox english=new TextBox();readonly Label status=new Label();
  readonly CheckBox compare=new CheckBox();readonly Button copy=new SoftButton(),copySource=new SoftButton(),retry=new SoftButton(),close=new SoftButton();
  string source="",translated="",origin="";bool closed,ready,blocked,resourcesDisposed;
  [DllImport("user32.dll")]static extern uint GetClipboardSequenceNumber();
  Point dragStart;Rectangle dragOriginal;CaptureGrip dragGrip;bool dragging;
  uint copiedImageSequence;bool captureOnly;readonly bool fromSelection,allowCover;readonly ToolTip hint=new ToolTip();
  string DirectionText {get{return String.IsNullOrWhiteSpace(source)?"自动识别":Translator.ReadingTargetChinese(source)?"英 → 中":"中 → 英";}}
  internal int RequestTimeoutMilliseconds=35000;
  public ScreenTranslation(Settings settings,Translator engine,Bitmap image,Rectangle bounds,Rectangle region,Action<bool> onCompare,Func<CancellationToken,Task<string>> ocr=null,string selectedSource=null,bool allowCover=true,bool captureOnly=false){
   this.captureOnly=captureOnly;config=settings;translator=engine;screenshot=image;desktop=bounds;selection=region;comparisonChanged=onCompare;
   fromSelection=selectedSource!=null;this.allowCover=allowCover;if(fromSelection)source=selectedSource;
   recognize=ocr??(token=>LocalOcr.Recognize(screenshot.Clone(new Rectangle(selection.Left-desktop.Left,selection.Top-desktop.Top,selection.Width,selection.Height),PixelFormat.Format24bppRgb),token));
   Text=fromSelection?"言渡 · 划词翻译":"言渡 · 截屏翻译";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=bounds;AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;KeyPreview=true;
   card.BackColor=Palette.Surface;card.Padding=new Padding(8);Controls.Add(card);english.Multiline=true;english.ReadOnly=true;english.ImeMode=ImeMode.Disable;english.ScrollBars=ScrollBars.Vertical;english.BorderStyle=BorderStyle.None;english.BackColor=Palette.Surface;english.ForeColor=Palette.Text;english.Font=new Font("Microsoft YaHei UI",11);english.Dock=DockStyle.Fill;card.Controls.Add(english);
   toolbar.BackColor=Palette.Surface;Controls.Add(toolbar);compare.Text="对照";compare.Checked=!allowCover||config.CaptureComparison;compare.Enabled=allowCover;if(!allowCover)hint.SetToolTip(compare,"无法定位原文，仅支持对照；也可使用截屏翻译选择范围。");compare.ForeColor=Palette.Text;compare.SetBounds(12,8,74,32);toolbar.Controls.Add(compare);
   Add(copy,"复制译文",92,100);Add(copySource,"复制原文",198,100);Add(retry,"重试",304,70);Add(close,"关闭",380,70);
   status.ForeColor=Palette.Secondary;status.SetBounds(12,46,438,42);status.Font=new Font("Microsoft YaHei UI",9);toolbar.Controls.Add(status);
   copy.Enabled=copySource.Enabled=false;copy.Click+=async delegate{if(captureOnly){if(await CopyImage())Close();}else Copy(translated);};copySource.Click+=delegate{Copy(source);};retry.Click+=async delegate{captureOnly=false;card.Visible=true;copy.Text="复制译文";copySource.Visible=true;compare.Enabled=allowCover;await Run(!fromSelection&&String.IsNullOrWhiteSpace(source),ready&&origin=="本地词典");};close.Click+=delegate{Close();};
   compare.CheckedChanged+=delegate{PositionContent();if(comparisonChanged!=null)comparisonChanged(compare.Checked);};
   KeyDown+=async delegate(object sender,KeyEventArgs e){if(captureOnly&&e.KeyCode==Keys.Enter){e.Handled=e.SuppressKeyPress=true;if(await CopyImage())Close();return;}if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}};
   Shown+=async delegate{PositionContent();if(captureOnly){card.Visible=false;compare.Enabled=false;retry.Text="翻译";copy.Text="复制截图";copy.Enabled=true;copySource.Visible=false;status.Text="截图已复制；可拖动选区或边缘调整。点击复制截图或按 Enter 退出。";await CopyImage();}else await Run(!fromSelection);};FormClosed+=delegate{closed=true;lifetime.Cancel();if(request!=null)request.Cancel();};
  }
  void Add(Button button,string text,int left,int width){button.Text=text;button.SetBounds(left,8,width,32);button.BackColor=Color.FromArgb(174,255,50);button.ForeColor=Color.Black;toolbar.Controls.Add(button);}
  void Copy(string text){if(String.IsNullOrEmpty(text))return;try{Clipboard.SetText(text);status.Text="已复制 · "+DirectionText;}catch(ExternalException){status.Text="剪贴板正忙，请重试。";}}
  async Task<bool> CopyImage(){for(int attempt=0;attempt<5&&!closed;attempt++){try{using(var image=screenshot.Clone(new Rectangle(selection.Left-desktop.Left,selection.Top-desktop.Top,selection.Width,selection.Height),PixelFormat.Format24bppRgb))Clipboard.SetImage(image);copiedImageSequence=GetClipboardSequenceNumber();if(captureOnly)status.Text="截图已复制；可拖动选区或边缘，复制截图或 Enter 退出。";return true;}catch(ExternalException){}await Task.Delay(80);}if(!closed)status.Text="剪贴板正忙，请点击“复制截图”重试。";return false;}
  Rectangle Local(Rectangle screen){screen.Offset(-desktop.Left,-desktop.Top);return screen;}
  void PositionContent(){
   if(closed)return;var work=Screen.FromRectangle(selection).WorkingArea;int width=Math.Max(300,Math.Min(Math.Max(selection.Width,460),work.Width-24));
   if(ready)status.Text=origin+" · "+DirectionText+(!allowCover?" · 无法定位原文，已使用对照。":compare.Checked?" · 对照模式；长译文可滚动查看。":" · 覆盖模式；对照可查看全文。");
   var text=ready?translated:english.Text;int height;
   using(var font=new Font("Microsoft YaHei UI",11)){height=TextRenderer.MeasureText(text??"",font,new Size(width-28,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+24;}
   height=Math.Max(64,Math.Min(height,work.Height-120));
   card.Bounds=Local(compare.Checked||!ready?ScreenTranslationLayout.Beside(selection,new Size(width,height),work):selection);
   card.Padding=!compare.Checked&&ready?new Padding(Math.Min(6,Math.Max(1,card.Height/8))):new Padding(8);
   if(!compare.Checked&&ready){float size=11;while(size>7){using(var font=new Font("Microsoft YaHei UI",size)){var measure=TextRenderer.MeasureText(translated,font,new Size(Math.Max(8,card.Width-card.Padding.Horizontal-6),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);if(measure.Height<=Math.Max(12,card.Height-card.Padding.Vertical))break;}size-=0.5f;}if(Math.Abs(english.Font.Size-size)>0.1f){var old=english.Font;english.Font=new Font("Microsoft YaHei UI",size);old.Dispose();}}
   else if(Math.Abs(english.Font.Size-11)>0.1f){var old=english.Font;english.Font=new Font("Microsoft YaHei UI",11);old.Dispose();}
   var measured=TextRenderer.MeasureText(english.Text,english.Font,new Size(Math.Max(8,card.Width-card.Padding.Horizontal-6),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);english.ScrollBars=card.Width>=60&&measured.Height>card.Height-card.Padding.Vertical?ScrollBars.Vertical:ScrollBars.None;
   int toolbarWidth=Math.Min(464,work.Width-20),toolbarHeight=94;int x=Math.Max(work.Left+8,Math.Min(selection.Left,work.Right-toolbarWidth-8));
   int y=selection.Bottom+12;if(y+toolbarHeight>work.Bottom)y=selection.Top-toolbarHeight-12;y=Math.Max(work.Top+8,Math.Min(y,work.Bottom-toolbarHeight-8));
   var screenCard=new Rectangle(card.Left+desktop.Left,card.Top+desktop.Top,card.Width,card.Height);
   if(compare.Checked||!ready){if(y<screenCard.Bottom&&y+toolbarHeight>screenCard.Top){if(screenCard.Bottom+10+toolbarHeight<=work.Bottom)y=screenCard.Bottom+10;else if(screenCard.Top-toolbarHeight-10>=work.Top)y=screenCard.Top-toolbarHeight-10;else y=work.Bottom-toolbarHeight-8;}}
   toolbar.Bounds=Local(new Rectangle(x,Math.Max(work.Top+8,Math.Min(y,work.Bottom-toolbarHeight-8)),toolbarWidth,toolbarHeight));toolbar.BringToFront();Invalidate();
  }
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImageUnscaled(screenshot,0,0);using(var shade=new SolidBrush(Color.FromArgb(90,0,0,0)))e.Graphics.FillRectangle(shade,ClientRectangle);if(allowCover){var r=Local(selection);e.Graphics.DrawImage(screenshot,r,r,GraphicsUnit.Pixel);using(var pen=new Pen(Color.FromArgb(174,255,50),2))e.Graphics.DrawRectangle(pen,r);if(CanEdit){foreach(var point in new[]{new Point(r.Left,r.Top),new Point(r.Left+r.Width/2,r.Top),new Point(r.Right,r.Top),new Point(r.Left,r.Top+r.Height/2),new Point(r.Right,r.Top+r.Height/2),new Point(r.Left,r.Bottom),new Point(r.Left+r.Width/2,r.Bottom),new Point(r.Right,r.Bottom)})e.Graphics.FillRectangle(Brushes.White,point.X-3,point.Y-3,6,6);}}}
  bool CanEdit {get{return captureOnly&&!fromSelection&&!closed;}}
  Cursor GripCursor(CaptureGrip grip){if(grip==CaptureGrip.Move)return Cursors.SizeAll;if(grip==(CaptureGrip.Left|CaptureGrip.Top)||grip==(CaptureGrip.Right|CaptureGrip.Bottom))return Cursors.SizeNWSE;if(grip==(CaptureGrip.Right|CaptureGrip.Top)||grip==(CaptureGrip.Left|CaptureGrip.Bottom))return Cursors.SizeNESW;if((grip&(CaptureGrip.Left|CaptureGrip.Right))!=0)return Cursors.SizeWE;if((grip&(CaptureGrip.Top|CaptureGrip.Bottom))!=0)return Cursors.SizeNS;return Cursors.Default;}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!CanEdit||e.Button!=MouseButtons.Left)return;var grip=CaptureRegionEdit.Hit(Local(selection),e.Location,8);if(grip==CaptureGrip.None)return;dragStart=e.Location;dragOriginal=Local(selection);dragGrip=grip;dragging=true;Capture=true;Cursor=GripCursor(grip);}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!CanEdit)return;if(dragging){var next=CaptureRegionEdit.Adjust(dragOriginal,dragGrip,new Point(e.X-dragStart.X,e.Y-dragStart.Y),ClientRectangle);next.Offset(desktop.Left,desktop.Top);selection=next;PositionContent();}else Cursor=GripCursor(CaptureRegionEdit.Hit(Local(selection),e.Location,8));}
  protected override async void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging||e.Button!=MouseButtons.Left)return;OnMouseMove(e);dragging=false;Capture=false;Cursor=Cursors.Default;await CopyImage();}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(dragging&&!Capture){dragging=false;selection=new Rectangle(dragOriginal.X+desktop.Left,dragOriginal.Y+desktop.Top,dragOriginal.Width,dragOriginal.Height);PositionContent();}}
  protected override async void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(CanEdit&&e.Button==MouseButtons.Left&&Local(selection).Contains(e.Location)){dragging=false;Capture=false;if(await CopyImage())Close();}}
  async Task Run(bool withOcr,bool forceAi=false){
   if(request!=null||closed||blocked)return;var active=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);request=active;active.CancelAfter(RequestTimeoutMilliseconds);ready=false;translated=origin="";copy.Enabled=retry.Enabled=false;english.Text="";
   try{if(withOcr){status.Text="自动识别 · 正在本机识别文字……";english.Text=status.Text;PositionContent();source=await recognize(active.Token);active.Token.ThrowIfCancellationRequested();}
    if(!ReadingPolicy.ValidText(source))throw new InvalidOperationException(String.IsNullOrWhiteSpace(source)?"没有识别到文字，请重新框选清晰的中文区域。":"识别文字过长，请缩小框选范围。");
    PrivacyPolicy.RequireSafe(source);copySource.Enabled=true;
    status.Text=DirectionText+(forceAi?" · AI精译中……":" · 本地优先查询……");english.Text=status.Text;PositionContent();var result=await translator.TranslateReadingDetailed(config,source,active.Token,forceAi);active.Token.ThrowIfCancellationRequested();
    if(!closed){translated=result.Text;origin=result.Origin;ready=true;english.Text=translated;copy.Enabled=true;retry.Text=origin=="本地词典"?"AI精译":"重试";PositionContent();}
   }catch(SensitiveContentException ex){if(!closed){blocked=true;source=translated="";if(copiedImageSequence!=0&&GetClipboardSequenceNumber()==copiedImageSequence)try{Clipboard.Clear();}catch(ExternalException){}copy.Enabled=copySource.Enabled=false;using(var graphics=Graphics.FromImage(screenshot))graphics.FillRectangle(Brushes.DimGray,Local(selection));ShowError(ex.Message);}}
   catch(OperationCanceledException){if(!closed)ShowError("识别或翻译超时，原图保留，可点击重试。");}
   catch(Exception ex){if(!closed)ShowError(ex is System.Net.Http.HttpRequestException?"无法连接翻译接口，请检查网络和接口设置后重试。":ex.Message);}
   finally{request=null;active.Dispose();if(!closed)retry.Enabled=!blocked&&(!ready||origin!="本地词典"||config.Ready);}
  }
  void ShowError(string text){status.Text=DirectionText+" · 未完成";english.Text=text;PositionContent();}
  protected override void Dispose(bool disposing){if(disposing&&!resourcesDisposed){resourcesDisposed=true;closed=true;lifetime.Cancel();if(request!=null)request.Cancel();hint.Dispose();screenshot.Dispose();}base.Dispose(disposing);}
 }
}
