using Microsoft.Web.WebView2.Core;
namespace AnySidebar.Windows;
internal sealed class WebBridge {
 const string Origin="https://anysidebar.local";readonly AppController app;readonly Dictionary<string,(Dictionary<string,string> Required,Dictionary<string,string> Optional)> schema=[];string? epoch;bool ready;
 internal WebBridge(AppController app){this.app=app;using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(app.Resources,"contracts","bridge.schema.json")));foreach(var c in doc.RootElement.GetProperty("commands").EnumerateObject())schema[c.Name]=(c.Value.GetProperty("required").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!),c.Value.GetProperty("optional").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!));}
 internal async Task InitializeAsync(){
  var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(app.Directory,"webview"));await app.Shell.Web.EnsureCoreWebView2Async(environment);var web=app.Shell.Web.CoreWebView2;
  web.Settings.AreDevToolsEnabled=false;web.Settings.AreDefaultContextMenusEnabled=false;web.Settings.IsStatusBarEnabled=false;web.Settings.AreHostObjectsAllowed=false;web.Settings.IsPasswordAutosaveEnabled=false;web.Settings.IsGeneralAutofillEnabled=false;
  web.SetVirtualHostNameToFolderMapping("anysidebar.local",Path.Combine(app.Resources,"web"),CoreWebView2HostResourceAccessKind.Deny);
  web.NavigationStarting+=(_,e)=>{if(e.Uri!=Origin+"/index.html"&&!e.Uri.StartsWith(Origin+"/index.html#",StringComparison.Ordinal)){e.Cancel=true;if(!e.Uri.StartsWith(Origin+"/",StringComparison.Ordinal))app.OpenExternal(e.Uri,e.IsUserInitiated);}};
  web.FrameNavigationStarting+=(_,e)=>e.Cancel=true;
  web.NewWindowRequested+=(_,e)=>{e.Handled=true;app.OpenExternal(e.Uri,e.IsUserInitiated);};
  web.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
  web.DownloadStarting+=(_,e)=>e.Cancel=true;
  web.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
  web.WebResourceRequested+=async(_,e)=>{
   var uri=e.Request.Uri;
   if(uri.StartsWith(Origin+"/asset/",StringComparison.Ordinal)){
    var deferral=e.GetDeferral();try{var parts=new Uri(uri).AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);if(parts.Length!=3||!J.IsId(parts[1])||parts[2] is not("preview" or "thumbnail"))throw new AppFault("IMAGE_MISSING","截图不可用。");var asset=await app.Runtime.GetAssetAsync(parts[1]);var bytes=await Task.Run(()=>app.Files.Read(asset,parts[2]=="thumbnail"));e.Response=environment.CreateWebResourceResponse(new MemoryStream(bytes),200,"OK","Content-Type: image/png\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff");}catch{e.Response=environment.CreateWebResourceResponse(new MemoryStream(),404,"Not Found","Cache-Control: no-store");}finally{deferral.Complete();}
   }else if(!uri.StartsWith(Origin+"/",StringComparison.Ordinal))e.Response=environment.CreateWebResourceResponse(new MemoryStream(),403,"Forbidden","Content-Type: text/plain");
  };
  web.WebMessageReceived+=async(_,e)=>{string? messageId=null;try{
   if(e.Source!=Origin+"/index.html"&&!e.Source.StartsWith(Origin+"/index.html#",StringComparison.Ordinal))throw new AppFault("INVALID_ORIGIN","不允许的页面来源。");
   if(Encoding.UTF8.GetByteCount(e.WebMessageAsJson)>3*1024*1024)throw new AppFault("INVALID_ARGUMENT","操作参数过大。");using var doc=JsonDocument.Parse(e.WebMessageAsJson);var request=doc.RootElement;messageId=request.TryGetProperty("messageId",out var mid)?mid.GetString():null;Validate(request);
   var currentEpoch=request.GetProperty("viewEpoch").GetString()!;var command=request.GetProperty("command").GetString()!;
   if(command=="bootstrap"){epoch=currentEpoch;ready=true;}else if(currentEpoch!=epoch)throw new AppFault("STALE_VIEW","界面已更新，请重新打开。");
   var data=await app.ExecuteAsync(command,request.GetProperty("args"),request.GetProperty("operationId").GetString()!);web.PostWebMessageAsJson(JsonSerializer.Serialize(new{messageId,ok=true,data},J.Options));
  }catch(Exception error){var fault=error as AppFault??new AppFault("INVALID_ARGUMENT","操作未完成，请重试。",true);if(messageId is not null)try{web.PostWebMessageAsJson(JsonSerializer.Serialize(new{messageId,ok=false,error=fault.View()},J.Options));}catch{/* The document may already have closed. */}}};
  web.ProcessFailed+=(_,_)=>{ready=false;epoch=null;app.SetFault(new("WEBVIEW_CRASHED","对话界面已中断，已保存内容保留。请从托盘重新打开。",true));};
  app.Shell.Web.Source=new Uri(Origin+"/index.html");
 }
 void Validate(JsonElement request){
  var keys=new[]{"version","viewEpoch","messageId","operationId","command","args"};if(request.ValueKind!=JsonValueKind.Object||request.EnumerateObject().Count()!=keys.Length||keys.Any(k=>!request.TryGetProperty(k,out _))||request.GetProperty("version").GetInt32()!=1)throw new AppFault("INVALID_ARGUMENT","操作格式无效。");
  foreach(var key in new[]{"viewEpoch","messageId","operationId"})if(!J.IsId(request.GetProperty(key).GetString()))throw new AppFault("INVALID_ARGUMENT","操作标识无效。");
  var command=request.GetProperty("command").GetString()??"";if(!schema.TryGetValue(command,out var rule))throw new AppFault("INVALID_ARGUMENT","未知操作。");var args=request.GetProperty("args");if(args.ValueKind!=JsonValueKind.Object||rule.Required.Keys.Any(k=>!args.TryGetProperty(k,out _)))throw new AppFault("INVALID_ARGUMENT","缺少操作参数。");
  foreach(var p in args.EnumerateObject()){
   var type=rule.Required.GetValueOrDefault(p.Name)??rule.Optional.GetValueOrDefault(p.Name);var value=p.Value;
   var valid=type switch{"id"=>value.ValueKind==JsonValueKind.String&&J.IsId(value.GetString()),"string"=>value.ValueKind==JsonValueKind.String&&Encoding.UTF8.GetByteCount(value.GetString()!)<=2*1024*1024,"integer"=>value.TryGetInt32(out var n)&&n>=0,"boolean"=>value.ValueKind is JsonValueKind.True or JsonValueKind.False,"object"=>value.ValueKind==JsonValueKind.Object,null=>false,_=>value.ValueKind==JsonValueKind.String&&type.Split('|').Contains(value.GetString())};
   if(!valid)throw new AppFault("INVALID_ARGUMENT","操作参数无效。");
  }
 }
 internal void Notify(string type){if(!ready)return;Application.Current.Dispatcher.BeginInvoke(new Action(async()=>{try{var runtime=await app.Runtime.ViewAsync();using var json=JsonDocument.Parse(JsonSerializer.Serialize(runtime,J.Options));var sequence=json.RootElement.GetProperty("sequence").GetInt64();app.Shell.Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="native-event",detail=new{type,bootId=app.Runtime.BootId,sequence}},J.Options));}catch{/* A hidden or closing view can refresh from persisted state on reopen. */}}));}
}
