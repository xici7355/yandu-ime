using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing.Imaging;
class RenderConcept {
 static Graphics g;static readonly Color Ink=Color.FromArgb(30,37,32),Muted=Color.FromArgb(111,120,113),Lime=Color.FromArgb(174,255,46),Line=Color.FromArgb(226,232,226);
 static GraphicsPath Round(float x,float y,float w,float h,float r){var p=new GraphicsPath();p.AddArc(x,y,r*2,r*2,180,90);p.AddArc(x+w-r*2,y,r*2,r*2,270,90);p.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90);p.AddArc(x,y+h-r*2,r*2,r*2,90,90);p.CloseFigure();return p;}
 static void Box(float x,float y,float w,float h,Color color,float radius=12){using(var path=Round(x,y,w,h,radius))using(var b=new SolidBrush(color))g.FillPath(b,path);}
 static void Text(string s,float x,float y,float size,Color color,bool bold=false){using(var font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(color))g.DrawString(s,font,brush,x,y);}
 static void Button(string s,int x,int y,int w,bool primary){Box(x,y,w,46,primary?Lime:Color.FromArgb(239,243,238),9);Text(s,x+18,y+10,19,Ink,true);}
 static void Field(string label,string value,int x,int y,int w){Text(label,x,y,16,Muted);Box(x,y+28,w,42,Color.FromArgb(246,248,245),7);Text(value,x+12,y+38,17,Ink);}
 static void Main(string[] args){using(var b=new Bitmap(1500,1050)){g=Graphics.FromImage(b);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;g.Clear(Color.FromArgb(242,245,241));
  Text("言渡输入法",52,34,38,Ink,true);Text("落笔此岸，言达彼岸。",54,87,22,Muted);Text("下一版界面方向 · 概念预览",1100,57,20,Muted);
  Box(48,150,640,535,Color.White,18);Box(712,150,740,535,Color.White,18);
  Text("安装与首次使用",76,174,18,Muted);Text("设置中心",740,174,18,Muted);
  Box(90,236,58,58,Lime,14);Text("渡",104,243,33,Ink,true);Text("言渡输入法",167,237,28,Ink,true);Text("Windows 版",169,277,17,Muted);
  Text("落笔此岸，言达彼岸。",91,332,28,Ink,true);
  Text("中文全拼   /   实时翻译   /   译文朗读",92,383,19,Muted);
  Box(91,432,551,56,Color.FromArgb(246,248,245),9);Text("安装位置",109,448,17,Muted);Text("默认目录",230,448,17,Ink);
  Text("安装完成后，可直接进入 AI 接口配置。",92,514,18,Muted);
  Button("安装言渡",92,580,550,true);
  Box(738,224,156,433,Color.FromArgb(247,249,246),12);string[] nav={"键盘","输入","翻译","隐私"};
  for(int i=0;i<4;i++){if(i==2)Box(750,247+i*65,132,45,Color.FromArgb(232,249,212),8);Text(nav[i],780,257+i*65,20,i==2?Ink:Muted,i==2);}
  Text("翻译",924,227,28,Ink,true);Box(1294,229,114,32,Color.FromArgb(232,249,212),16);Text("已连接",1315,234,16,Color.FromArgb(57,104,21));
  Field("API 接口地址","https://api.deepseek.com/v1",927,282,476);Field("模型名称","deepseek-chat",927,370,476);Field("API 密钥","••••••••••••••••••••••••",927,458,476);
  Button("测试接口",927,554,158,false);Button("保存",1245,554,158,true);Text("朗读设置：系统音色、语速与音量",928,620,16,Muted);
  Box(48,710,920,286,Color.White,18);Text("候选与译文浮窗",76,731,18,Muted);
  Text("jin tian",88,777,17,Muted);Box(88,808,111,38,Color.FromArgb(232,249,212),7);Text("1 今天",100,814,21,Ink,true);Text("2 金田",230,814,21,Ink);Text("3 津田",369,814,21,Ink);
  using(var pen=new Pen(Line))g.DrawLine(pen,88,863,925,863);
  Text("今天的会议改到下午三点。",89,882,18,Muted);Text("Today's meeting has been moved to 3 p.m.",88,917,25,Ink);
  using(var pen=new Pen(Ink,2)){g.DrawPolygon(pen,new[]{new Point(878,918),new Point(886,918),new Point(896,909),new Point(896,939),new Point(886,930),new Point(878,930)});g.DrawArc(pen,894,914,16,21,-65,130);g.DrawArc(pen,895,906,25,37,-65,130);}
  Text("点击译文替换对应中文",89,960,15,Muted);
  Box(992,710,460,286,Color.White,18);Text("先把使用闭环做好",1022,739,23,Ink,true);
  Text("01  微信与 GPT 中译文稳定显示",1022,795,19,Ink);Text("02  设置入口明确，首次配置有引导",1022,850,19,Ink);Text("03  评测词库、首选率与整句输入",1022,905,19,Ink);
  Text("此图为设计方向，尚未替换当前程序。",54,1016,16,Muted);
  b.Save(args[0],ImageFormat.Png);g.Dispose();
 }}
}
