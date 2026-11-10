using System;
using System.Runtime.InteropServices;
using System.Text;
namespace PersonalIme {
 internal static class Native {
  public const int WM_COPYDATA=0x4a, WM_HOTKEY=0x312;
  public const long Magic=0x5054494d;
  [DllImport("gdi32.dll")]public static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
  [DllImport("gdi32.dll")]public static extern bool DeleteObject(IntPtr value);
  [DllImport("user32.dll")]public static extern bool SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
  [StructLayout(LayoutKind.Sequential)] public struct PaintInfo {public IntPtr Hdc;public int Erase;public int Left,Top,Right,Bottom;public int Restore,Update;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Reserved;}
  [DllImport("user32.dll")]public static extern IntPtr BeginPaint(IntPtr window,out PaintInfo paint);
  [DllImport("user32.dll")]public static extern bool EndPaint(IntPtr window,ref PaintInfo paint);
  [DllImport("user32.dll")]public static extern bool InvalidateRect(IntPtr window,IntPtr rectangle,bool erase);
  [StructLayout(LayoutKind.Sequential)] public struct CopyData {public IntPtr Tag; public int Size; public IntPtr Data;}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,int msg,IntPtr wp,ref CopyData data,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
  [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint processId);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className,string title);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
  public static bool Send(IntPtr target, IntPtr sender, string text) {
   byte[] bytes=Encoding.Unicode.GetBytes(text);
   IntPtr memory=Marshal.AllocHGlobal(bytes.Length);
   try {
    Marshal.Copy(bytes,0,memory,bytes.Length);
    var data=new CopyData {Tag=new IntPtr(Magic),Size=bytes.Length,Data=memory}; IntPtr result;
    return SendMessageTimeout(target,WM_COPYDATA,sender,ref data,2,150,out result)!=IntPtr.Zero && result!=IntPtr.Zero;
   } finally {Marshal.FreeHGlobal(memory);}
  }
  public static string Read(IntPtr ptr) {
   var data=(CopyData)Marshal.PtrToStructure(ptr,typeof(CopyData));
   if(data.Tag.ToInt64()!=Magic || data.Size<2 || data.Size>65536 || data.Size%2!=0 || data.Data==IntPtr.Zero) return null;
   return Marshal.PtrToStringUni(data.Data,data.Size/2);
  }
 }
 public sealed class Snapshot {
  public long Revision; public IntPtr Target,Foreground; public int X,Y; public string Source; public string Mode="legacy";
  public static Snapshot Parse(string input) {
   int n=input.IndexOf('\n'); if(n<0) return null;
   var fields=input.Substring(0,n).Split('|');
   long rev,target,foreground; int x,y;
   if((fields.Length!=6 && fields.Length!=7) || fields[0]!="S" || !Int64.TryParse(fields[1],out rev) || !Int64.TryParse(fields[2],out target) || !Int64.TryParse(fields[3],out foreground) || !Int32.TryParse(fields[4],out x) || !Int32.TryParse(fields[5],out y)) return null;
   return new Snapshot {Revision=rev,Target=new IntPtr(target),Foreground=new IntPtr(foreground),X=x,Y=y,Source=input.Substring(n+1),Mode=fields.Length==7?fields[6]:"legacy"};
  }
 }
}
