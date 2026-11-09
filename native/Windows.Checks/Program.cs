using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using AnySidebar.Core;
using AnySidebar.Windows;
using Application=System.Windows.Application;
using Window=System.Windows.Window;
namespace AnySidebar.SystemChecks;
internal sealed class Checks:Application {
 readonly List<object> results=[];readonly string root;int failures;AppController? controller;Process? fixture;
 Checks(string root){this.root=root;ShutdownMode=ShutdownMode.OnExplicitShutdown;}
 [STAThread]static int Main(string[] args){var app=new Checks(args.FirstOrDefault()??Directory.GetCurrentDirectory());if(args.Contains("--fixture")){app.Startup+=(_,_)=>app.ShowFixture();app.Run();return 0;}app.Startup+=async(_,_)=>await app.RunChecks();app.Run();return app.failures==0?0:1;}
 void ShowFixture(){var text=new RichTextBox{IsReadOnly=!Environment.GetCommandLineArgs().Contains("--editable"),Document=new FlowDocument(new Paragraph(new Run(Environment.GetCommandLineArgs().Contains("--editable")?"Editable private-input fixture must never become an external quote.":"Any Sidebar public test fixture. A cache stores completed results."))){FontSize=22},Background=Brushes.AliceBlue,Margin=new Thickness(20)};var window=new Window{Title="Any Sidebar Public Reading Fixture",Width=720,Height=440,Left=40,Top=40,Content=text};window.Loaded+=(_,_)=>{window.Activate();text.Focus();text.SelectAll();};window.Closed+=(_,_)=>Shutdown();window.Show();}
 static void Expect(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
 async Task Wait(Func<Task<bool>> condition,string message,int timeout=15000){var until=Environment.TickCount64+timeout;while(Environment.TickCount64<until){if(await condition().WaitAsync(TimeSpan.FromSeconds(3)))return;await Task.Delay(100);}throw new TimeoutException(message);}
 async Task Check(string name,Func<Task> action){try{await action().WaitAsync(TimeSpan.FromSeconds(30));results.Add(new{name,status="passed"});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,status="failed",error=e.GetType().Name+": "+e.Message});Console.WriteLine("FAIL "+name+" "+e.GetType().Name+": "+e.Message);}}
 async Task RunChecks(){
  var output=Path.Combine(root,"artifacts/windows-native");Directory.CreateDirectory(output);var data=Path.Combine(Path.GetTempPath(),"anysidebar-windows-check-"+Guid.NewGuid());
  try{
   System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
   Console.WriteLine("STAGE create native controller");controller=new AppController(data);Console.WriteLine("STAGE initialize visible WebView2 host");await controller.StartAsync().WaitAsync(TimeSpan.FromSeconds(90));Console.WriteLine("STAGE native host initialized");var app=controller;
   await Check("真实WebView2页面与桥接启动",async()=>{await Wait(async()=>await app.Shell.Web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"收起浮窗\"]')")=="true","页面未加载");Expect(app.Shell.Panel.IsVisible,"侧栏未展开");});
   await Check("页面收起按钮真实调用原生隐藏",async()=>{await app.Shell.Web.ExecuteScriptAsync("document.querySelector('[aria-label=\"收起浮窗\"]').click()");await Wait(()=>Task.FromResult(!app.Shell.Panel.IsVisible),"页面未能收起原生窗口");app.Shell.ShowPanel();});
   await Check("托盘及桌面入口可恢复",()=>{app.Shell.SetCompanion(false);Expect(!app.Shell.Companion.IsVisible,"入口未隐藏");app.RunAction("companion");return Wait(()=>Task.FromResult(app.Shell.Companion.IsVisible),"入口未恢复");});
   await Check("默认全局快捷键实际注册与触发",async()=>{Expect(app.Hotkeys.CaptureState=="registered"&&app.Hotkeys.PanelState=="registered","默认热键未注册");app.Shell.ShowPanel();Keys(0x11,0x12,0x53);await Wait(()=>Task.FromResult(!app.Shell.Panel.IsVisible),"Ctrl+Alt+S未收起");Keys(0x11,0x12,0x53);await Wait(()=>Task.FromResult(app.Shell.Panel.IsVisible),"Ctrl+Alt+S未展开");});
   await Check("快捷键录入捕获来自WebView2的按键",async()=>{
    app.Shell.ShowPanel();await app.Shell.Web.ExecuteScriptAsync("document.querySelector('[aria-label=\"设置\"]').click()");
    await Wait(async()=>await app.Shell.Web.ExecuteScriptAsync("document.querySelectorAll('.shortcut-setting').length===2")=="true","设置页未显示快捷键");
    await app.Shell.Web.ExecuteScriptAsync("document.querySelectorAll('.shortcut-setting')[1].querySelector('button').click()");
    await Wait(async()=>app.Hotkeys.RecordingOperation is not null&&await app.Shell.Web.ExecuteScriptAsync("document.body.innerText.includes('按下快捷键')")=="true","录入状态未同步到WebView2");
    app.Shell.Web.Focus();await Task.Delay(100);Keys(0x11,0x12,0x4b);await Wait(()=>Task.FromResult(app.Hotkeys.Candidate?.KeyCode==0x4b),"WebView2内按键未录入");
    app.Hotkeys.StartRecording("panel",J.Id());await Wait(async()=>await app.Shell.Web.ExecuteScriptAsync("document.body.innerText.includes('按下快捷键')")=="true","取消前录入状态未就绪");Keys(0x1b);await Wait(()=>Task.FromResult(app.Hotkeys.RecordingOperation is null),"Esc未取消录入");
    await app.Shell.Web.ExecuteScriptAsync("document.querySelector('[aria-label=\"返回对话\"]').click()");
   });
   await Check("系统凭据读写清除不落入页面",async()=>{var vault=new CredentialVault();Expect(vault.Read() is null,"测试runner不应有真实凭据");try{vault.Save("public-fixture-placeholder");Expect(vault.Read()=="public-fixture-placeholder","凭据读取不一致");var bootstrap=JsonSerializer.Serialize(await app.BootstrapAsync(),J.Options);Expect(!bootstrap.Contains("public-fixture-placeholder"),"页面暴露凭据");}finally{vault.Clear();}Expect(vault.Read() is null,"凭据未清除");});
   var state=await app.Runtime.ReadStateAsync();await app.Runtime.AcceptDisclosureAsync();await app.Runtime.EnableSelectionAsync(true,state.Preferences.Revision+1);
   await Check("独立阅读进程划词自动加入最新引用",async()=>{
    fixture=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"\""+root+"\" --fixture"){UseShellExecute=false});Expect(fixture is not null,"资料进程未启动");await Wait(async()=>{var s=await app.Runtime.ReadStateAsync();return s.Drafts[s.ActiveTaskId!].ExternalQuotes.Any(q=>q.Text.Contains("cache stores completed results"));},"自动划词未取得只读资料选区",20000);
    var s=await app.Runtime.ReadStateAsync();Expect(s.Drafts[s.ActiveTaskId!].ExternalQuotes.Count==1,"默认模式未保持单条引用");
   });
   await Check("区域读屏二次热键取消无附件",async()=>{app.Shell.ShowPanel();Keys(0x11,0x12,0x41);await Wait(()=>Task.FromResult(Application.Current.Windows.Cast<Window>().Any(w=>w.Title=="Any Sidebar 框选读屏"&&w.IsVisible)),"未进入框选");Keys(0x11,0x12,0x41);await Wait(()=>Task.FromResult(!Application.Current.Windows.Cast<Window>().Any(w=>w.Title=="Any Sidebar 框选读屏"&&w.IsVisible)),"未取消框选");var s=await app.Runtime.ReadStateAsync();Expect(s.Drafts[s.ActiveTaskId!].Images.Count==0,"取消生成了附件");});
   await Check("窗口截图保存真实PNG及原会话归属",async()=>{
    fixture?.Refresh();if(fixture?.MainWindowHandle is {} handle&&handle!=IntPtr.Zero){Native.SetForegroundWindow(handle);await Task.Delay(650);}var before=await app.Runtime.ReadStateAsync();var task=before.ActiveTaskId!;
    await app.ExecuteAsync("startCapture",JsonSerializer.SerializeToElement(new{taskId=task,mode="window"}),J.Id());
    await Wait(()=>Task.FromResult(Application.Current.Windows.Cast<Window>().Any(w=>w.Title=="选择要读取的窗口")),"窗口选择未显示");
    var chooser=Application.Current.Windows.Cast<Window>().First(w=>w.Title=="选择要读取的窗口");var list=Find<ListBox>(chooser);if(list is not null){for(var i=0;i<list.Items.Count;i++)if(list.Items[i].ToString()!.Contains("Public Reading Fixture")){list.SelectedIndex=i;break;}}
    var button=Find<Button>(chooser,b=>b.Content?.ToString()=="截取窗口")??throw new Exception("缺少截取按钮");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    await Wait(async()=>{var s=await app.Runtime.ReadStateAsync();return s.Drafts[task].Images.Count==1;},"窗口截图未加入草稿");var state=await app.Runtime.ReadStateAsync();var image=state.Assets[state.Drafts[task].Images.Single().ImageId];var png=app.Files.Read(image);Expect(image.Width>50&&image.Height>50&&png.Length>100,"截图尺寸或PNG无效");File.WriteAllBytes(Path.Combine(output,"public-fixture-capture.png"),png);Expect(app.Shell.Panel.IsVisible,"完成截图后侧栏未展开");
   });
   await Check("可编辑阅读控件不能被自动采集",async()=>{
    using var editable=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"\""+root+"\" --fixture --editable"){UseShellExecute=false})??throw new Exception("编辑样例进程未启动");
    try{await Wait(async()=>{var bootstrap=JsonSerializer.SerializeToElement(await app.BootstrapAsync(),J.Options);return bootstrap.GetProperty("selectionStatus").GetString()=="excluded";},"未排除可编辑选区");var s=await app.Runtime.ReadStateAsync();Expect(s.Drafts[s.ActiveTaskId!].ExternalQuotes.All(q=>!q.Text.Contains("Editable private-input")),"可编辑输入进入了引用");}finally{editable.CloseMainWindow();if(!editable.WaitForExit(2000))editable.Kill(true);}
   });
   await Check("退出准备保存页面草稿",async()=>{app.Shell.ShowPanel();await app.Shell.Web.ExecuteScriptAsync("(()=>{const e=document.querySelector('textarea');const setter=Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set;setter.call(e,'退出前的公开测试草稿');e.dispatchEvent(new Event('input',{bubbles:true}));})()");Expect(await app.PrepareQuitAsync(),"退出保存握手失败");var s=await app.Runtime.ReadStateAsync();Expect(s.Drafts[s.ActiveTaskId!].Prompt=="退出前的公开测试草稿","未保存最后输入");});
  }catch(Exception e){failures++;results.Add(new{name="启动/环境",status="failed",error=e.GetType().Name+": "+e.Message});Console.WriteLine(e.GetType().Name+": "+e.Message);}
  finally{try{if(fixture is not null&&!fixture.HasExited){fixture.CloseMainWindow();if(!fixture.WaitForExit(3000))fixture.Kill(true);}}catch{}try{if(controller is not null)await controller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));}catch(Exception e){failures++;results.Add(new{name="关闭释放",status="failed",error=e.Message});}File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(new{environment=Environment.OSVersion.ToString(),architecture=RuntimeInformation.ProcessArchitecture.ToString(),results,failures},new JsonSerializerOptions{WriteIndented=true}));try{Directory.Delete(data,true);}catch{}Shutdown();}
 }
 static T? Find<T>(DependencyObject root,Func<T,bool>? predicate=null)where T:DependencyObject{if(root is T value&&(predicate is null||predicate(value)))return value;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){var match=Find(child,predicate);if(match is not null)return match;}return null;}
 static void Keys(params ushort[] keys){var inputs=keys.Select(k=>Native.Input.Keyboard(k,false)).Concat(keys.Reverse().Select(k=>Native.Input.Keyboard(k,true))).ToArray();var sent=Native.SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Native.Input>());if(sent!=inputs.Length)throw new Exception("系统输入未全部投递");}
 static class Native {
  [StructLayout(LayoutKind.Sequential)]internal struct Input{public uint Type;public Union Data;public static Input Keyboard(ushort key,bool up)=>new(){Type=1,Data=new Union{Key=new KeyInput{Vk=key,Flags=up?2u:0u}}};}
  [StructLayout(LayoutKind.Explicit)]internal struct Union{[FieldOffset(0)]public KeyInput Key;[FieldOffset(0)]public MouseInput Mouse;}
  [StructLayout(LayoutKind.Sequential)]internal struct KeyInput{public ushort Vk,Scan;public uint Flags,Time;public IntPtr Extra;}
  [StructLayout(LayoutKind.Sequential)]internal struct MouseInput{public int X,Y;public uint Data,Flags,Time;public IntPtr Extra;}
  [DllImport("user32.dll",SetLastError=true)]internal static extern uint SendInput(uint count,Input[] inputs,int size);
  [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr window);
 }
}
