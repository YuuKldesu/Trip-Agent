using System.IO;
using System.Windows;
using System.Windows.Controls;
using Planner.Data;
using Planner.Desktop;

static class ResponsiveShellChecks
{
    public static async Task Run(MainWindow owner,string directory,Action<bool,string> check)
    {
        var vm=new PlannerViewModel(Path.Combine(directory,"responsive-shell"),false);
        var window=new MainWindow(vm){Owner=owner};window.Show();window.SetLayoutMode("FULL");vm.LoadSample();
        try
        {
            var board=Walk(window).OfType<ScheduleBoard>().Single();board.SetView("MONTH");
            var scroll=Walk(board).OfType<ScrollViewer>().Single(s=>Equals(s.Tag,"MonthViewport"));
            foreach(double font in new[]{14d,22d})foreach(double width in new[]{1120d,1480d})foreach(double assistant in new[]{280d,500d})
            {
                vm.UpdateAppearance(p=>{p.FontSize=font;p.AssistantWidth=assistant;});window.Width=width;window.Height=900;await Task.Delay(60);window.UpdateLayout();
                scroll=Walk(board).OfType<ScrollViewer>().Single(s=>Equals(s.Tag,"MonthViewport"));var month=(Grid)scroll.Content;
                var sunday=month.Children.OfType<Border>().First(c=>Grid.GetColumn(c)==6);var right=sunday.TranslatePoint(new Point(sunday.ActualWidth,0),scroll).X;
                check(scroll.ScrollableWidth<1&&month.ActualWidth<=scroll.ViewportWidth+1&&right<=scroll.ActualWidth+1&&month.ColumnDefinitions.All(c=>Math.Abs(c.ActualWidth-month.ActualWidth/7)<1),$"{width}px窗口/{assistant}px助手/{font}号字时七列完整显示");
                var model=Walk(window).OfType<TextBlock>().Single(t=>Equals(t.Tag,"AssistantModelLabel"));
                check(model.TextWrapping==TextWrapping.Wrap&&model.TextTrimming==TextTrimming.None&&model.ActualWidth>50,"窄助手模型提示完整换行显示");
            }
            vm.UpdateAppearance(p=>p.AssistantWidth=280);window.Width=1480;await Task.Delay(50);
            var panel=Walk(window).OfType<GuidedAssistantPanel>().Single();var input=Walk(panel).OfType<TextBox>().Single(t=>Equals(t.Tag,"ConversationInput"));input.Text="测试窄侧栏输入框";
            var send=Walk(panel).OfType<Button>().Single(b=>Equals(b.Tag,"ConversationSend"));var edge=send.TranslatePoint(new Point(send.ActualWidth,0),panel).X;
            check(send.IsEnabled&&edge<=panel.ActualWidth+1,"280px助手中输入与发送按钮可达");
        }
        finally{window.Close();Typography.Apply(14);}
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
