using System.Text;
using System.Text.Json;
namespace AnySidebar.Core;

public sealed class ReadingRuntime:IAsyncDisposable {
 readonly IRepository repository;readonly IAttachments files;readonly ITransport transport;readonly ContextBuilder builder;readonly SemaphoreSlim gate=new(1,1);
 readonly Dictionary<string,(CancellationTokenSource Cancel,Task Work)> jobs=[];readonly Dictionary<string,CancellationTokenSource> preparing=[];
 readonly Dictionary<string,(string Lease,long Sequence,string Token,string Quote)> selections=[];
 State state;long sequence;long routeEpoch;bool closing;
 public string BootId{get;}=J.Id();public ModelCatalog Catalog{get;}public event Action<string>? Changed;
 public ReadingRuntime(IRepository repository,IAttachments files,ITransport transport,ModelCatalog catalog,string prompt){
  this.repository=repository;this.files=files;this.transport=transport;Catalog=catalog;builder=new(prompt);state=repository.Load();
  if(state.SchemaVersion!=1)throw new AppFault("DATABASE_VERSION","本机数据由其他版本创建，请保留原文件并更新应用。");
  foreach(var g in state.Generations.Values.Where(g=>g.Active)){g.Status="interrupted";g.Error=new(){Code="NETWORK_ERROR",Message="上次运行中断，未自动重发。",Retryable=true};if(g.RestoresRequestId is {} old&&state.Turns.TryGetValue(g.TurnId,out var t))t.DisplayedRequestId=old;}
  foreach(var p in state.Preparations.Values.Where(p=>p.State=="preparing"))p.State="interrupted";
  foreach(var task in state.Tasks.Values){task.ActiveOperationId=null;task.ActiveRequestId=null;}
  foreach(var op in state.Operations.Values.Where(op=>op.State=="pending")){op.State="failed";op.Error=new(){Code="NETWORK_ERROR",Message="上次操作已中断。",Retryable=true};}
  if(state.Tasks.Count==0)AddTask(state,"阅读任务");if(state.ActiveTaskId is null||!state.Tasks.ContainsKey(state.ActiveTaskId))state.ActiveTaskId=state.Tasks.Keys.First();
  repository.Save(state);
 }
 void Notify(string name){try{Changed?.Invoke(name);}catch{/* UI delivery cannot invalidate an already committed transaction. */}}
 void Commit(State next,string type="runtime-changed"){try{repository.Save(next);}catch(AppFault){throw;}catch{throw AppFault.Storage();}state=next;sequence++;Notify(type);}
 static ReadingTask TaskFor(State s,string id)=>s.Tasks.GetValueOrDefault(id)??throw new AppFault("TASK_MISSING","此会话已删除。");
 static Draft DraftFor(State s,string id){TaskFor(s,id);return s.Drafts[id];}
 static void Check(int expected,int actual){if(expected!=actual)throw new AppFault("REVISION_CONFLICT","资料已更新，请重试。",true,actual);}
 static string AddTask(State s,string title){var t=new ReadingTask{Title=title};s.Tasks[t.Id]=t;s.Drafts[t.Id]=new(){TaskId=t.Id};s.ActiveTaskId=t.Id;return t.Id;}
 static string Title(string title){title=title.Trim();if(title.Length is <1 or >120)throw new AppFault("INVALID_ARGUMENT","会话名称须为 1–120 个字符。");return title;}
 async Task Mutate(Action<State> change,string type="runtime-changed"){await gate.WaitAsync();try{var next=J.Clone(state);change(next);Commit(next,type);}finally{gate.Release();}}
 public async Task<State> ReadStateAsync(){await gate.WaitAsync();try{return J.Clone(state);}finally{gate.Release();}}
 public async Task<(string? TaskId,long Epoch)> RouteAsync(){await gate.WaitAsync();try{return(state.ActiveTaskId,routeEpoch);}finally{gate.Release();}}
 public async Task<Operation?> OperationAsync(string id){await gate.WaitAsync();try{return state.Operations.TryGetValue(id,out var op)?J.Clone(op):null;}finally{gate.Release();}}
 public async Task<bool> ReserveAsync(string id,string command,string digest){await gate.WaitAsync();try{
  if(state.Operations.TryGetValue(id,out var previous)){if(previous.Command!=command||previous.Digest!=digest)throw new AppFault("INVALID_ARGUMENT","操作标识已用于其他内容。");if(previous.Error is {} e)throw new AppFault(e.Code,e.Message,e.Retryable);return false;}
  var next=J.Clone(state);next.Operations[id]=new(){Id=id,Command=command,Digest=digest};Commit(next);return true;
 }finally{gate.Release();}}
 public Task FinishOperationAsync(string id,string? result=null,Exception? error=null)=>Mutate(s=>{if(s.Operations.TryGetValue(id,out var op)){op.State=error is null?"succeeded":"failed";op.ResultId=result;op.Error=error is null?null:FaultRecord.From(error);}});
 public async Task<string> CreateTaskAsync(string title){string id="";await Mutate(s=>{id=AddTask(s,Title(title));routeEpoch++;});return id;}
 public Task SwitchTaskAsync(string id)=>Mutate(s=>{TaskFor(s,id);s.ActiveTaskId=id;routeEpoch++;});
 public Task RenameTaskAsync(string id,string title,int revision)=>Mutate(s=>{var t=TaskFor(s,id);Check(revision,t.Revision);t.Title=Title(title);t.Revision++;t.UpdatedAt=J.Now();});
 public Task ChangeDraftAsync(string id,int expected,JsonElement change)=>Mutate(s=>{
  var d=DraftFor(s,id);Check(expected,d.Revision);var kind=change.GetProperty("kind").GetString();
  switch(kind){
   case "prompt":var text=change.GetProperty("text").GetString()??"";if(Encoding.UTF8.GetByteCount(text)>2*1024*1024)throw new AppFault("INVALID_ARGUMENT","输入过长。");d.Prompt=text;d.PromptRevision++;break;
   case "removeQuote":d.ExternalQuotes.RemoveAll(q=>q.Id==change.GetProperty("id").GetString());break;
   case "removeImage":d.Images.RemoveAll(x=>x.Id==change.GetProperty("id").GetString());break;
   case "clearAssistantQuote":if(d.AssistantQuote?.Id==change.GetProperty("id").GetString())d.AssistantQuote=null;break;
   case "assistantQuote":
    var turnId=change.GetProperty("turnId").GetString()!;var requestId=change.GetProperty("requestId").GetString()!;var selected=change.GetProperty("text").GetString()??"";
    if(!s.Turns.TryGetValue(turnId,out var turn)||turn.TaskId!=id||!s.Generations.TryGetValue(requestId,out var g)||g.TurnId!=turnId||g.TaskId!=id||string.IsNullOrWhiteSpace(selected)||!g.Answer.Contains(selected,StringComparison.Ordinal))throw new AppFault("QUOTE_SOURCE_MISMATCH","引用内容不属于此回答。");
    d.AssistantQuote=new(){SourceTurnId=turnId,SourceRequestId=requestId,Text=selected};break;
   default:throw new AppFault("INVALID_ARGUMENT","无法识别的草稿修改。");
  }d.Revision++;d.UpdatedAt=J.Now();
 });
 public Task RestoreSubmissionAsync(string taskId,string requestId,int expected)=>Mutate(s=>{var d=DraftFor(s,taskId);Check(expected,d.Revision);if(!s.Snapshots.TryGetValue(requestId,out var snap)||snap.TaskId!=taskId)throw new AppFault("REQUEST_MISSING","原问题不可用。");d.Prompt=snap.Submission.Prompt;d.PromptRevision++;d.Revision++;});
 public Task SavePreferencesAsync(int expected,JsonElement patch)=>Mutate(s=>{
  var p=s.Preferences;Check(expected,p.Revision);foreach(var property in patch.EnumerateObject()){
   switch(property.Name){
    case "modelId":p.ModelId=property.Value.GetString()!;Catalog.Find(p.ModelId);break;
    case "thinkingEnabled":p.ThinkingEnabled=property.Value.GetBoolean();break;
    case "reasoningEffort":p.ReasoningEffort=property.Value.GetString()!;if(p.ReasoningEffort is not("low" or "high" or "max"))throw new AppFault("INVALID_ARGUMENT","无效思考强度。");break;
    case "theme":p.Theme=property.Value.GetString()!;if(p.Theme is not("system" or "light" or "dark"))throw new AppFault("INVALID_ARGUMENT","无效主题。");break;
    case "fontSize":p.FontSize=property.Value.GetInt32();if(p.FontSize is not(12 or 13 or 14 or 16))throw new AppFault("INVALID_ARGUMENT","无效字号。");break;
    case "quoteMode":p.QuoteMode=property.Value.GetString()!;if(p.QuoteMode is not("latest" or "accumulate"))throw new AppFault("INVALID_ARGUMENT","无效引用模式。");break;
    default:throw new AppFault("INVALID_ARGUMENT","此设置不能通过该接口修改。");
   }
  }Catalog.Resolve(p);p.Revision++;p.UpdatedAt=J.Now();
 });
 public Task SetShortcutAsync(string kind,Shortcut? shortcut,int expected)=>Mutate(s=>{Check(expected,s.Preferences.Revision);if(kind=="panel")s.Preferences.PanelShortcut=shortcut;else if(kind=="capture")s.Preferences.CaptureShortcut=shortcut;else throw new AppFault("INVALID_ARGUMENT","无效快捷键类型。");s.Preferences.Revision++;});
 public Task AcceptDisclosureAsync()=>Mutate(s=>{s.Preferences.DisclosureVersion="v1";s.Preferences.Revision++;});
 public Task EnableSelectionAsync(bool enabled,int expected)=>Mutate(s=>{Check(expected,s.Preferences.Revision);if(enabled&&s.Preferences.DisclosureVersion!="v1")throw new AppFault("CONSENT_REQUIRED","请先确认本机采集说明。");s.Preferences.AutoSelectionEnabled=enabled;s.Preferences.Revision++;});
 public Task SetCompanionAsync(bool visible)=>Mutate(s=>{s.Preferences.CompanionVisible=visible;});
 public async Task<string?> IngestSelectionAsync(SelectionEvent e){await gate.WaitAsync();try{
  if(closing||!state.Preferences.AutoSelectionEnabled||state.Preferences.DisclosureVersion!="v1"||state.ActiveTaskId!=e.TaskId||routeEpoch!=e.RouteEpoch||!state.Drafts.ContainsKey(e.TaskId))return null;
  if(selections.TryGetValue(e.TaskId,out var old)&&old.Lease==e.Lease&&old.Sequence>=e.Sequence)return old.Quote;
  var next=J.Clone(state);var d=next.Drafts[e.TaskId];
  if(!string.IsNullOrWhiteSpace(e.Text)){
   if(Encoding.UTF8.GetByteCount(e.Text)>2*1024*1024)return null;
   if(next.Preferences.QuoteMode=="accumulate"&&d.ExternalQuotes.LastOrDefault() is {} last&&last.Text==e.Text&&last.Source.DocumentIdentity==e.Source.DocumentIdentity)return last.Id;
   var q=new ExternalQuote{Source=J.Clone(e.Source),Text=e.Text,SourceLeaseId=e.Lease,SourceSequence=e.Sequence,SelectionToken=e.Token};
   if(next.Preferences.QuoteMode=="latest")d.ExternalQuotes=[q];else d.ExternalQuotes.Add(q);
   d.Revision++;d.UpdatedAt=J.Now();Commit(next);selections[e.TaskId]=(e.Lease,e.Sequence,e.Token,q.Id);return q.Id;
  }
  if(next.Preferences.QuoteMode!="latest"||!selections.TryGetValue(e.TaskId,out old)||old.Lease!=e.Lease||old.Token!=e.Token||old.Quote!=e.ExpectedQuoteId)return null;
  var removed=d.ExternalQuotes.RemoveAll(q=>q.Id==old.Quote&&q.SelectionToken==e.Token&&q.SourceLeaseId==e.Lease);if(removed>0){d.Revision++;Commit(next);}selections.Remove(e.TaskId);return null;
 }finally{gate.Release();}}
 public async Task AttachAsync(string taskId,ImageAsset asset){try{await Mutate(s=>{var d=DraftFor(s,taskId);if(d.Images.Count>=Catalog.AppLimits.MaxNewImages)throw new AppFault("IMAGE_LIMIT","每次最多新增 9 张截图。");if(asset.ByteLength>Catalog.AppLimits.MaxImageBytes)throw new AppFault("IMAGE_LIMIT","单张截图最多 10 MiB。");s.Assets[asset.Id]=asset;d.Images.Add(new(){ImageId=asset.Id});d.Revision++;d.UpdatedAt=J.Now();});}catch{files.Delete(asset);throw;}}
 public async Task<string> SendAsync(string taskId,int expectedDraft,int expectedPreferences,string operationId,string kind="send",string? sourceRequestId=null,string? turnId=null){
  Preparation prep;State frozen;CancellationTokenSource cts;
  await gate.WaitAsync();try{
   if(closing)throw new AppFault("APP_EXITING","应用正在退出。");var task=TaskFor(state,taskId);if(task.ActiveOperationId is not null)throw new AppFault("REQUEST_BUSY","此会话已有请求正在进行。");Check(expectedPreferences,state.Preferences.Revision);
   var next=J.Clone(state);Submission submission;List<FrozenMessage> messages;
   if(kind=="send"){
    var draft=DraftFor(next,taskId);Check(expectedDraft,draft.Revision);if(string.IsNullOrWhiteSpace(draft.Prompt)&&draft.Images.Count==0)throw new AppFault("EMPTY_QUESTION","请输入问题或加入图片。");submission=Submission.Freeze(draft,next);messages=builder.Build(submission,next);
   }else{
    if(sourceRequestId is null||turnId is null||!next.Snapshots.TryGetValue(sourceRequestId,out var original)||original.TaskId!=taskId||original.TurnId!=turnId||!next.Turns.TryGetValue(turnId,out var turn))throw new AppFault("REQUEST_MISSING","原请求不可用。");
    var generation=next.Generations.GetValueOrDefault(sourceRequestId)??throw new AppFault("REQUEST_MISSING","原回答不可用。");
    if(kind=="regenerate"&&(generation.Status!="complete"||turn.CompletedRequestId!=sourceRequestId||next.Turns.Values.Any(t=>t.TaskId==taskId&&t.Order>turn.Order)))throw new AppFault("INVALID_ARGUMENT","只能重新生成最后一条已完成回答。");
    if(kind=="retry"&&generation.Status is not("failed" or "stopped" or "interrupted" or "truncated"))throw new AppFault("INVALID_ARGUMENT","此回答无需重试。");
    submission=J.Clone(original.Submission);messages=J.Clone(original.Messages);
   }
   prep=new(){OperationId=operationId,TaskId=taskId,Kind=kind,SourceRequestId=sourceRequestId,Submission=submission,Messages=messages,Options=Catalog.Resolve(next.Preferences)};
   next.Preparations[prep.Id]=prep;next.Tasks[taskId].ActiveOperationId=operationId;Commit(next);frozen=J.Clone(next);cts=new();preparing[operationId]=cts;
  }finally{gate.Release();}
  try{
   var prepared=await Task.Run(()=>BudgetValidator.Encode(prep.Messages,prep.Submission,prep.Options,frozen,files,Catalog),cts.Token);await transport.EnsureCredentialsAsync(cts.Token);cts.Token.ThrowIfCancellationRequested();
   string requestId;
   await gate.WaitAsync();try{
    cts.Token.ThrowIfCancellationRequested();var next=J.Clone(state);var task=TaskFor(next,taskId);if(closing||task.ActiveOperationId!=operationId)throw new OperationCanceledException();
    TurnRecord turn;
    if(kind=="send"){turn=new(){TaskId=taskId,Order=task.NextTurnOrder++,Submission=J.Clone(prep.Submission)};next.Turns[turn.Id]=turn;}
    else turn=next.Turns[turnId!];
    var snap=new RequestSnapshot{TaskId=taskId,TurnId=turn.Id,Submission=J.Clone(prep.Submission),Messages=J.Clone(prep.Messages),Options=prep.Options,Budget=prepared.Budget,CapabilitiesVersion=Catalog.Version};requestId=snap.Id;
    var g=new Generation{Id=requestId,SnapshotId=requestId,TaskId=taskId,TurnId=turn.Id,Kind=kind,RestoresRequestId=kind=="regenerate"?turn.CompletedRequestId:null};
    next.Snapshots[requestId]=snap;next.Generations[requestId]=g;turn.DisplayedRequestId=requestId;task.ActiveRequestId=requestId;task.UpdatedAt=J.Now();
    var savedPrep=next.Preparations[prep.Id];savedPrep.State="accepted";savedPrep.AcceptedRequestId=requestId;
    if(kind=="send")CleanSubmitted(DraftFor(next,taskId),prep.Submission);
    Commit(next);preparing.Remove(operationId);var work=RunGenerationAsync(requestId,prepared.Bytes,cts);jobs[requestId]=(cts,work);
   }finally{gate.Release();}
   return requestId;
  }catch(Exception error){
   await Mutate(s=>{if(s.Preparations.TryGetValue(prep.Id,out var p)){p.State=error is OperationCanceledException?"stopped":"failed";p.Error=FaultRecord.From(error);}if(s.Tasks.TryGetValue(taskId,out var t)&&t.ActiveOperationId==operationId){t.ActiveOperationId=null;t.ActiveRequestId=null;}});
   await gate.WaitAsync();try{preparing.Remove(operationId);}finally{gate.Release();}cts.Dispose();if(error is OperationCanceledException)throw new AppFault("REQUEST_STOPPED","请求已停止，草稿保留。");throw;
  }
 }
 static void CleanSubmitted(Draft draft,Submission submission){
  if(draft.PromptRevision==submission.PromptRevision){draft.Prompt="";draft.PromptRevision++;}
  var quoted=submission.ExternalQuotes.ToDictionary(q=>q.Id,q=>q.Revision);draft.ExternalQuotes.RemoveAll(q=>quoted.TryGetValue(q.Id,out var rev)&&rev==q.Revision);
  var images=submission.Images.ToDictionary(x=>x.DraftItemId,x=>x.ItemRevision);draft.Images.RemoveAll(x=>images.TryGetValue(x.Id,out var rev)&&rev==x.Revision);
  if(draft.AssistantQuote?.Id==submission.AssistantQuote?.Id&&draft.AssistantQuote?.Revision==submission.AssistantQuote?.Revision)draft.AssistantQuote=null;
  draft.Revision++;draft.UpdatedAt=J.Now();
 }
 async Task RunGenerationAsync(string requestId,byte[] body,CancellationTokenSource cts){
  var answer=new StringBuilder();var reasoning=new StringBuilder();var last=Environment.TickCount64;string? finish=null;Exception? failure=null;
  async Task Flush(){if(answer.Length==0&&reasoning.Length==0)return;var a=answer.ToString();var r=reasoning.ToString();await Mutate(s=>{if(s.Generations.TryGetValue(requestId,out var g)&&g.Active){g.Answer+=a;g.Reasoning+=r;g.Status="streaming";g.UpdatedAt=J.Now();}});answer.Clear();reasoning.Clear();last=Environment.TickCount64;}
  try{
   await Mutate(s=>{if(s.Generations.TryGetValue(requestId,out var g)&&g.Active)g.Status="connecting";});
   await foreach(var delta in transport.StreamAsync(body,cts.Token).WithCancellation(cts.Token)){
    answer.Append(delta.Answer);reasoning.Append(delta.Reasoning);if(delta.FinishReason is not null)finish=delta.FinishReason;
    if(answer.Length+reasoning.Length>=8192||Environment.TickCount64-last>=200)await Flush();
   }
   cts.Token.ThrowIfCancellationRequested();if(finish is null)throw new AppFault("NETWORK_ERROR","服务连接提前结束，已接收内容已保存。",true);
  }catch(Exception error){failure=error;}
  try{
   await Flush();await Mutate(s=>{
    if(!s.Generations.TryGetValue(requestId,out var g)||!s.Turns.TryGetValue(g.TurnId,out var turn))return;
    g.Status=failure is OperationCanceledException?"stopped":failure is not null?"failed":finish=="length"?"truncated":"complete";g.FinishReason=finish;g.Error=failure is null?null:FaultRecord.From(failure);g.UpdatedAt=J.Now();
    if(g.Status=="complete")turn.CompletedRequestId=requestId;else if(g.RestoresRequestId is {} old)turn.DisplayedRequestId=old;
    if(s.Tasks.TryGetValue(g.TaskId,out var task)&&task.ActiveRequestId==requestId){task.ActiveRequestId=null;task.ActiveOperationId=null;}
   });
  }catch{Notify("storage-failed");}
  finally{Array.Clear(body);await gate.WaitAsync();try{jobs.Remove(requestId);}finally{gate.Release();}cts.Dispose();}
 }
 public async Task StopAsync(string taskId,string? requestId,string? operationId){await gate.WaitAsync();try{
  var task=TaskFor(state,taskId);if(requestId is not null){if(task.ActiveRequestId==requestId&&jobs.TryGetValue(requestId,out var job))job.Cancel.Cancel();}
  else if(operationId is not null&&task.ActiveOperationId==operationId&&preparing.TryGetValue(operationId,out var cancel))cancel.Cancel();
 }finally{gate.Release();}}
 public async Task DeleteTaskAsync(string id,int expected){
  await gate.WaitAsync();try{
   var task=TaskFor(state,id);Check(expected,task.Revision);if(task.ActiveRequestId is {} req&&jobs.TryGetValue(req,out var job))job.Cancel.Cancel();if(task.ActiveOperationId is {} op&&preparing.TryGetValue(op,out var prep))prep.Cancel();
   var next=J.Clone(state);next.Tasks.Remove(id);next.Drafts.Remove(id);foreach(var key in next.Turns.Where(x=>x.Value.TaskId==id).Select(x=>x.Key).ToArray())next.Turns.Remove(key);foreach(var key in next.Generations.Where(x=>x.Value.TaskId==id).Select(x=>x.Key).ToArray())next.Generations.Remove(key);foreach(var key in next.Snapshots.Where(x=>x.Value.TaskId==id).Select(x=>x.Key).ToArray())next.Snapshots.Remove(key);foreach(var key in next.Preparations.Where(x=>x.Value.TaskId==id).Select(x=>x.Key).ToArray())next.Preparations.Remove(key);
   if(next.Tasks.Count==0)AddTask(next,"阅读任务");if(next.ActiveTaskId==id)next.ActiveTaskId=next.Tasks.Values.OrderByDescending(t=>t.UpdatedAt).First().Id;Commit(next);selections.Remove(id);routeEpoch++;
  }finally{gate.Release();}await CollectAsync();
 }
 public async Task ClearAllAsync(){await gate.WaitAsync();try{foreach(var job in jobs.Values)job.Cancel.Cancel();foreach(var prep in preparing.Values)prep.Cancel();var next=new State{Preferences=J.Clone(state.Preferences),Operations=J.Clone(state.Operations)};AddTask(next,"阅读任务");Commit(next);routeEpoch++;selections.Clear();}finally{gate.Release();}await CollectAsync();}
 static HashSet<string> LiveIds(State s)=>s.Drafts.Values.SelectMany(x=>x.Images.Select(i=>i.ImageId)).Concat(s.Snapshots.Values.SelectMany(x=>x.Messages.SelectMany(m=>m.Parts.Where(p=>p.Type=="image").Select(p=>p.ImageId!)))).Concat(s.Preparations.Values.Where(x=>x.State=="preparing").SelectMany(x=>x.Submission.Images.Select(i=>i.ImageId))).ToHashSet();
 public async Task CollectAsync(){await gate.WaitAsync();try{var next=J.Clone(state);var live=LiveIds(next);foreach(var id in next.Assets.Keys.Where(id=>!live.Contains(id)).ToArray())next.Assets.Remove(id);Commit(next);files.Collect(live);}finally{gate.Release();}}
 public async Task<ImageAsset> GetAssetAsync(string id){await gate.WaitAsync();try{if(!J.IsId(id)||!LiveIds(state).Contains(id)||!state.Assets.TryGetValue(id,out var asset))throw new AppFault("IMAGE_MISSING","截图不可用。");return J.Clone(asset);}finally{gate.Release();}}
 object ImageView(State s,string imageId,string? draftItemId=null){if(!s.Assets.TryGetValue(imageId,out var a))return new{id=imageId,missing=true};return new{id=a.Id,draftItemId,source=a.Source.View(),width=a.Width,height=a.Height,byteLength=a.ByteLength,thumbnailURL=$"https://anysidebar.local/asset/{a.Id}/thumbnail",previewURL=$"https://anysidebar.local/asset/{a.Id}/preview"};}
 public async Task<object> ViewAsync(string? taskId=null,int? cursor=null){await gate.WaitAsync();try{
  var s=state;var id=taskId??s.ActiveTaskId;object? current=null;
  if(id is not null&&s.Tasks.TryGetValue(id,out var task)){
   var d=s.Drafts[id];var all=s.Turns.Values.Where(t=>t.TaskId==id).OrderBy(t=>t.Order).ToArray();var end=Math.Max(0,all.Length-(cursor??0));var start=Math.Max(0,end-30);
   var turns=all[start..end].Select(t=>{var g=s.Generations[t.DisplayedRequestId];return new{id=t.Id,requestId=g.Id,prompt=t.Submission.Prompt,externalQuotes=t.Submission.ExternalQuotes.Select(q=>q.View()),assistantQuote=t.Submission.AssistantQuote,images=t.Submission.Images.Select(x=>ImageView(s,x.ImageId)),answer=g.Answer,reasoning=g.Reasoning,status=g.Status,error=g.Error,canRegenerate=g.Status=="complete"&&t.Order==all.Last().Order&&task.ActiveOperationId is null};}).ToArray();
   current=new{task,draft=new{taskId=d.TaskId,revision=d.Revision,promptRevision=d.PromptRevision,prompt=d.Prompt,externalQuotes=d.ExternalQuotes.Select(q=>q.View()),assistantQuote=d.AssistantQuote,images=d.Images.Select(x=>ImageView(s,x.ImageId,x.Id))},turns,nextCursor=start>0?(int?)(all.Length-start):null};
  }
  return new{bootId=BootId,sequence,activeTaskId=s.ActiveTaskId,tasks=s.Tasks.Values.OrderByDescending(t=>t.UpdatedAt).ToArray(),currentTask=current,preferences=J.Clone(s.Preferences),models=Catalog.Models.Select(m=>new{m.ProviderId,m.ModelId,m.Label,m.InputModalities,m.ReasoningEfforts})};
 }finally{gate.Release();}}
 public async ValueTask DisposeAsync(){Task[] works;await gate.WaitAsync();try{closing=true;foreach(var p in preparing.Values)p.Cancel();foreach(var job in jobs.Values)job.Cancel.Cancel();works=jobs.Values.Select(x=>x.Work).ToArray();}finally{gate.Release();}await Task.WhenAll(works);await gate.WaitAsync();try{repository.Save(state);}finally{gate.Release();repository.Dispose();}}
}
