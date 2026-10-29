using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Planner.Data;
using Planner.Desktop;
static class RouteFixChecks
{
 public static async Task RunSearch(MainWindow owner,string directory,Action<bool,string> check)
 {
  var type=typeof(MainWindow).Assembly.GetType("Planner.Desktop.PlaceSearchBox")??throw new Exception("缺少可见的地点搜索控件");
  var gate=new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
  using var http=new HttpClient(new Handler(async req=>{if(req.RequestUri!.AbsolutePath.Contains("district"))return Json(new{status="1",districts=new[]{new{polyline="118,31;120,31;120,33;118,33"}}});string q=Uri.UnescapeDataString(req.RequestUri.Query.Split('&').Single(s=>s.TrimStart('?').StartsWith("keywords=")).Split('=')[1]);if(q=="玄武湖")return await gate.Task;if(q=="出错")return new(HttpStatusCode.InternalServerError);return Json(new{status="1",pois=q=="空"?Array.Empty<object>():new object[]{new{id="new",name=q,location="118.8,32.0",adcode="320102",address="测试新地点地址"}}});}));
  var client=new AmapClient("fixture",http);dynamic search=Activator.CreateInstance(type,[new Func<string,CancellationToken,Task<List<Place>>>(client.SearchPlacesAsync)])!;
  var w=new Window{Title="地点搜索实测",Width=650,Height=520,Owner=owner,Content=search};w.Show();search.QueryInput.Text="玄武湖";Task pending=search.SearchAsync();await Task.Delay(30);check(((string)search.Feedback.Text).Contains("搜索")&&!(bool)search.SearchButton.IsEnabled,"地点搜索立即显示加载状态并防止重复提交");
  gate.SetResult(Json(new{status="1",pois=new[]{new{id="a",name="玄武湖公园南门",location="118.8,32.0",adcode="320102",address="玄武区测试南门"},new{id="b",name="玄武湖公园东门",location="118.81,32.01",adcode="320102",address="玄武区测试东门"}}}));await pending;check((int)search.Results.Items.Count==2&&search.SelectedPlace is null,"搜索结果在界面列出并要求用户确认多个候选");search.Results.SelectedIndex=1;check(((Place)search.SelectedPlace).Address=="玄武区测试东门","点击地点候选保留准确名称地址坐标");
  search.QueryInput.Text="空";await (Task)search.SearchAsync();check((int)search.Results.Items.Count==0&&search.SelectedPlace is null&&((string)search.Feedback.Text).Contains("没有找到"),"空搜索清除旧地点并明确提示没有结果");search.QueryInput.Text="出错";await (Task)search.SearchAsync();check(((string)search.Feedback.Text).Contains("失败")&&(bool)search.SearchButton.IsEnabled,"搜索失败明确反馈并允许再次搜索");search.QueryInput.Text="新地点";await (Task)search.SearchAsync();check(((Place)search.SelectedPlace).Name=="新地点","搜索失败后换词重试可正常选择唯一结果");Capture(w,Path.Combine(directory,"place-search-results.png"));w.Close();
 }
 public static void CheckFrame(Window w,Action<bool,string> check)
 {
  var frame=w.Content as Border;check(frame is not null&&frame.BorderThickness.Left>=1&&frame.BorderBrush is not null&&frame.BorderBrush.ToString()!=frame.Background.ToString(),"无系统边框窗口有可辨识的暖色外轮廓");var caption=Walk(w).OfType<DockPanel>().SingleOrDefault(x=>Equals(x.Tag,"AppCaption"));check(caption?.Background is not null&&caption.Background.ToString()!=w.Background.ToString(),"应用标题区与正文有独立底色分界");
 }
 static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")};
 static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var d in Walk(c))yield return d;}
 static void Capture(Window window,string path){window.UpdateLayout();var e=(FrameworkElement)window.Content;var b=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth),(int)Math.Ceiling(e.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);b.Render(e);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(b));using var stream=File.Create(path);png.Save(stream);}
 sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
}
