using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Planner.Core;

namespace Planner.Desktop;
public sealed class CalendarView : Grid
{
    readonly PlannerViewModel vm;readonly Canvas canvas=new(){Height=1440,Background=Brushes.White};readonly Grid header=new(){Height=54};readonly ScrollViewer scroll;
    const double Gutter=52,Hour=60;
    public event Action<DateTime>? AddRequested;public event Action<Occurrence>? EditRequested;public event Action<Occurrence>? Selected;public event Action<Occurrence>? DeleteRequested;
    public CalendarView(PlannerViewModel model)
    {
        vm=model;RowDefinitions.Add(new(){Height=new(54)});RowDefinitions.Add(new());
        Children.Add(header);scroll=new(){Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);Children.Add(scroll);
        scroll.Loaded+=(_,_)=>scroll.ScrollToVerticalOffset(7*Hour);SizeChanged+=(_,_)=>Render();vm.Changed+=Render;
        canvas.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is Canvas||e.OriginalSource is Line||e.OriginalSource is Rectangle){var point=e.GetPosition(canvas);var day=(int)((point.X-Gutter)/ColWidth);if(day is >=0 and <7){vm.SelectedDate=vm.Week.AddDays(day);AddRequested?.Invoke(vm.SelectedDate.ToDateTime(TimeOnly.MinValue).AddMinutes(Math.Clamp(Math.Floor(point.Y/Hour*4)*15,0,1425)));}}};
    }
    double ColWidth=>Math.Max(40,(Math.Max(400,ActualWidth)-Gutter-18)/7);
    public void Render()
    {
        if(ActualWidth<100)return;
        canvas.Children.Clear();header.Children.Clear();header.ColumnDefinitions.Clear();header.ColumnDefinitions.Add(new(){Width=new(Gutter)});
        var items=vm.WeekItems();var problems=vm.WeekConflicts();var conflicts=problems.SelectMany(p=>new[]{p.FirstKey,p.SecondKey}).Where(k=>k is not null).ToHashSet();
        for(var d=0;d<7;d++)
        {
            var day=vm.Week.AddDays(d);header.ColumnDefinitions.Add(new(){Width=new(ColWidth)});
            var b=Ui.Button($"{new[]{"周一","周二","周三","周四","周五","周六","周日"}[d]}  {day:MM/dd}",()=>{vm.SelectedDate=day;vm.Notify();});b.Padding=new(4);b.Background=Ui.Brush(day==vm.SelectedDate?"#EDD4BA":"#FBF7F0");Grid.SetColumn(b,d+1);header.Children.Add(b);
            if(d>=5){var fill=new Rectangle{Width=ColWidth,Height=1440,Fill=Ui.Brush("#FCF9F3")};Canvas.SetLeft(fill,Gutter+d*ColWidth);canvas.Children.Add(fill);}
            var v=new Line{X1=Gutter+d*ColWidth,X2=Gutter+d*ColWidth,Y1=0,Y2=1440,Stroke=Ui.Brush("#E9DED2")};canvas.Children.Add(v);
        }
        for(int hour=0;hour<=24;hour++)
        {
            var y=hour*Hour;canvas.Children.Add(new Line{X1=Gutter,X2=Gutter+ColWidth*7,Y1=y,Y2=y,Stroke=Ui.Brush("#E9DED2")});
            if(hour<24){var label=Ui.Text($"{hour:00}:00",11);label.Foreground=Ui.Brush("#927C68");Canvas.SetTop(label,y+3);Canvas.SetLeft(label,4);canvas.Children.Add(label);}
        }
        for(var d=0;d<7;d++)
        {
            var dayStart=vm.Week.AddDays(d).ToDateTime(TimeOnly.MinValue);var end=dayStart.AddDays(1);
            var pieces=items.Where(o=>o.Start<end&&o.End>dayStart).OrderBy(o=>o.Start).ToList();
            var group=new List<Occurrence>();DateTime groupEnd=DateTime.MinValue;
            void Flush()
            {
                var lanes=new List<DateTime>();var assigned=new List<(Occurrence,int)>();
                foreach(var o in group){var lane=lanes.FindIndex(t=>t<=o.Start);if(lane<0){lane=lanes.Count;lanes.Add(o.End);}else lanes[lane]=o.End;assigned.Add((o,lane));}
                foreach(var (o,lane) in assigned)DrawCard(o,d,dayStart,end,lane,lanes.Count,conflicts.Contains(o.Key));group.Clear();
            }
            foreach(var o in pieces){if(group.Count>0&&o.Start>=groupEnd)Flush();group.Add(o);groupEnd=group.Count==1?o.End:(o.End>groupEnd?o.End:groupEnd);}Flush();
        }
        if(Clock.Today>=vm.Week&&Clock.Today<vm.Week.AddDays(7))
        {var d=Clock.Today.DayNumber-vm.Week.DayNumber;var y=Clock.Now.TimeOfDay.TotalHours*Hour;canvas.Children.Add(new Line{X1=Gutter+d*ColWidth,X2=Gutter+(d+1)*ColWidth,Y1=y,Y2=y,Stroke=Ui.Brush("#BA6847"),StrokeThickness=2,IsHitTestVisible=false});}
    }
    void DrawCard(Occurrence o,int day,DateTime dayStart,DateTime dayEnd,int lane,int lanes,bool conflict)
    {
        var start=o.Start>dayStart?o.Start:dayStart;var end=o.End<dayEnd?o.End:dayEnd;
        var table=vm.State.Tables.First(t=>t.Id==o.Activity.TableId);var h=Math.Max(16,(end-start).TotalHours*Hour-3);var w=Math.Max(20,(ColWidth-8)/lanes-3);
        bool active=table.Active(DateOnly.FromDateTime(o.Start));
        var content=new StackPanel{Margin=new(7,4,5,2),IsHitTestVisible=false};var heading=Ui.Text((active?conflict?"! ":"":"未生效 · ")+o.Title,12,true);heading.Margin=new(0,0,0,3);heading.MaxHeight=h>44?30:Math.Max(10,h-8);heading.TextTrimming=TextTrimming.CharacterEllipsis;content.Children.Add(heading);
        if(h>44){var time=Ui.Text($"{start:HH:mm}—{end:HH:mm}",10);time.Margin=new(0,0,0,3);content.Children.Add(time);}if(h>77){var location=Ui.Text(vm.State.PlaceName(o.PlaceId),10);location.Margin=new(0);content.Children.Add(location);}
        var card=new Border{Width=w,Height=h,CornerRadius=new(7),Background=Ui.Brush(table.Color),BorderBrush=Ui.Brush(conflict?"#B76845":"#D7BEA6"),BorderThickness=new(conflict?2:1),Child=content,ClipToBounds=true,Cursor=Cursors.Hand,ToolTip=$"{o.Title}\n{o.Start:MM-dd HH:mm} — {o.End:MM-dd HH:mm}\n{vm.State.PlaceName(o.PlaceId)}\n{table.Name}\n双击编辑；拖动改时间；上下边缘改时长"};
        Canvas.SetLeft(card,Gutter+day*ColWidth+4+lane*(ColWidth-8)/lanes);Canvas.SetTop(card,(start-dayStart).TotalHours*Hour+2);canvas.Children.Add(card);
        var menu=new ContextMenu();var edit=new MenuItem{Header="编辑行程"};edit.Click+=(_,_)=>EditRequested?.Invoke(o);menu.Items.Add(edit);var delete=new MenuItem{Header="删除这次行程"};delete.Click+=(_,_)=>DeleteRequested?.Invoke(o);menu.Items.Add(delete);card.ContextMenu=menu;
        Point origin=new();bool dragging=false;string mode="move";
        card.MouseLeftButtonDown+=(_,e)=>{Selected?.Invoke(o);if(e.ClickCount==2){EditRequested?.Invoke(o);e.Handled=true;return;}origin=e.GetPosition(canvas);var local=e.GetPosition(card);mode=local.Y<7&&start==o.Start?"top":local.Y>h-7&&end==o.End?"bottom":"move";dragging=true;card.CaptureMouse();e.Handled=true;};
        card.MouseMove+=(_,e)=>{if(dragging&&e.LeftButton==MouseButtonState.Pressed){var p=e.GetPosition(canvas);if(Math.Abs(p.Y-origin.Y)>3||Math.Abs(p.X-origin.X)>3)card.Opacity=.6;}};
        card.MouseLeftButtonUp+=(_,e)=>
        {
            if(!dragging)return;dragging=false;card.ReleaseMouseCapture();card.Opacity=1;var p=e.GetPosition(canvas);var dy=Math.Round((p.Y-origin.Y)/Hour*4)*15;var dx=Math.Clamp((int)((p.X-Gutter)/ColWidth),0,6)-day;
            if(Math.Abs(p.Y-origin.Y)<4&&Math.Abs(p.X-origin.X)<4)return;
            Ui.Safe(()=>{
                var a=JsonKit.Clone(o.Activity);a.Title=o.Title;a.PlaceId=o.PlaceId;a.StartAt=mode=="bottom"?o.Start:o.Start.AddMinutes(dy).AddDays(mode=="move"?dx:0);a.EndAt=mode=="top"?o.End:o.End.AddMinutes(dy).AddDays(mode=="move"?dx:0);a.DurationMin=(int)(a.EndAt-a.StartAt)!.Value.TotalMinutes;
                var setting=vm.State.Reminders.FirstOrDefault(r=>r.ActivityId==a.Id);var cmd=new EditCommand{Activity=a,OriginDate=o.OriginDate,ReminderEnabled=setting?.Enabled??false,ReminderLeadMin=setting?.LeadMin??15};var next=ChangeService.Apply(vm.State,[cmd],vm.State.Revision);
                var warnings=CalendarEngine.AllConflicts(next,a.StartAt.Value.Date.AddDays(-1),a.EndAt.Value.Date.AddDays(2));
                if(Ui.Confirm($"将“{o.Title}”调整为\n{a.StartAt:MM-dd HH:mm} — {a.EndAt:MM-dd HH:mm}\n"+(o.OriginDate is null?"":"仅影响这一次。\n")+string.Join("\n",warnings.Take(3).Select(x=>x.Message))))vm.Persist(next);
            });Render();e.Handled=true;
        };
    }
}
