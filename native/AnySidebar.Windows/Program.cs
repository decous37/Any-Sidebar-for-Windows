using System.Security.Principal;
namespace AnySidebar.Windows;
internal sealed class Program:Application {
 AppController? controller;Mutex? instance;EventWaitHandle? signal;CancellationTokenSource stop=new();bool shuttingDown;
 [STAThread]public static void Main(){var app=new Program{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Run();}
 protected override async void OnStartup(StartupEventArgs e){base.OnStartup(e);try{
  var sid=WindowsIdentity.GetCurrent().User?.Value??Environment.UserName;var name="Local\\AnySidebarForWindows-"+sid;
  instance=new Mutex(true,name,out var first);if(!first){try{using var existing=EventWaitHandle.OpenExisting(name+"-show");existing.Set();}catch{/* Existing process may still be initializing. */}Shutdown();return;}
  signal=new EventWaitHandle(false,EventResetMode.AutoReset,name+"-show");var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AnySidebarForWindows");controller=new(directory);await controller.StartAsync();
  _=Task.Run(()=>{while(!stop.IsCancellationRequested){try{if(signal.WaitOne(500))Dispatcher.BeginInvoke(new Action(()=>controller?.Shell.ShowPanel()));}catch(ObjectDisposedException){break;}}});
 }catch(Exception error){var text=error is AppFault?error.Message:"应用无法启动。请确认已安装 Microsoft Edge WebView2 Runtime，并检查应用文件是否完整。";MessageBox.Show(text+"\n\nWebView2 官方下载：https://developer.microsoft.com/microsoft-edge/webview2/", "Any Sidebar",MessageBoxButton.OK,MessageBoxImage.Warning);Shutdown();}}
 internal async void RequestQuit(){if(shuttingDown)return;shuttingDown=true;try{if(controller is not null&&!await controller.PrepareQuitAsync()){shuttingDown=false;return;}stop.Cancel();if(controller is not null)await controller.DisposeAsync();controller=null;Shutdown();}catch{shuttingDown=false;MessageBox.Show("部分资料尚未保存，请稍后重试退出。","Any Sidebar");}}
 protected override void OnSessionEnding(SessionEndingCancelEventArgs e){e.Cancel=true;RequestQuit();}
 protected override void OnExit(ExitEventArgs e){stop.Cancel();signal?.Dispose();try{instance?.ReleaseMutex();}catch(ApplicationException){/* A second instance never owned the mutex. */}instance?.Dispose();base.OnExit(e);}
}
