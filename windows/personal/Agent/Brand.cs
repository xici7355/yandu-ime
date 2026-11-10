using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace PersonalIme {
 internal class SoftPanel : Panel {
  public SoftPanel(){DoubleBuffered=true;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Shape(ClientRectangle))using(var fill=new SolidBrush(BackColor))e.Graphics.FillPath(fill,path);}
  internal static GraphicsPath Shape(Rectangle r){var p=new GraphicsPath();int d=16;r.Width--;r.Height--;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 }
 internal class SoftButton : Button {
  public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);using(var path=SoftPanel.Shape(ClientRectangle))using(var fill=new SolidBrush(Enabled?BackColor:Color.LightGray))e.Graphics.FillPath(fill,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5));}
 }
 internal static class Brand {
  public static readonly Icon Icon=CreateIcon();
  public static Bitmap CreateImage(){using(var stream=typeof(Brand).Assembly.GetManifestResourceStream("yandu.png")){if(stream!=null)using(var source=Image.FromStream(stream))return new Bitmap(source);}return Icon.ToBitmap();}
  static Icon CreateIcon(){using(var stream=typeof(Brand).Assembly.GetManifestResourceStream("yandu.ico")){if(stream!=null)using(var icon=new Icon(stream,128,128))return (Icon)icon.Clone();}return (Icon)SystemIcons.Application.Clone();}
 }
}
