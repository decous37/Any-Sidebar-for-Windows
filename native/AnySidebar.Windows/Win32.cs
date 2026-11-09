using System.Diagnostics;
namespace AnySidebar.Windows;
internal static class Win32 {
 internal const int WM_HOTKEY=0x312;internal const uint MOD_NOREPEAT=0x4000;
 [StructLayout(LayoutKind.Sequential)]internal struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]internal struct Rect {public int Left,Top,Right,Bottom;public int Width=>Right-Left;public int Height=>Bottom-Top;}
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll")]internal static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]internal static extern bool GetCursorPos(out Point point);
 [DllImport("user32.dll")]internal static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]internal static extern short GetKeyState(int key);
 [DllImport("user32.dll",SetLastError=true)]internal static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
 [DllImport("user32.dll")]internal static extern bool UnregisterHotKey(IntPtr window,int id);
 [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]internal static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out Rect rect,int size);
 internal delegate bool EnumWindow(IntPtr window,IntPtr param);
 [DllImport("user32.dll")]internal static extern bool EnumWindows(EnumWindow callback,IntPtr param);
 internal static string Title(IntPtr window){var text=new StringBuilder(1024);GetWindowText(window,text,text.Capacity);return text.ToString();}
 internal static bool Own(IntPtr window){GetWindowThreadProcessId(window,out var id);return id==Environment.ProcessId;}
 internal static Rect Bounds(IntPtr window){if(DwmGetWindowAttribute(window,9,out var rect,Marshal.SizeOf<Rect>())==0&&rect.Width>0&&rect.Height>0)return rect;GetWindowRect(window,out rect);return rect;}
 internal static SourceInfo Source(IntPtr window,string kind){GetWindowThreadProcessId(window,out var pid);string? name=null;try{using var process=Process.GetProcessById((int)pid);name=process.ProcessName;}catch{/* Unknown source metadata is allowed. */}var title=Title(window);return new(){Kind=kind,AppBundleId=name,AppName=name,DocumentTitle=string.IsNullOrWhiteSpace(title)?null:title,DocumentIdentity=$"{pid}:{window.ToInt64()}:{title}"};}
 internal static IntPtr Handle(Window window)=>new WindowInteropHelper(window).EnsureHandle();
 internal static void Exclude(Window window)=>SetWindowDisplayAffinity(Handle(window),0x11);
}
