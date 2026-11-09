using System.Text.Json;
using System.Text.Json.Serialization;
namespace AnySidebar.Core;

public static class J {
 public static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,DefaultIgnoreCondition=JsonIgnoreCondition.Never,PropertyNameCaseInsensitive=false};
 public static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Options),Options)!;
 public static string Id()=>Guid.NewGuid().ToString("D");
 public static bool IsId(string? value)=>Guid.TryParseExact(value,"D",out _);
 public static long Now()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
 public static T Read<T>(JsonElement value)=>value.Deserialize<T>(Options)??throw new AppFault("INVALID_ARGUMENT","操作参数无效。");
}
public sealed class AppFault(string code,string message,bool retryable=false,int? currentRevision=null):Exception(message) {
 public string Code{get;}=code;public bool Retryable{get;}=retryable;public int? CurrentRevision{get;}=currentRevision;
 public object View()=>new{code=Code,message=Message,retryable=Retryable,currentRevision=CurrentRevision};
 public static AppFault Storage()=>new("STORAGE_FAILED","本机保存失败，已保存的资料会保留。请检查磁盘空间和访问权限。",true);
}
public sealed class SourceInfo {
 public string Id{get;set;}=J.Id();public string Kind{get;set;}="external-text";public string? AppBundleId{get;set;}public string? AppName{get;set;}public string? DocumentTitle{get;set;}public string? DocumentIdentity{get;set;}public string? PageLabel{get;set;}public long CapturedAt{get;set;}=J.Now();
 public object View()=>new{id=Id,kind=Kind,appBundleId=AppBundleId,appName=AppName,documentTitle=DocumentTitle,pageLabel=PageLabel,capturedAt=CapturedAt};
}
public sealed class ExternalQuote {
 public string Id{get;set;}=J.Id();public int Revision{get;set;}public SourceInfo Source{get;set;}=new();public string Text{get;set;}="";public long SelectedAt{get;set;}=J.Now();public string? SourceLeaseId{get;set;}public long SourceSequence{get;set;}public string? SelectionToken{get;set;}
 public object View()=>new{id=Id,revision=Revision,source=Source.View(),text=Text,selectedAt=SelectedAt};
}
public sealed class AssistantQuote {
 public string Id{get;set;}=J.Id();public int Revision{get;set;}public string SourceTurnId{get;set;}="";public string SourceRequestId{get;set;}="";public string Text{get;set;}="";public long SelectedAt{get;set;}=J.Now();
}
public sealed class DraftImage {public string Id{get;set;}=J.Id();public int Revision{get;set;}public string ImageId{get;set;}="";public long AddedAt{get;set;}=J.Now();}
public sealed class Draft {
 public string TaskId{get;set;}="";public int Revision{get;set;}public int PromptRevision{get;set;}public string Prompt{get;set;}="";public List<ExternalQuote> ExternalQuotes{get;set;}=[];public AssistantQuote? AssistantQuote{get;set;}public List<DraftImage> Images{get;set;}=[];public long UpdatedAt{get;set;}=J.Now();
}
public sealed class ReadingTask {
 public string Id{get;set;}=J.Id();public string Title{get;set;}="阅读任务";public int Revision{get;set;}public int NextTurnOrder{get;set;}public string? ActiveOperationId{get;set;}public string? ActiveRequestId{get;set;}public long CreatedAt{get;set;}=J.Now();public long UpdatedAt{get;set;}=J.Now();
}
public sealed class ImageAsset {
 public string Id{get;set;}=J.Id();public SourceInfo Source{get;set;}=new();public string MediaType{get;set;}="image/png";public int Width{get;set;}public int Height{get;set;}public long ByteLength{get;set;}public string Sha256{get;set;}="";public string StorageKey{get;set;}="";public string? ThumbnailKey{get;set;}public string State{get;set;}="ready";public long CreatedAt{get;set;}=J.Now();
}
public sealed class FrozenImage {public string DraftItemId{get;set;}="";public int ItemRevision{get;set;}public string ImageId{get;set;}="";public string Sha256{get;set;}="";}
public sealed class Submission {
 public string TaskId{get;set;}="";public int DraftRevision{get;set;}public int PromptRevision{get;set;}public string Prompt{get;set;}="";public List<ExternalQuote> ExternalQuotes{get;set;}=[];public AssistantQuote? AssistantQuote{get;set;}public List<FrozenImage> Images{get;set;}=[];
 public static Submission Freeze(Draft draft,State state)=>new(){TaskId=draft.TaskId,DraftRevision=draft.Revision,PromptRevision=draft.PromptRevision,Prompt=draft.Prompt,ExternalQuotes=J.Clone(draft.ExternalQuotes),AssistantQuote=draft.AssistantQuote is null?null:J.Clone(draft.AssistantQuote),Images=draft.Images.Select(x=>new FrozenImage{DraftItemId=x.Id,ItemRevision=x.Revision,ImageId=x.ImageId,Sha256=state.Assets.TryGetValue(x.ImageId,out var a)&&a.State=="ready"?a.Sha256:throw new AppFault("IMAGE_MISSING","截图不可用，请移除后重新截取。")}).ToList()};
}
public sealed class MessagePart {public string Type{get;set;}="text";public string? Text{get;set;}public string? ImageId{get;set;}public string? Sha256{get;set;}public string? Detail{get;set;}public static MessagePart T(string text)=>new(){Text=text};public static MessagePart I(FrozenImage image)=>new(){Type="image",ImageId=image.ImageId,Sha256=image.Sha256,Detail="original"};}
public sealed class FrozenMessage {public string Role{get;set;}="user";public List<MessagePart> Parts{get;set;}=[];public static FrozenMessage Text(string role,string text)=>new(){Role=role,Parts=[MessagePart.T(text)]};}
public sealed class ResolvedOptions {public string ProviderId{get;set;}="deepseek";public string ModelId{get;set;}="deepseek-flash";public bool ThinkingEnabled{get;set;}=true;public string? ReasoningEffort{get;set;}="high";public int MaxOutputTokens{get;set;}=16384;}
public sealed class BudgetReport {public int NewImageCount{get;set;}public int RequestImageParts{get;set;}public int EncodedBodyBytes{get;set;}public long TextTokenUpperBound{get;set;}public long ImageTokenUpperBound{get;set;}public int ReservedEnvelopeTokens{get;set;}=1024;public int ReservedOutputTokens{get;set;}public int ModelContextTokens{get;set;}public bool Allowed{get;set;}public List<string> Violations{get;set;}=[];public string Method{get;set;}="desktop-utf8-image-upper-bound-v1";}
public sealed class RequestSnapshot {public string Id{get;set;}=J.Id();public string TaskId{get;set;}="";public string TurnId{get;set;}="";public Submission Submission{get;set;}=new();public List<FrozenMessage> Messages{get;set;}=[];public string PromptVersion{get;set;}="desktop-reading-v1";public string CapabilitiesVersion{get;set;}="";public ResolvedOptions Options{get;set;}=new();public BudgetReport Budget{get;set;}=new();public long CreatedAt{get;set;}=J.Now();}
public sealed class TurnRecord {public string Id{get;set;}=J.Id();public string TaskId{get;set;}="";public int Order{get;set;}public Submission Submission{get;set;}=new();public string DisplayedRequestId{get;set;}="";public string? CompletedRequestId{get;set;}public long CreatedAt{get;set;}=J.Now();}
public sealed class FaultRecord {public string Code{get;set;}="UNKNOWN";public string Message{get;set;}="操作未完成。";public bool Retryable{get;set;}public static FaultRecord From(Exception e)=>e is AppFault a?new(){Code=a.Code,Message=a.Message,Retryable=a.Retryable}:new(){Code="NETWORK_ERROR",Message="请求中断，请稍后重试。",Retryable=true};}
public sealed class Generation {public string Id{get;set;}="";public string TaskId{get;set;}="";public string TurnId{get;set;}="";public string SnapshotId{get;set;}="";public string Kind{get;set;}="send";public string? RestoresRequestId{get;set;}public string Status{get;set;}="prepared";public string Answer{get;set;}="";public string Reasoning{get;set;}="";public string? FinishReason{get;set;}public FaultRecord? Error{get;set;}public long CreatedAt{get;set;}=J.Now();public long UpdatedAt{get;set;}=J.Now();[JsonIgnore]public bool Active=>Status is "prepared" or "connecting" or "streaming";}
public sealed class Preparation {public string Id{get;set;}=J.Id();public string OperationId{get;set;}="";public string TaskId{get;set;}="";public string Kind{get;set;}="send";public string? SourceRequestId{get;set;}public Submission Submission{get;set;}=new();public List<FrozenMessage> Messages{get;set;}=[];public ResolvedOptions Options{get;set;}=new();public string State{get;set;}="preparing";public string? AcceptedRequestId{get;set;}public FaultRecord? Error{get;set;}}
public sealed class Shortcut {public int KeyCode{get;set;}public List<string> Modifiers{get;set;}=[];public static Shortcut Default(int code)=>new(){KeyCode=code,Modifiers=["control","alt"]};}
public sealed class Preferences {public int Revision{get;set;}public string ModelId{get;set;}="deepseek-flash";public bool ThinkingEnabled{get;set;}=true;public string ReasoningEffort{get;set;}="high";public string Theme{get;set;}="system";public int FontSize{get;set;}=13;public string QuoteMode{get;set;}="latest";public bool AutoSelectionEnabled{get;set;}public Shortcut? CaptureShortcut{get;set;}=Shortcut.Default(0x41);public Shortcut? PanelShortcut{get;set;}=Shortcut.Default(0x53);public string? DisclosureVersion{get;set;}public bool CompanionVisible{get;set;}=true;public long UpdatedAt{get;set;}=J.Now();}
public sealed class Operation {public string Id{get;set;}="";public string Command{get;set;}="";public string Digest{get;set;}="";public string State{get;set;}="pending";public string? ResultId{get;set;}public FaultRecord? Error{get;set;}}
public sealed class State {public int SchemaVersion{get;set;}=1;public string? ActiveTaskId{get;set;}public Preferences Preferences{get;set;}=new();public Dictionary<string,ReadingTask> Tasks{get;set;}=[];public Dictionary<string,Draft> Drafts{get;set;}=[];public Dictionary<string,ImageAsset> Assets{get;set;}=[];public Dictionary<string,TurnRecord> Turns{get;set;}=[];public Dictionary<string,RequestSnapshot> Snapshots{get;set;}=[];public Dictionary<string,Generation> Generations{get;set;}=[];public Dictionary<string,Preparation> Preparations{get;set;}=[];public Dictionary<string,Operation> Operations{get;set;}=[];}
public sealed record StreamDelta(string Answer="",string Reasoning="",string? FinishReason=null);
public interface IRepository:IDisposable {State Load();void Save(State state);}
public interface IAttachments {ImageAsset Persist(byte[] png,SourceInfo source,byte[]? thumbnail=null);byte[] Read(ImageAsset asset,bool thumbnail=false);void Delete(ImageAsset asset);void Collect(IReadOnlySet<string> liveIds);}
public interface ITransport {Task EnsureCredentialsAsync(CancellationToken cancel);IAsyncEnumerable<StreamDelta> StreamAsync(byte[] body,CancellationToken cancel);}
public sealed record PreparedBody(byte[] Bytes,BudgetReport Budget);
public sealed record SelectionEvent(string TaskId,long RouteEpoch,string Lease,long Sequence,string Token,string? ExpectedQuoteId,SourceInfo Source,string? Text);
