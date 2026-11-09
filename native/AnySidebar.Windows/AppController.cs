using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Threading;
namespace AnySidebar.Windows;
internal sealed class AppController:IAsyncDisposable {
 internal string Directory{get;}internal string Resources{get;}internal ReadingRuntime Runtime{get;}internal AttachmentStore Files{get;}internal DesktopShell Shell{get;}internal HotkeyService Hotkeys{get;}internal WebBridge Bridge{get;}
 readonly CredentialVault vault;readonly DeepSeekTransport transport;readonly SelectionService selection;readonly CaptureService capture=new();readonly DispatcherTimer foregroundTimer;long systemRevision;string selectionStatus="disabled";AppFault? fault,captureError;string? captureId;CancellationTokenSource? captureCancel;Task? captureWork;IntPtr lastExternal;bool quitting;TaskCompletionSource<bool>? quitReady;
 internal AppController(string directory){
  Directory=directory;System.IO.Directory.CreateDirectory(directory);Resources=Path.Combine(AppContext.BaseDirectory,"Resources");var catalog=JsonSerializer.Deserialize<ModelCatalog>(File.ReadAllText(Path.Combine(Resources,"contracts","model-capabilities.json")),J.Options)??throw new AppFault("RESOURCE_MISSING","模型配置缺失。");
  vault=new();transport=new(vault);Files=new(directory);Runtime=new(new SqliteRepository(directory),Files,transport,catalog,File.ReadAllText(Path.Combine(Resources,"contracts","desktop-reading-prompt.txt")));
  Shell=new(directory,RunAction);Hotkeys=new(Shell.Companion);Bridge=new(this);selection=new(Runtime);
  Runtime.Changed+=type=>{Bridge.Notify(type);Application.Current.Dispatcher.BeginInvoke(new Action(async()=>{try{await RefreshAsync();}catch(AppFault error){SetFault(error);}}));};
  selection.StatusChanged+=status=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>{selectionStatus=status;SystemChanged("permissions-changed");}));
  Hotkeys.Triggered+=kind=>RunAction(kind=="capture"?"capture":"toggle");Hotkeys.Changed+=()=>SystemChanged("shortcut-recording-changed");
  foregroundTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};foregroundTimer.Tick+=(_,_)=>{var window=Win32.GetForegroundWindow();if(window!=IntPtr.Zero&&!Win32.Own(window))lastExternal=window;};foregroundTimer.Start();
 }
 internal async Task StartAsync(){var state=await Runtime.ReadStateAsync();Hotkeys.Restore(state.Preferences);Shell.SetCompanion(state.Preferences.CompanionVisible);Shell.ShowPanel();await Bridge.InitializeAsync();await RefreshAsync();Shell.ShowPanel();}
 internal void SetFault(AppFault value){fault=value;SystemChanged("permissions-changed");}
 void SystemChanged(string type){systemRevision++;Bridge.Notify(type);}
 async Task RefreshAsync(){if(quitting)return;var state=await Runtime.ReadStateAsync();Shell.ApplyTheme(state.Preferences.Theme);selection.Configure(state.Preferences.AutoSelectionEnabled&&state.Preferences.DisclosureVersion=="v1");if(state.ActiveTaskId is {} id&&state.Drafts.TryGetValue(id,out var draft))Shell.Reference(draft.ExternalQuotes.Count+draft.Images.Count>0||draft.AssistantQuote is not null);}
 object CredentialStatus(){try{return new{state="known",configured=!string.IsNullOrWhiteSpace(vault.Read())};}catch(AppFault error){return new{state="unknown",error=error.View()};}}
 internal async Task<object> BootstrapAsync()=>new{runtime=await Runtime.ViewAsync(),systemRevision,permissions=new{accessibility=true,screen=true},credential=CredentialStatus(),shortcutState=Hotkeys.CaptureState,panelShortcutState=Hotkeys.PanelState,recordingKind=Hotkeys.RecordingKind,recordingOperationId=Hotkeys.RecordingOperation,recordedShortcut=Hotkeys.Candidate,captureOperationId=captureId,captureError=captureError?.View(),selectionStatus,appFault=fault?.View()};
 internal async Task<object?> ExecuteAsync(string command,JsonElement args,string operationId){
  string Str(string name)=>args.GetProperty(name).GetString()!;int Int(string name)=>args.GetProperty(name).GetInt32();
  var reads=new[]{"bootstrap","listTasks","getTaskView","getOperation","getCredentialStatus"};var write=!reads.Contains(command);
  if(command=="readyToQuit"){quitReady?.TrySetResult(args.GetProperty("success").GetBoolean());return new{};}
  if(command=="getOperation")return await Runtime.OperationAsync(Str("sourceOperationId"));
  if(command=="getTaskView")return new{runtime=await Runtime.ViewAsync(Str("taskId"),args.TryGetProperty("cursor",out var cursor)?cursor.GetInt32():null)};
  if(command=="listTasks")return(await Runtime.ReadStateAsync()).Tasks.Values.OrderByDescending(t=>t.UpdatedAt).ToArray();
  if(write){var digest=command=="setKey"?"sensitive":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(args.GetRawText())));if(!await Runtime.ReserveAsync(operationId,command,digest))return await BootstrapAsync();}
  try{string? result=null;
   switch(command){
    case "bootstrap":case "getCredentialStatus":break;
    case "createTask":result=await Runtime.CreateTaskAsync(Str("title"));break;
    case "switchTask":await Runtime.SwitchTaskAsync(Str("taskId"));break;
    case "renameTask":await Runtime.RenameTaskAsync(Str("taskId"),Str("title"),Int("expectedRevision"));break;
    case "changeDraft":await Runtime.ChangeDraftAsync(Str("taskId"),Int("expectedRevision"),args.GetProperty("change"));if(args.GetProperty("change").GetProperty("kind").GetString()=="removeImage")await Runtime.CollectAsync();break;
    case "send":result=await Runtime.SendAsync(Str("taskId"),Int("expectedDraftRevision"),Int("expectedPreferencesRevision"),operationId);break;
    case "retry":case "regenerate":result=await Runtime.SendAsync(Str("taskId"),0,Int("expectedPreferencesRevision"),operationId,command,Str("requestId"),Str("turnId"));break;
    case "stop":var target=args.GetProperty("target");var kind=target.GetProperty("kind").GetString();var taskId=target.GetProperty("taskId").GetString()!;if(!J.IsId(taskId))throw new AppFault("INVALID_ARGUMENT","停止目标无效。");if(kind=="request"&&target.EnumerateObject().Count()==3&&J.IsId(target.GetProperty("requestId").GetString()))await Runtime.StopAsync(taskId,target.GetProperty("requestId").GetString(),null);else if(kind=="operation"&&target.EnumerateObject().Count()==3&&J.IsId(target.GetProperty("sourceOperationId").GetString()))await Runtime.StopAsync(taskId,null,target.GetProperty("sourceOperationId").GetString());else throw new AppFault("INVALID_ARGUMENT","停止目标无效。");break;
    case "restoreSubmission":await Runtime.RestoreSubmissionAsync(Str("taskId"),Str("requestId"),Int("expectedDraftRevision"));break;
    case "savePreferences":
     var patch=args.GetProperty("patch");var fields=patch.EnumerateObject().ToArray();var shortcut=fields.FirstOrDefault(p=>p.Name is "captureShortcut" or "panelShortcut");
     if(shortcut.Name is not null){if(fields.Length!=1)throw new AppFault("INVALID_ARGUMENT","请单独设置快捷键。");var value=shortcut.Value.ValueKind==JsonValueKind.Null?null:J.Read<Shortcut>(shortcut.Value);var shortcutKind=shortcut.Name=="panelShortcut"?"panel":"capture";await Hotkeys.ReplaceAsync(shortcutKind,value,()=>Runtime.SetShortcutAsync(shortcutKind,value,Int("expectedRevision")));}else await Runtime.SavePreferencesAsync(Int("expectedRevision"),patch);break;
    case "setKey":vault.Save(Str("key"));SystemChanged("credential-changed");break;
    case "clearKey":vault.Clear();SystemChanged("credential-changed");break;
    case "acceptDisclosure":await Runtime.AcceptDisclosureAsync();break;
    case "setAutoSelectionEnabled":await Runtime.EnableSelectionAsync(args.GetProperty("enabled").GetBoolean(),Int("expectedRevision"));break;
    case "requestPermission":SystemChanged("permissions-changed");break;
    case "startCapture":await StartCaptureAsync(Str("taskId"),Str("mode"),operationId);break;
    case "cancelCapture":if(captureId==Str("captureOperationId")){captureCancel?.Cancel();capture.Cancel();}break;
    case "startShortcutRecording":Shell.ShowPanel();Hotkeys.StartRecording(args.TryGetProperty("kind",out var recordKind)?recordKind.GetString()!:"capture",operationId);Shell.Web.Focus();break;
    case "recordShortcut":Hotkeys.AcceptCandidate(Str("recordingOperationId"),J.Read<Shortcut>(args.GetProperty("binding")));break;
    case "cancelShortcutRecording":if(Hotkeys.RecordingOperation==Str("recordingOperationId"))Hotkeys.CancelRecording();break;
    case "deleteTask":await Runtime.DeleteTaskAsync(Str("taskId"),Int("expectedRevision"));break;
    case "clearAllTasks":await Runtime.ClearAllAsync();break;
    case "setCompanionVisible":var visible=args.GetProperty("visible").GetBoolean();await Runtime.SetCompanionAsync(visible);Shell.SetCompanion(visible);break;
    case "copyText":try{System.Windows.Clipboard.SetText(Str("text"));}catch{throw new AppFault("COPY_FAILED","复制失败，请稍后重试。",true);}break;
    case "hidePanel":Shell.Panel.Hide();break;
    default:throw new AppFault("INVALID_ARGUMENT","未知操作。");
   }
   if(write)await Runtime.FinishOperationAsync(operationId,result);await RefreshAsync();return await BootstrapAsync();
  }catch(Exception error){var mapped=error as AppFault??(error is OperationCanceledException?new AppFault("REQUEST_STOPPED","操作已取消。"):new AppFault("OPERATION_FAILED","操作未完成，资料保留。请稍后重试。",true));if(write)await Runtime.FinishOperationAsync(operationId,error:mapped);throw mapped;}
 }
 async Task StartCaptureAsync(string taskId,string mode,string operationId){if(captureId is not null)throw new AppFault("CAPTURE_BUSY","已有截图操作正在进行。");var state=await Runtime.ReadStateAsync();if(state.Preferences.DisclosureVersion!="v1")throw new AppFault("CONSENT_REQUIRED","请先在设置中确认本机采集说明。");if(!state.Tasks.ContainsKey(taskId))throw new AppFault("TASK_MISSING","原会话已删除。");var fg=Win32.GetForegroundWindow();var original=fg!=IntPtr.Zero&&!Win32.Own(fg)?fg:lastExternal;captureId=operationId;captureError=null;captureCancel=new();var token=captureCancel.Token;var restore=Shell.Panel.IsVisible;Shell.Panel.Hide();Shell.Companion.Hide();SystemChanged("capture-changed");captureWork=CompleteCaptureAsync(taskId,mode,original,restore,token);}
 async Task CompleteCaptureAsync(string taskId,string mode,IntPtr source,bool restore,CancellationToken cancel){ImageAsset? asset=null;try{
  var result=await capture.CaptureAsync(mode,source,cancel);cancel.ThrowIfCancellationRequested();asset=await Task.Run(()=>Files.Persist(result.Png,result.Source,result.Thumbnail),cancel);cancel.ThrowIfCancellationRequested();await Runtime.AttachAsync(taskId,asset,cancel);asset=null;if(!quitting)Shell.ShowPanel();
 }catch(Exception error){if(asset is not null)Files.Delete(asset);captureError=error is OperationCanceledException?new("CAPTURE_CANCELLED","已取消截图。"):error as AppFault??new("CAPTURE_FAILED","截图失败，当前窗口可能受保护或不可见。可换用区域读屏。",true);if(!quitting&&(restore||captureError.Code!="CAPTURE_CANCELLED"))Shell.ShowPanel();}
 finally{captureId=null;captureCancel?.Dispose();captureCancel=null;if(!quitting)Shell.SetCompanion((await Runtime.ReadStateAsync()).Preferences.CompanionVisible);SystemChanged("capture-changed");}}
 internal void RunAction(string command){Application.Current.Dispatcher.BeginInvoke(new Action(async()=>{try{
  switch(command){case "toggle":if(captureId is null)Shell.Toggle();break;case "capture":case "capture-window":if(captureId is not null){captureCancel?.Cancel();capture.Cancel();}else{var route=await Runtime.RouteAsync();if(route.TaskId is not null)await StartCaptureAsync(route.TaskId,command=="capture"?"region":"window",J.Id());}break;case "settings":Shell.ShowPanel();Bridge.Notify("open-settings");break;case "companion":var visible=!Shell.Companion.IsVisible;await Runtime.SetCompanionAsync(visible);Shell.SetCompanion(visible);break;case "quit":((Program)Application.Current).RequestQuit();break;}
 }catch(Exception e){SetFault(e as AppFault??new("OPERATION_FAILED","操作未完成，请重试。",true));Shell.ShowPanel();}}));}
 internal void OpenExternal(string url,bool userInitiated){if(userInitiated&&Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http")try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch{SetFault(new("LINK_FAILED","无法打开系统浏览器。",true));}}
 internal async Task<bool> PrepareQuitAsync(){if(Shell.Web.CoreWebView2 is null)return true;quitReady=new(TaskCreationOptions.RunContinuationsAsynchronously);Shell.Web.CoreWebView2.PostWebMessageAsJson("{\"type\":\"will-quit\"}");try{return await quitReady.Task.WaitAsync(TimeSpan.FromSeconds(3));}catch(TimeoutException){SetFault(new("SAVE_PENDING","输入尚未确认保存，请稍后再退出。",true));Shell.ShowPanel();return false;}finally{quitReady=null;}}
 public async ValueTask DisposeAsync(){quitting=true;foregroundTimer.Stop();selection.Dispose();Hotkeys.Dispose();captureCancel?.Cancel();capture.Cancel();if(captureWork is not null)await captureWork;await Runtime.DisposeAsync();transport.Dispose();Shell.Dispose();}
}
