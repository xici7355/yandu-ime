using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
class BuildStateIcons {
 static byte[] Frame(int size,string text){using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var graphics=Graphics.FromImage(bitmap))using(var stream=new MemoryStream()){
  graphics.Clear(Color.Transparent);graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
  float inset=size/32f,radius=size*0.25f;var rect=new RectangleF(inset,inset,size-inset*2,size-inset*2);float d=radius*2;
  using(var path=new GraphicsPath()){path.AddArc(rect.Left,rect.Top,d,d,180,90);path.AddArc(rect.Right-d,rect.Top,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);path.CloseFigure();using(var fill=new SolidBrush(Color.FromArgb(41,70,91)))graphics.FillPath(fill,path);}
  using(var font=new Font(text=="中"?"Microsoft YaHei UI":"Segoe UI",size*(text=="中"?0.66f:0.53f),FontStyle.Bold,GraphicsUnit.Pixel))using(var fill=new SolidBrush(Color.FromArgb(247,250,252)))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})graphics.DrawString(text,font,fill,new RectangleF(0,-size*0.015f,size,size),format);
  bitmap.Save(stream,ImageFormat.Png);return stream.ToArray();
 }}
 static void IconFile(string folder,string name,string text){int[] sizes={16,20,24,32,40,48,64,128,256};var frames=new List<byte[]>();foreach(int size in sizes)frames.Add(Frame(size,text));using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,name+".ico")))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}foreach(var frame in frames)writer.Write(frame);}
  File.WriteAllBytes(Path.Combine(folder,name+"-preview.png"),Frame(64,text));
 }
 static void Main(string[] args){var folder=Path.GetFullPath(args.Length>0?args[0]:"personal/Assets");Directory.CreateDirectory(folder);IconFile(folder,"state-zh","中");IconFile(folder,"state-en","EN");}
}
