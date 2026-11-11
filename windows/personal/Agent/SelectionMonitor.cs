using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonalIme {
 internal sealed class SelectionMonitor : IDisposable {
  delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
  [StructLayout(LayoutKind.Sequential)]struct MouseData {public int X,Y;public uint Mouse,Flags,Time;public UIntPtr Extra;}
  [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,Hook callback,IntPtr module,uint thread);
  [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
  readonly Form owner;readonly Func<bool> enabled;readonly Action<SelectedText> translate;readonly Hook callback;
  readonly TranslateBadge badge=new TranslateBadge();readonly Timer timer=new Timer{Interval=150};
  IntPtr hook;SelectedText selection;Task<SelectionRead> pending;readonly Func<IntPtr,Point,SelectionRead> read;IntPtr requestedWindow;Point requestedPoint;DateTime requestedAt,shownAt;long generation,readingGeneration;bool disposed;
  public SelectionMonitor(Form window,Func<bool> isEnabled,Action<SelectedText> action,Func<IntPtr,Point,SelectionRead> reader=null){read=reader??SelectedText.ReadChecked;owner=window;enabled=isEnabled;translate=action;callback=MouseHook;
   hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);timer.Tick+=delegate{Tick();};timer.Start();badge.Requested+=delegate{Confirm();};
  }
  public bool Available {get{return hook!=IntPtr.Zero;}}
  IntPtr MouseHook(int code,IntPtr message,IntPtr data){
   // Always forward every mouse event. Selection discovery runs after the callback.
   if(code>=0&&!disposed){int kind=message.ToInt32();if(kind==0x202||kind==0x205||kind==0x20a){var value=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));var point=new Point(value.X,value.Y);bool badgeHit=badge.Visible&&badge.Bounds.Contains(point);try{owner.BeginInvoke((Action)delegate{
     if(disposed||badgeHit)return;Hide();generation++;
     if(kind==0x202&&enabled()){requestedPoint=point;requestedWindow=Native.GetForegroundWindow();requestedAt=DateTime.UtcNow.AddMilliseconds(250);}
    });}catch(InvalidOperationException){}}
   }return CallNextHookEx(hook,code,message,data);
  }
  void Tick(){if(disposed)return;if(!enabled()){Hide();requestedWindow=IntPtr.Zero;return;}
   if(badge.Visible&&(selection==null||Native.GetForegroundWindow()!=selection.Foreground||(DateTime.UtcNow-shownAt).TotalSeconds>8))Hide();
   if(pending!=null){if(!pending.IsCompleted)return;var completed=pending;pending=null;if(completed.Status==TaskStatus.RanToCompletion&&readingGeneration==generation&&completed.Result!=null&&completed.Result.State==SelectionReadState.Ready&&completed.Result.Value!=null&&Native.GetForegroundWindow()==completed.Result.Value.Foreground){selection=completed.Result.Value;shownAt=DateTime.UtcNow;badge.At(selection.Anchor);}return;}
   if(requestedWindow==IntPtr.Zero||DateTime.UtcNow<requestedAt)return;var target=requestedWindow;var point=requestedPoint;requestedWindow=IntPtr.Zero;readingGeneration=generation;pending=Task.Run(()=>read(target,point));
  }
  async void Confirm(){var captured=selection;Hide();generation++;if(captured==null||pending!=null||!enabled())return;
   readingGeneration=-1;pending=Task.Run(()=>read(captured.Foreground,captured.Anchor));var active=pending;
   // A broken accessibility provider cannot block the keyboard or create unbounded workers.
   if(await Task.WhenAny(active,Task.Delay(1500))!=active){Notify("读取选区超时，请重新划词或使用截图翻译。");return;}if(disposed)return;pending=null;
   if(!enabled()||Native.GetForegroundWindow()!=captured.Foreground)return;
   if(active.Status!=TaskStatus.RanToCompletion||active.Result==null){Notify("暂时无法读取选区，请重新选择。");return;}
   var result=active.Result;
   if(result.State==SelectionReadState.Ready&&result.Value!=null){translate(result.Value);return;}
   if((result.State==SelectionReadState.Empty||result.State==SelectionReadState.Unavailable)&&ReadingPolicy.ValidText(captured.Text)&&!PrivacyPolicy.IsSensitive(captured.Text)){
    if(await PrivacyPolicy.ProtectedFocus()){Notify("当前输入受保护或无法确认安全，未开始翻译。");return;}
    if(disposed||!enabled()||Native.GetForegroundWindow()!=captured.Foreground)return;
    // Clicking the badge can collapse a browser selection. Use only the already
    // validated snapshot explicitly confirmed by that click; never reread a full document.
    translate(new SelectedText{Text=captured.Text,Foreground=captured.Foreground,Anchor=captured.Anchor,Region=Rectangle.Empty});return;
   }
   Notify(result.State==SelectionReadState.Blocked?"选区受隐私保护或内容过长，未开始翻译。":"暂时无法读取选区，请重新选择或使用截图翻译。");
  }
  public event Action<string> Unavailable;
  void Notify(string message){if(!disposed&&Unavailable!=null)Unavailable(message);}
  public void Hide(){selection=null;badge.Hide();}
  public void Dispose(){if(disposed)return;disposed=true;timer.Dispose();if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}badge.Dispose();}
 }
}
