using System.Windows.Input;
using System.Windows.Shapes;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;
namespace AnySidebar.Windows;
internal sealed class CaptureService {
 Window? activeWindow;
 internal void Cancel(){activeWindow?.Close();}
 internal async Task<(byte[] Png,byte[] Thumbnail,SourceInfo Source)> CaptureAsync(string mode,IntPtr originalWindow,CancellationToken cancel){
  cancel.ThrowIfCancellationRequested();IntPtr target=originalWindow;Win32.Rect bounds;
  if(mode=="window"){
   target=await ChooseWindowAsync(originalWindow,cancel);cancel.ThrowIfCancellationRequested();if(!Win32.IsWindowVisible(target)||Win32.IsIconic(target))throw new AppFault("CAPTURE_FAILED","目标窗口不可见，请恢复窗口后重试。");Win32.SetForegroundWindow(target);bounds=Win32.Bounds(target);
  }else if(mode=="region")bounds=await SelectRegionAsync(cancel);
  else throw new AppFault("INVALID_ARGUMENT","无效读屏方式。");
  cancel.ThrowIfCancellationRequested();var screen=Forms.SystemInformation.VirtualScreen;var clipped=Drawing.Rectangle.Intersect(new(bounds.Left,bounds.Top,bounds.Width,bounds.Height),screen);
  if(clipped.Width<2||clipped.Height<2)throw new AppFault("CAPTURE_FAILED","截取范围太小或不在屏幕内。");if(clipped.Width>8192||clipped.Height>8192)throw new AppFault("IMAGE_LIMIT","截取范围超过 8192px，请缩小区域。");
  await Task.Delay(220,cancel);var source=target!=IntPtr.Zero&&!Win32.Own(target)?Win32.Source(target,mode=="region"?"screen-region":"screen-window"):new SourceInfo{Kind=mode=="region"?"screen-region":"screen-window"};
  var result=await Task.Run(()=>{
   using var bitmap=new Drawing.Bitmap(clipped.Width,clipped.Height,Drawing.Imaging.PixelFormat.Format32bppRgb);using(var graphics=Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(clipped.Left,clipped.Top,0,0,clipped.Size,Drawing.CopyPixelOperation.SourceCopy);
   cancel.ThrowIfCancellationRequested();using var bytes=new MemoryStream();bitmap.Save(bytes,Drawing.Imaging.ImageFormat.Png);var ratio=Math.Min(1,160d/Math.Max(bitmap.Width,bitmap.Height));using var thumb=new Drawing.Bitmap(bitmap,Math.Max(1,(int)(bitmap.Width*ratio)),Math.Max(1,(int)(bitmap.Height*ratio)));using var preview=new MemoryStream();thumb.Save(preview,Drawing.Imaging.ImageFormat.Png);return(Png:bytes.ToArray(),Thumbnail:preview.ToArray(),Source:source);
  },cancel);cancel.ThrowIfCancellationRequested();return result;
 }
 Task<IntPtr> ChooseWindowAsync(IntPtr original,CancellationToken cancel){
  var choices=new List<(IntPtr Handle,string Title)>();Win32.EnumWindows((h,_)=>{var title=Win32.Title(h);if(!Win32.Own(h)&&Win32.IsWindowVisible(h)&&!Win32.IsIconic(h)&&title.Length>0&&Win32.Bounds(h).Width>50)choices.Add((h,title));return true;},IntPtr.Zero);
  var done=new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);var list=new ListBox{Margin=new Thickness(16),MinHeight=180,DisplayMemberPath="Title",ItemsSource=choices.Select(x=>new WindowChoice(x.Handle,x.Title)).ToArray()};list.SelectedIndex=Math.Max(0,choices.FindIndex(x=>x.Handle==original));
  var window=new Window{Title="选择要读取的窗口",Width=440,Height=380,WindowStartupLocation=WindowStartupLocation.CenterScreen,Topmost=true,ShowInTaskbar=false};activeWindow=window;Win32.Exclude(window);
  var root=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(16)};var confirm=new Button{Content="截取窗口",Padding=new Thickness(14,7,14,7),IsDefault=true};var close=new Button{Content="取消",Margin=new Thickness(0,0,10,0),Padding=new Thickness(14,7,14,7),IsCancel=true};buttons.Children.Add(close);buttons.Children.Add(confirm);DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);root.Children.Add(list);window.Content=root;
  void Accept(){if(list.SelectedItem is WindowChoice choice){done.TrySetResult(choice.Handle);window.Close();}}confirm.Click+=(_,_)=>Accept();list.MouseDoubleClick+=(_,_)=>Accept();close.Click+=(_,_)=>window.Close();window.Closed+=(_,_)=>{activeWindow=null;done.TrySetCanceled();};var registration=cancel.Register(()=>window.Dispatcher.BeginInvoke(()=>window.Close()));_=done.Task.ContinueWith(_=>registration.Dispose(),TaskScheduler.Default);window.Show();return done.Task;
 }
 sealed record WindowChoice(IntPtr Handle,string Title);
 Task<Win32.Rect> SelectRegionAsync(CancellationToken cancel){
  var done=new TaskCompletionSource<Win32.Rect>(TaskCreationOptions.RunContinuationsAsynchronously);var virtualScreen=Forms.SystemInformation.VirtualScreen;var overlay=new Window{Title="Any Sidebar 框选读屏",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=new SolidColorBrush(Color.FromArgb(38,8,20,36)),Topmost=true,ShowInTaskbar=false,Cursor=Cursors.Cross};activeWindow=overlay;
  var canvas=new Canvas{Background=Brushes.Transparent};var rectangle=new Rectangle{Stroke=new SolidColorBrush(Color.FromRgb(80,160,245)),StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(24,140,195,255)),Visibility=Visibility.Collapsed};canvas.Children.Add(rectangle);var hint=new TextBlock{Text="拖动选择区域 · Esc 取消",Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromArgb(180,20,30,45)),Padding=new Thickness(12,7,12,7)};Canvas.SetLeft(hint,18);Canvas.SetTop(hint,18);canvas.Children.Add(hint);overlay.Content=canvas;
  Win32.Point? origin=null;
  Win32.Point Position(){Win32.GetCursorPos(out var p);return p;}
  Win32.Rect Selected(){var p=Position();var o=origin??p;return new(){Left=Math.Min(o.X,p.X),Top=Math.Min(o.Y,p.Y),Right=Math.Max(o.X,p.X),Bottom=Math.Max(o.Y,p.Y)};}
  overlay.SourceInitialized+=(_,_)=>{Win32.Exclude(overlay);Win32.SetWindowPos(Win32.Handle(overlay),new IntPtr(-1),virtualScreen.Left,virtualScreen.Top,virtualScreen.Width,virtualScreen.Height,0x40);};
  overlay.Loaded+=(_,_)=>{Win32.SetWindowPos(Win32.Handle(overlay),new IntPtr(-1),virtualScreen.Left,virtualScreen.Top,virtualScreen.Width,virtualScreen.Height,0x40);overlay.Activate();overlay.Focus();};
  overlay.MouseLeftButtonDown+=(_,e)=>{origin=Position();rectangle.Visibility=Visibility.Visible;overlay.CaptureMouse();e.Handled=true;};
  overlay.MouseMove+=(_,_)=>{if(origin is null)return;var r=Selected();Win32.GetWindowRect(Win32.Handle(overlay),out var area);var transform=PresentationSource.FromVisual(overlay)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;var top=transform.Transform(new System.Windows.Point(r.Left-area.Left,r.Top-area.Top));var size=transform.Transform(new System.Windows.Point(r.Width,r.Height));Canvas.SetLeft(rectangle,top.X);Canvas.SetTop(rectangle,top.Y);rectangle.Width=Math.Max(0,size.X);rectangle.Height=Math.Max(0,size.Y);};
  overlay.MouseLeftButtonUp+=(_,_)=>{if(origin is null)return;var r=Selected();overlay.ReleaseMouseCapture();if(r.Width>=2&&r.Height>=2)done.TrySetResult(r);overlay.Close();};
  overlay.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;overlay.Close();}};overlay.Closed+=(_,_)=>{activeWindow=null;done.TrySetCanceled();};var registration=cancel.Register(()=>overlay.Dispatcher.BeginInvoke(()=>overlay.Close()));_=done.Task.ContinueWith(_=>registration.Dispose(),TaskScheduler.Default);overlay.Show();return done.Task;
 }
}
