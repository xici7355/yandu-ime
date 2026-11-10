using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace PersonalIme {
 // Qt's WeChat TSF store contains the composition, not the chat document.
 // Read the focused chat editor through UIA on workers; never block the IME.
 internal sealed class WeChatEditor {
  internal sealed class State {
   public IntPtr Foreground;
   public int[] Identity;
   public string Text;
   public int X,Y;
  }
  static AutomationElement Editor(IntPtr foreground) {
   if(Native.GetForegroundWindow()!=foreground)return null;
   uint pid;Native.GetWindowThreadProcessId(foreground,out pid);
   var name=Process.GetProcessById((int)pid).ProcessName;
   if(name!="Weixin"&&name!="WeChat")return null;
   var focused=AutomationElement.FocusedElement;
   if(focused==null||focused.Current.ProcessId!=pid||focused.Current.ClassName!="mmui::ChatInputField"||focused.Current.IsPassword)return null;
   return focused;
  }
  static bool EndSelection(AutomationElement editor) {
   object p;if(!editor.TryGetCurrentPattern(TextPattern.Pattern,out p))return false;
   var text=(TextPattern)p;var selection=text.GetSelection();
   return selection.Length==1&&selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start,selection[0],TextPatternRangeEndpoint.End)==0&&selection[0].CompareEndpoints(TextPatternRangeEndpoint.End,text.DocumentRange,TextPatternRangeEndpoint.End)==0;
  }
  public static State Read(IntPtr foreground,int[] identity) {
   try {
    var editor=Editor(foreground);if(editor==null)return null;
    var id=editor.GetRuntimeId();if(identity!=null&&!identity.SequenceEqual(id))return null;
    object p;if(!editor.TryGetCurrentPattern(ValuePattern.Pattern,out p)||!EndSelection(editor))return null;
    var value=(ValuePattern)p;if(value.Current.IsReadOnly)return null;
    var text=value.Current.Value??"";if(text.Length>12000)return null;
    var r=editor.Current.BoundingRectangle;
    return new State{Foreground=foreground,Identity=id,Text=text,X=(int)r.Left,Y=(int)r.Bottom};
   }catch{return null;}
  }
  public static bool Replace(State state,string expected,string replacement) {
   try {
    var current=Read(state.Foreground,state.Identity);
    if(current==null||current.Text!=expected)return false;
    var editor=Editor(state.Foreground);if(editor==null||!state.Identity.SequenceEqual(editor.GetRuntimeId())||!EndSelection(editor))return false;
    var value=(ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern);
    // Recheck immediately before the provider write; no clipboard or send keys.
    if(value.Current.Value!=expected||Native.GetForegroundWindow()!=state.Foreground)return false;
    value.SetValue(replacement);
    if(value.Current.Value!=replacement)return false;
    var text=(TextPattern)editor.GetCurrentPattern(TextPattern.Pattern);
    var end=text.DocumentRange;end.MoveEndpointByRange(TextPatternRangeEndpoint.Start,end,TextPatternRangeEndpoint.End);end.Select();
    return true;
   }catch{return false;}
  }
 }
}
