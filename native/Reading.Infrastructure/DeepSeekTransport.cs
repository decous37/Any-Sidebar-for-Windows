using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AnySidebar.Core;
namespace AnySidebar.Infrastructure;
public interface ICredentialVault {string? Read();void Save(string key);void Clear();}
public sealed class DeepSeekTransport: ITransport,IDisposable {
 readonly ICredentialVault vault;readonly HttpClient client;
 public DeepSeekTransport(ICredentialVault vault,HttpMessageHandler? handler=null){this.vault=vault;client=handler is null?new(new SocketsHttpHandler{AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(30)}):new(handler);client.Timeout=Timeout.InfiniteTimeSpan;}
 public Task EnsureCredentialsAsync(CancellationToken cancel){cancel.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(vault.Read()))throw new AppFault("CREDENTIAL_REQUIRED","请先填写 API Key。");return Task.CompletedTask;}
 public async IAsyncEnumerable<StreamDelta> StreamAsync(byte[] body,[EnumeratorCancellation]CancellationToken cancel){
  var key=vault.Read();if(string.IsNullOrWhiteSpace(key))throw new AppFault("CREDENTIAL_REQUIRED","请先填写 API Key。");
  using var total=CancellationTokenSource.CreateLinkedTokenSource(cancel);total.CancelAfter(TimeSpan.FromMinutes(30));
  using var upload=CancellationTokenSource.CreateLinkedTokenSource(total.Token);upload.CancelAfter(TimeSpan.FromSeconds(120));
  using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.deepseek.com/chat/completions");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);request.Headers.Accept.Add(new("text/event-stream"));request.Content=new ByteArrayContent(body);request.Content.Headers.ContentType=new("application/json");
  HttpResponseMessage response;
  try{response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,upload.Token);}catch(OperationCanceledException)when(!cancel.IsCancellationRequested){throw new AppFault("NETWORK_TIMEOUT","连接或上传超时，已保存内容保留。",true);}catch(HttpRequestException){throw new AppFault("NETWORK_ERROR","无法连接 DeepSeek，请检查网络。",true);}
  using(response){
   if(response.StatusCode!=HttpStatusCode.OK)throw MapStatus((int)response.StatusCode);
   if(!response.Content.Headers.ContentType?.MediaType?.Equals("text/event-stream",StringComparison.OrdinalIgnoreCase)??true)throw new AppFault("INVALID_RESPONSE","服务返回的数据格式无法识别。",true);
   await using var stream=await response.Content.ReadAsStreamAsync(total.Token);using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,4096,leaveOpen:true);var data=new StringBuilder();bool done=false,finished=false;
   while(true){
    string? line;using var idle=CancellationTokenSource.CreateLinkedTokenSource(total.Token);idle.CancelAfter(TimeSpan.FromSeconds(120));
    try{line=await reader.ReadLineAsync(idle.Token);}catch(OperationCanceledException)when(!cancel.IsCancellationRequested){throw new AppFault("NETWORK_TIMEOUT","等待回答超时，已接收内容保留。",true);}catch(DecoderFallbackException){throw new AppFault("INVALID_RESPONSE","服务返回了无法解析的文字编码。",true);}
    if(line is null){if(data.Length>0){var remaining=SseFrame(data.ToString(),out var ended);foreach(var delta in remaining){finished|=delta.FinishReason is not null;yield return delta;}done|=ended;}break;}
    if(line.Length==0){if(data.Length==0)continue;var frame=data.ToString();data.Clear();var deltas=SseFrame(frame,out var end);if(end){done=true;break;}foreach(var delta in deltas){finished|=delta.FinishReason is not null;yield return delta;}continue;}
    if(line.StartsWith(':'))continue;if(line.StartsWith("data:",StringComparison.Ordinal)){var value=line[5..];if(value.StartsWith(' '))value=value[1..];if(data.Length>0)data.Append('\n');data.Append(value);if(data.Length>2*1024*1024)throw new AppFault("INVALID_RESPONSE","服务事件过大。",true);}
   }
   if(!done&&!finished)throw new AppFault("NETWORK_ERROR","服务连接提前结束，已接收内容保留。",true);
   if(done&&!finished)yield return new(FinishReason:"stop");
  }
 }
 public static List<StreamDelta> SseFrame(string data,out bool done){done=data.Trim()=="[DONE]";if(done)return[];try{using var doc=JsonDocument.Parse(data);var root=doc.RootElement;if(root.TryGetProperty("error",out _))throw new AppFault("API_ERROR","服务返回错误，请稍后重试。",true);if(!root.TryGetProperty("choices",out var choices)||choices.ValueKind!=JsonValueKind.Array)throw new JsonException();var output=new List<StreamDelta>();foreach(var c in choices.EnumerateArray()){if(c.TryGetProperty("index",out var index)&&index.GetInt32()!=0)continue;var answer="";var reasoning="";if(c.TryGetProperty("delta",out var delta)){if(delta.TryGetProperty("content",out var a)&&a.ValueKind==JsonValueKind.String)answer=a.GetString()!;if(delta.TryGetProperty("reasoning_content",out var r)&&r.ValueKind==JsonValueKind.String)reasoning=r.GetString()!;}string? finish=c.TryGetProperty("finish_reason",out var f)&&f.ValueKind==JsonValueKind.String?f.GetString():null;if(answer.Length+reasoning.Length>0||finish is not null)output.Add(new(answer,reasoning,finish));}return output;}catch(JsonException){throw new AppFault("INVALID_RESPONSE","服务返回了无法解析的数据。",true);}}
 public static AppFault MapStatus(int status)=>status switch{401 or 403=>new("CREDENTIAL_REJECTED","API Key 无效或无访问权限，请检查设置。"),402=>new("INSUFFICIENT_BALANCE","DeepSeek 账户余额不足。"),429=>new("RATE_LIMITED","请求过于频繁，请稍后手动重试。",true),>=500=>new("SERVICE_UNAVAILABLE","DeepSeek 服务暂时不可用，请稍后重试。",true),_=>new("API_ERROR",$"服务拒绝请求（HTTP {status}）。请检查模型和资料限制。",status>=500)};
 public void Dispose()=>client.Dispose();
}
