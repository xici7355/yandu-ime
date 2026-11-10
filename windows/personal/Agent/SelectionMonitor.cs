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
  IntPtr hook;SelectedText selection;Task<SelectedText> pending;IntPtr requestedWindow;Point requestedPoint;DateTime requestedAt,shownAt;long generation,readingGeneration;bool disposed;
  public SelectionMonitor(Form window,Func<bool> isEnabled,Action<SelectedText> action){owner=window;enabled=isEnabled;translate=action;callback=MouseHook;
   hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);timer.Tick+=delegate{Tick();};timer.Start();badge.Requested+=delegate{Confirm();};
  }
  public bool Available {get{return hook!=IntPtr.Zero;}}
  IntPtr MouseHook(int code,IntPtr message,IntPtr data){
   // Always forward every mouse event. Selection discovery runs after the callback.
   if(code>=0&&!disposed){int kind=message.ToInt32();if(kind==0x202||kind==0x205||kind==0x20a){var value=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));var point=new Point(value.X,value.Y);try{owner.BeginInvoke((Action)delegate{
     if(disposed)return;if(badge.Visible&&badge.Bounds.Contains(point))return;Hide();generation++;
     if(kind==0x202&&enabled()){requestedPoint=point;requestedWindow=Native.GetForegroundWindow();requestedAt=DateTime.UtcNow.AddMilliseconds(250);}
    });}catch(InvalidOperationException){}}
   }return CallNextHookEx(hook,code,message,data);
  }
  void Tick(){if(disposed)return;if(!enabled()){Hide();requestedWindow=IntPtr.Zero;return;}
   if(badge.Visible&&(selection==null||Native.GetForegroundWindow()!=selection.Foreground||(DateTime.UtcNow-shownAt).TotalSeconds>8))Hide();
   if(pending!=null){if(!pending.IsCompleted)return;var completed=pending;pending=null;if(completed.Status==TaskStatus.RanToCompletion&&readingGeneration==generation&&completed.Result!=null&&Native.GetForegroundWindow()==completed.Result.Foreground){selection=completed.Result;shownAt=DateTime.UtcNow;badge.At(selection.Anchor);}return;}
   if(requestedWindow==IntPtr.Zero||DateTime.UtcNow<requestedAt)return;var target=requestedWindow;var point=requestedPoint;requestedWindow=IntPtr.Zero;readingGeneration=generation;pending=Task.Run(()=>SelectedText.Read(target,point));
  }
  async void Confirm(){var captured=selection;Hide();generation++;if(captured==null||pending!=null||!enabled())return;
   readingGeneration=-1;pending=Task.Run(()=>SelectedText.Read(captured.Foreground,captured.Anchor));var active=pending;
   // A broken accessibility provider cannot block the keyboard or create unbounded workers.
   if(await Task.WhenAny(active,Task.Delay(1000))!=active)return;if(disposed)return;pending=null;
   if(active.Status==TaskStatus.RanToCompletion&&active.Result!=null&&active.Result.Text==captured.Text&&Native.GetForegroundWindow()==captured.Foreground)translate(active.Result);
  }
  public void Hide(){selection=null;badge.Hide();}
  public void Dispose(){if(disposed)return;disposed=true;timer.Dispose();if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}badge.Dispose();}
 }
}
