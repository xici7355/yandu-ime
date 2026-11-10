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
 // One screenshot surface, with child controls anchored to the selected region.
 // No separate centered result window; the original desktop pixels stay frozen.
 internal sealed class ScreenTranslation : Form {
  readonly Settings config;readonly Translator translator;readonly Bitmap screenshot;readonly Rectangle desktop,selection;
  readonly Func<CancellationToken,Task<string>> recognize;readonly Action<bool> comparisonChanged;
  readonly CancellationTokenSource lifetime=new CancellationTokenSource();CancellationTokenSource request;
  readonly Panel card=new Panel(),toolbar=new Panel();readonly TextBox english=new TextBox();readonly Label status=new Label();
  readonly CheckBox compare=new CheckBox();readonly Button copy=new SoftButton(),copySource=new SoftButton(),retry=new SoftButton(),close=new SoftButton();
  string source="",translated="";bool closed,ready;
  string DirectionText {get{return String.IsNullOrWhiteSpace(source)?"自动识别":Translator.ReadingTargetChinese(source)?"英 → 中":"中 → 英";}}
  internal int RequestTimeoutMilliseconds=35000;
  public ScreenTranslation(Settings settings,Translator engine,Bitmap image,Rectangle bounds,Rectangle region,Action<bool> onCompare,Func<CancellationToken,Task<string>> ocr=null){
   config=settings;translator=engine;screenshot=image;desktop=bounds;selection=region;comparisonChanged=onCompare;
   recognize=ocr??(token=>LocalOcr.Recognize(screenshot.Clone(new Rectangle(selection.Left-desktop.Left,selection.Top-desktop.Top,selection.Width,selection.Height),PixelFormat.Format24bppRgb),token));
   Text="言渡 · 截屏翻译";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=bounds;AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;KeyPreview=true;
   card.BackColor=Palette.Surface;card.Padding=new Padding(8);Controls.Add(card);english.Multiline=true;english.ReadOnly=true;english.ScrollBars=ScrollBars.Vertical;english.BorderStyle=BorderStyle.None;english.BackColor=Palette.Surface;english.ForeColor=Palette.Text;english.Font=new Font("Microsoft YaHei UI",11);english.Dock=DockStyle.Fill;card.Controls.Add(english);
   toolbar.BackColor=Palette.Surface;Controls.Add(toolbar);compare.Text="对照";compare.Checked=config.CaptureComparison;compare.ForeColor=Palette.Text;compare.SetBounds(12,8,74,32);toolbar.Controls.Add(compare);
   Add(copy,"复制译文",92,100);Add(copySource,"复制原文",198,100);Add(retry,"重试",304,70);Add(close,"关闭",380,70);
   status.ForeColor=Palette.Secondary;status.SetBounds(12,46,438,42);status.Font=new Font("Microsoft YaHei UI",9);toolbar.Controls.Add(status);
   copy.Enabled=copySource.Enabled=false;copy.Click+=delegate{Copy(translated);};copySource.Click+=delegate{Copy(source);};retry.Click+=async delegate{await Run(String.IsNullOrWhiteSpace(source));};close.Click+=delegate{Close();};
   compare.CheckedChanged+=delegate{PositionContent();if(comparisonChanged!=null)comparisonChanged(compare.Checked);};
   KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}};
   Shown+=async delegate{PositionContent();await Run(true);};FormClosed+=delegate{closed=true;lifetime.Cancel();if(request!=null)request.Cancel();};
  }
  void Add(Button button,string text,int left,int width){button.Text=text;button.SetBounds(left,8,width,32);button.BackColor=Color.FromArgb(174,255,50);button.ForeColor=Color.Black;toolbar.Controls.Add(button);}
  void Copy(string text){if(String.IsNullOrEmpty(text))return;try{Clipboard.SetText(text);status.Text="已复制 · "+DirectionText;}catch(ExternalException){status.Text="剪贴板正忙，请重试。";}}
  Rectangle Local(Rectangle screen){screen.Offset(-desktop.Left,-desktop.Top);return screen;}
  void PositionContent(){
   if(closed)return;var work=Screen.FromRectangle(selection).WorkingArea;int width=Math.Max(300,Math.Min(Math.Max(selection.Width,460),work.Width-24));
   if(ready)status.Text=DirectionText+(compare.Checked?" · 对照模式；长译文可滚动查看。":" · 覆盖模式；打开对照查看完整译文。");
   var text=ready?translated:english.Text;int height;
   using(var font=new Font("Microsoft YaHei UI",11)){height=TextRenderer.MeasureText(text??"",font,new Size(width-28,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+24;}
   height=Math.Max(64,Math.Min(height,work.Height-120));
   card.Bounds=Local(compare.Checked||!ready?ScreenTranslationLayout.Beside(selection,new Size(width,height),work):selection);
   if(!compare.Checked&&ready){float size=11;while(size>7){using(var font=new Font("Microsoft YaHei UI",size)){var measure=TextRenderer.MeasureText(translated,font,new Size(Math.Max(24,card.Width-24),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);if(measure.Height<=Math.Max(20,card.Height-16))break;}size-=0.5f;}var old=english.Font;english.Font=new Font("Microsoft YaHei UI",size);old.Dispose();}
   else{var old=english.Font;english.Font=new Font("Microsoft YaHei UI",11);old.Dispose();}
   var measured=TextRenderer.MeasureText(english.Text,english.Font,new Size(Math.Max(24,card.Width-24),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);english.ScrollBars=measured.Height>card.Height-16?ScrollBars.Vertical:ScrollBars.None;
   int toolbarWidth=Math.Min(464,work.Width-20),toolbarHeight=94;int x=Math.Max(work.Left+8,Math.Min(selection.Left,work.Right-toolbarWidth-8));
   int y=selection.Bottom+12;if(y+toolbarHeight>work.Bottom)y=selection.Top-toolbarHeight-12;y=Math.Max(work.Top+8,Math.Min(y,work.Bottom-toolbarHeight-8));
   var screenCard=new Rectangle(card.Left+desktop.Left,card.Top+desktop.Top,card.Width,card.Height);
   if(compare.Checked||!ready){if(y<screenCard.Bottom&&y+toolbarHeight>screenCard.Top){if(screenCard.Bottom+10+toolbarHeight<=work.Bottom)y=screenCard.Bottom+10;else if(screenCard.Top-toolbarHeight-10>=work.Top)y=screenCard.Top-toolbarHeight-10;else y=work.Bottom-toolbarHeight-8;}}
   toolbar.Bounds=Local(new Rectangle(x,Math.Max(work.Top+8,Math.Min(y,work.Bottom-toolbarHeight-8)),toolbarWidth,toolbarHeight));toolbar.BringToFront();Invalidate();
  }
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImageUnscaled(screenshot,0,0);using(var shade=new SolidBrush(Color.FromArgb(90,0,0,0)))e.Graphics.FillRectangle(shade,ClientRectangle);var r=Local(selection);e.Graphics.DrawImage(screenshot,r,r,GraphicsUnit.Pixel);using(var pen=new Pen(Color.FromArgb(174,255,50),2))e.Graphics.DrawRectangle(pen,r);}
  async Task Run(bool withOcr){
   if(request!=null||closed)return;var active=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);request=active;active.CancelAfter(RequestTimeoutMilliseconds);ready=false;translated="";copy.Enabled=retry.Enabled=false;english.Text="";
   try{if(withOcr){status.Text="自动识别 · 正在本机识别文字……";english.Text=status.Text;PositionContent();source=await recognize(active.Token);active.Token.ThrowIfCancellationRequested();}
    if(!ReadingPolicy.ValidText(source))throw new InvalidOperationException(String.IsNullOrWhiteSpace(source)?"没有识别到文字，请重新框选清晰的中文区域。":"识别文字过长，请缩小框选范围。");
    copySource.Enabled=true;if(!config.Ready)throw new InvalidOperationException("尚未配置 API 或已暂停翻译。识别原文仍可复制，请先在设置中验证连接。");
    status.Text=DirectionText+" · 正在翻译……";english.Text=status.Text;PositionContent();var result=await translator.TranslateReading(config,source,active.Token);active.Token.ThrowIfCancellationRequested();
    if(!closed){translated=result;ready=true;english.Text=result;copy.Enabled=true;PositionContent();}
   }catch(OperationCanceledException){if(!closed)ShowError("识别或翻译超时，原图保留，可点击重试。");}
   catch(Exception ex){if(!closed)ShowError(ex is System.Net.Http.HttpRequestException?"无法连接翻译接口，请检查网络和接口设置后重试。":ex.Message);}
   finally{request=null;active.Dispose();if(!closed)retry.Enabled=true;}
  }
  void ShowError(string text){status.Text=DirectionText+" · 未完成";english.Text=text;PositionContent();}
  protected override void Dispose(bool disposing){if(disposing){closed=true;lifetime.Cancel();if(request!=null)request.Cancel();screenshot.Dispose();}base.Dispose(disposing);}
 }
}
