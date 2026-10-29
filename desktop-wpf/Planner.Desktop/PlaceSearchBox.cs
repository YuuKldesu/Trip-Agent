using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using Planner.Core;
namespace Planner.Desktop;

// Search state stays beside its input so requests cannot silently disappear below a form.
public sealed class PlaceSearchBox:StackPanel
{
 readonly Func<string,CancellationToken,Task<List<Place>>> search;
 CancellationTokenSource? request;long serial;bool setting;bool closed;
 public TextBox QueryInput {get;}=Ui.Input();
 public Button SearchButton {get;}
 public ListBox Results {get;}=new(){MaxHeight=180,MinHeight=60,Visibility=Visibility.Collapsed};
 public TextBlock Feedback {get;}=Ui.Text("输入地点关键词，点击搜索或按回车。",12);
 public Place? SelectedPlace=>Results.SelectedItem as Place;
 public event Action? Changed;
 public PlaceSearchBox(Func<string,CancellationToken,Task<List<Place>>> search)
 {
  this.search=search;Ui.Field(this,"地点关键词",QueryInput);SearchButton=Ui.Button("搜索地点",()=>{});SearchButton.Tag="PlaceSearchButton";Children.Add(SearchButton);Children.Add(Feedback);Children.Add(Results);
  var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding());text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);text.SetValue(TextBlock.MarginProperty,new Thickness(6));Results.ItemTemplate=new DataTemplate{VisualTree=text};
  QueryInput.TextChanged+=(_,_)=>{serial++;request?.Cancel();setting=true;Results.ItemsSource=null;setting=false;Results.Visibility=Visibility.Collapsed;Feedback.Text="地点已修改，请重新搜索确认。";SetBusy(false);Changed?.Invoke();};
  QueryInput.KeyDown+=async(_,e)=>{if(e.Key==Key.Enter){e.Handled=true;await SearchAsync();}};SearchButton.Click+=async(_,_)=>await SearchAsync();
  Results.SelectionChanged+=(_,_)=>{if(setting)return;if(SelectedPlace is {} p){Feedback.Text="已选择："+p+"\n"+p.Source;Feedback.Foreground=Ui.Brush("#55715F");}Changed?.Invoke();};
  Unloaded+=(_,_)=>{closed=true;serial++;request?.Cancel();};Loaded+=(_,_)=>closed=false;
 }
 void SetBusy(bool busy){SearchButton.IsEnabled=!busy;SearchButton.Content=busy?"正在搜索…":"搜索地点";}
 public async Task SearchAsync()
 {
  if(closed)return;request?.Cancel();request?.Dispose();request=new();var token=request.Token;long own=++serial;string query=QueryInput.Text.Trim();setting=true;Results.ItemsSource=null;setting=false;Results.Visibility=Visibility.Collapsed;Changed?.Invoke();
  if(query.Length==0){Feedback.Text="请先输入地点名称。";SetBusy(false);return;}
  Feedback.Foreground=Ui.Brush("#886047");Feedback.Text="正在搜索“"+query+"”…";SetBusy(true);
  try{var found=await search(query,token);if(closed||own!=serial||token.IsCancellationRequested)return;setting=true;Results.ItemsSource=found;Results.SelectedIndex=found.Count==1?0:-1;setting=false;Results.Visibility=found.Count>0?Visibility.Visible:Visibility.Collapsed;Feedback.Text=found.Count==0?"没有找到匹配地点，请尝试景点名称、入口或更短的关键词。":found.Count==1?"已选择："+found[0]+"\n"+found[0].Source:$"找到{found.Count}个地点，请点击准确名称/地址确认。";Feedback.Foreground=Ui.Brush(found.Count==0?"#A45243":"#55715F");Changed?.Invoke();}
  catch(OperationCanceledException){if(own==serial&&!closed)Feedback.Text="搜索已取消，可以重试。";}
  catch(Exception ex){if(own==serial&&!closed){Feedback.Text="搜索失败："+ex.Message;Feedback.Foreground=Ui.Brush("#A45243");}}
  finally{if(own==serial&&!closed)SetBusy(false);}
 }
}
