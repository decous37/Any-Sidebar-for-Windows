using System.Windows.Automation;
using System.Diagnostics;
namespace AnySidebar.Windows;
internal sealed class SelectionService:IDisposable {
 readonly ReadingRuntime runtime;readonly Thread worker;readonly CancellationTokenSource stop=new();volatile bool enabled;string status="disabled";IntPtr sourceWindow;string lease=J.Id(),token=J.Id();string? quoteId,lastText;long sourceSequence;long routeEpoch=-1;string? taskId;
 internal event Action<string>? StatusChanged;
 internal SelectionService(ReadingRuntime runtime){this.runtime=runtime;worker=new Thread(Loop){IsBackground=true,Name="AnySidebar selection reader"};worker.SetApartmentState(ApartmentState.STA);worker.Start();}
 internal void Configure(bool enabled){this.enabled=enabled;if(!enabled)SetStatus("disabled");}
 void SetStatus(string next){if(status==next)return;status=next;StatusChanged?.Invoke(next);}
 void Loop(){while(!stop.IsCancellationRequested){try{ReadSelection();}catch{SetStatus("unsupported");}stop.Token.WaitHandle.WaitOne(250);}}
 void ReadSelection(){
  if(!enabled)return;var window=Win32.GetForegroundWindow();if(window==IntPtr.Zero||Win32.Own(window)||(Win32.GetAsyncKeyState(0x01)&0x8000)!=0)return;
  var route=runtime.RouteAsync().GetAwaiter().GetResult();if(route.TaskId is null)return;
  if(sourceWindow!=window||routeEpoch!=route.Epoch||taskId!=route.TaskId){sourceWindow=window;routeEpoch=route.Epoch;taskId=route.TaskId;lease=J.Id();token=J.Id();quoteId=null;lastText=null;sourceSequence=0;}
  Win32.GetWindowThreadProcessId(window,out var pid);var element=AutomationElement.FocusedElement;var watch=Stopwatch.StartNew();TextPattern? pattern=null;
  for(var depth=0;element is not null&&depth<8&&watch.ElapsedMilliseconds<250;depth++,element=TreeWalker.ControlViewWalker.GetParent(element)){
   var current=element.Current;if(current.ProcessId!=(int)pid||current.IsPassword){SetStatus("excluded");return;}
   if(element.TryGetCurrentPattern(TextPattern.Pattern,out var raw)){
    var candidate=(TextPattern)raw;var readOnly=candidate.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
    if(!Win32.IsDocumentEditor(window,current.ClassName)&&(readOnly is false||(element.TryGetCurrentPattern(ValuePattern.Pattern,out var valuePattern)&&!((ValuePattern)valuePattern).Current.IsReadOnly)||(readOnly is not true&&current.ControlType!=ControlType.Document))){SetStatus("excluded");return;}
    pattern=candidate;break;
   }
   if(current.ControlType==ControlType.Edit){SetStatus("excluded");return;}
  }
  if(pattern is null){SetStatus("unsupported");return;}
  var ranges=pattern.GetSelection();var text=string.Join("\n",ranges.Select(x=>x.GetText(2*1024*1024))).Trim();
  if(Win32.GetForegroundWindow()!=window||(Win32.GetAsyncKeyState(0x01)&0x8000)!=0)return;
  if(text==lastText)return;var latestRoute=runtime.RouteAsync().GetAwaiter().GetResult();if(latestRoute.TaskId!=taskId||latestRoute.Epoch!=routeEpoch)return;
  var previous=lastText;lastText=text;SetStatus("ready");
  if(text.Length==0){if(previous is null||quoteId is null)return;runtime.IngestSelectionAsync(new(taskId!,routeEpoch,lease,++sourceSequence,token,quoteId,Win32.Source(window,"external-text"),null)).GetAwaiter().GetResult();quoteId=null;return;}
  token=J.Id();quoteId=runtime.IngestSelectionAsync(new(taskId!,routeEpoch,lease,++sourceSequence,token,null,Win32.Source(window,"external-text"),text)).GetAwaiter().GetResult();
 }
 public void Dispose(){enabled=false;stop.Cancel();worker.Join(750);/* A blocked provider stays on the single background thread, never blocks app exit or creates unbounded workers. */}
}
