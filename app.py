import tkinter as tk
from tkinter import ttk, messagebox
import sqlite3, os, json, re, math, urllib.request, urllib.parse, datetime as dt, threading, calendar
from pathlib import Path

HOME = Path.home() / '.nanjing_trip_agent'
HOME.mkdir(exist_ok=True)
DB = HOME / 'trips.sqlite3'
CONFIG = HOME / 'config.json'
PLACES = {'南京南站':'118.8022,31.9745','南京站':'118.7978,32.0871','新街口':'118.7857,32.0417','夫子庙':'118.7886,32.0215','南京博物院':'118.8484,32.0448','玄武湖':'118.8030,32.0733','中山陵':'118.8529,32.0617','南京大学':'118.7792,32.0572','总统府':'118.8036,32.0444'}

def db():
    con = sqlite3.connect(DB)
    con.execute('CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY, day TEXT NOT NULL, title TEXT NOT NULL, location TEXT NOT NULL, coord TEXT NOT NULL, start INTEGER NOT NULL, duration INTEGER NOT NULL, fixed INTEGER NOT NULL DEFAULT 0)')
    columns = {r[1] for r in con.execute('PRAGMA table_info(events)')}
    if 'notes' not in columns: con.execute("ALTER TABLE events ADD COLUMN notes TEXT NOT NULL DEFAULT ''")
    return con

def settings():
    try: return json.loads(CONFIG.read_text(encoding='utf-8'))
    except (OSError, ValueError): return {}

def fetch(url, params):
    req = urllib.request.Request(url+'?'+urllib.parse.urlencode(params), headers={'User-Agent':'NanjingTripAgent/1.0'})
    with urllib.request.urlopen(req, timeout=12) as response: return json.load(response)

def geocode(place, key):
    if not key:
        for name, coord in PLACES.items():
            if name in place or place in name: return name, coord, '示例地点坐标'
        raise ValueError('未配置高德 Key：本地体验模式仅支持预置地点。请在设置中配置 Key。')
    data = fetch('https://restapi.amap.com/v3/place/text', {'key':key,'keywords':place,'city':'南京','citylimit':'true','offset':10,'page':1})
    if data.get('status') != '1': raise ValueError('高德地点查询失败：'+str(data.get('info')))
    pois = data.get('pois', [])
    valid = [p for p in pois if p.get('cityname') == '南京市' and isinstance(p.get('location'),str) and p.get('location')]
    if not valid: raise ValueError('未找到南京市内的地点，请提供更具体的地点名称。')
    p = valid[0]
    return p['name']+'（'+str(p.get('address') or p.get('adname') or '')+'）', p['location'], '高德地点搜索第一条结果，请核对'

def travel(a,b,key,mode):
    if a == b: return 0, '同一地点'
    if key:
        kind = {'驾车':'driving','步行':'walking','公交':'transit/integrated'}[mode]
        params = {'key':key,'origin':a,'destination':b}
        if mode == '公交': params.update(city='南京',cityd='南京')
        data = fetch('https://restapi.amap.com/v3/direction/'+kind, params)
        if data.get('status') != '1': raise ValueError('高德路线查询失败：'+str(data.get('info')))
        route=data.get('route',{})
        paths=route.get('transits' if mode=='公交' else 'paths',[])
        if not paths: raise ValueError('地图未找到路线，请调整交通方式或地点。')
        return math.ceil(float(paths[0]['duration'])/60), '高德实时估算'
    x1,y1=map(float,a.split(',')); x2,y2=map(float,b.split(','))
    lat=math.radians((y1+y2)/2); km=math.hypot((x1-x2)*111*math.cos(lat),(y1-y2)*111)
    speed={'驾车':25,'步行':4,'公交':18}[mode]
    return max(1,math.ceil(km*1.5/speed*60)+(10 if mode=='公交' else 0)), '直线距离粗估'

def parse_local(text, today=None):
    today=today or dt.date.today()
    if '后天' in text: day=today+dt.timedelta(days=2)
    elif '明天' in text: day=today+dt.timedelta(days=1)
    elif '今天' in text: day=today
    else:
        m=re.search(r'(\d{4})[-年/](\d{1,2})[-月/](\d{1,2})日?',text)
        n=re.search(r'(\d{1,2})月(\d{1,2})[日号]',text)
        if m: day=dt.date(*map(int,m.groups()))
        elif n: day=dt.date(today.year,*map(int,n.groups()))
        else: raise ValueError('请写明日期，例如“明天”或“2026-11-02”。')
    m=re.search(r'(?:在|去|到)([\u4e00-\u9fa5A-Za-z0-9]+?)(?:看|吃|参观|游览|开会|学习|逛|拍照|办|做|玩|，|,|\s)',text)
    if not m: raise ValueError('无法识别地点，请用“在南京博物院参观”这样的表达，或配置大模型。')
    place=m.group(1)
    dur=re.search(r'(\d+(?:\.\d+)?)\s*(个?小时|分钟)',text)
    if not dur: raise ValueError('请写明大致耗时，例如“2小时”或“90分钟”。')
    minutes=round(float(dur.group(1))*(60 if '小时' in dur.group(2) else 1))
    t=re.search(r'(上午|下午|晚上|中午)?\s*(\d{1,2})(?:点|:)(\d{1,2})?',text)
    start=None
    if t:
        hour=int(t.group(2)); minute=int(t.group(3) or 0)
        if t.group(1) in ('下午','晚上') and hour<12: hour+=12
        if t.group(1)=='中午' and hour<11: hour+=12
        if hour>23 or minute>59: raise ValueError('时间格式不正确。')
        start=hour*60+minute
    action=re.search(r'(看电影|参观|游览|开会|学习|逛街|拍照|吃饭|吃[^，,\s]*|看[^，,\s]*)',text[m.end(1):])
    activity=action.group(1) if action else '活动'
    title=activity+' · '+place
    return {'day':day.isoformat(),'location':place,'title':title,'duration':minutes,'start':start}

def parse(text,key):
    if not key: return parse_local(text)
    today=dt.date.today().isoformat()
    body={'model':'deepseek-flash','thinking':{'type':'disabled'},'messages':[{'role':'system','content':'你是行程信息提取器，只返回JSON对象，字段 day(YYYY-MM-DD), location(地点名称), title(包含具体活动及对象或地点的完整名称，如参观南京博物院，不要仅写参观), duration(整数分钟), start(当天零点起分钟数，未指定精确开始时间为null)。无法确定日期、地点或耗时则相应字段为null，不要猜测。仅提取，不规划。今天是'+today},{'role':'user','content':text}], 'response_format':{'type':'json_object'},'temperature':0}
    req=urllib.request.Request('https://api.deepseek.com/chat/completions',data=json.dumps(body,ensure_ascii=False).encode(),headers={'Authorization':'Bearer '+key,'Content-Type':'application/json'},method='POST')
    with urllib.request.urlopen(req,timeout=30) as response: result=json.load(response)
    return json.loads(result['choices'][0]['message']['content'])

def validate(e):
    try:
        dt.date.fromisoformat(str(e['day']))
        if not isinstance(e['title'],str) or not e['title'].strip() or not isinstance(e['location'],str) or not e['location'].strip(): raise ValueError()
        if isinstance(e['duration'],bool) or not isinstance(e['duration'],int) or not 1<=e['duration']<=720: raise ValueError()
        if e.get('start') is not None and (isinstance(e['start'],bool) or not isinstance(e['start'],int) or not 0<=e['start']<1440): raise ValueError()
    except (ValueError, KeyError, TypeError): raise ValueError('信息不完整或格式有误：请明确日期、地点、活动和 1–720 分钟的耗时。')

def plan(event, existing, travel_fn):
    """Find earliest feasible gap, keeping saved events fixed. Time window 08:00–22:00."""
    duration=event['duration']; specified=event.get('start'); best=None
    for i in range(len(existing)+1):
        prev=existing[i-1] if i else None; nxt=existing[i] if i<len(existing) else None
        before,bs=travel_fn(prev['coord'],event['coord']) if prev else (0,'无前序行程')
        after,asrc=travel_fn(event['coord'],nxt['coord']) if nxt else (0,'无后续行程')
        earliest=max(480, prev['start']+prev['duration']+before if prev else 480)
        latest=min(1320-duration,nxt['start']-after-duration if nxt else 1320-duration)
        candidate=specified if specified is not None else earliest
        if earliest<=candidate<=latest:
            best=(candidate,before,after,bs,asrc,prev,nxt); break
    if best is None: raise ValueError('无法在 08:00–22:00 内找到可行时段，或指定时间与已有行程及通行时间冲突。请调整日期、时间或耗时。')
    return best

def hm(m): return f'{m//60:02d}:{m%60:02d}'
def rows(day):
    with db() as c:
        c.row_factory=sqlite3.Row
        return [dict(r) for r in c.execute('SELECT * FROM events WHERE day=? ORDER BY start,id',(day,))]

class App(tk.Tk):
    def __init__(self):
        super().__init__(); self.title('南京行程 Agent'); self.geometry('1000x700'); self.minsize(800,580)
        self.configure(bg='#f5f3f0')
        self.option_add('*Font', ('Microsoft YaHei', 10))
        self.main=tk.Frame(self,bg='#f5f3f0',padx=36,pady=26); self.main.pack(fill='both',expand=True)
        top=tk.Frame(self.main,bg='#f5f3f0'); top.pack(fill='x')
        tk.Label(top,text='NANJING  /  PLANNER',bg='#f5f3f0',fg='#a09a91',font=('Arial',9,'bold')).pack(side='left')
        tk.Button(top,text='设置  ⚙',command=self.config_dialog,bg='#f5f3f0',fg='#56514d',relief='flat',cursor='hand2').pack(side='right')
        title=tk.Frame(self.main,bg='#f5f3f0'); title.pack(fill='x',pady=(20,14))
        tk.Label(title,text='我的日程',font=('Microsoft YaHei',25,'bold'),bg='#f5f3f0',fg='#252525').pack(side='left')
        tk.Label(title,text='把时间留给值得的事',font=('Microsoft YaHei',10),bg='#f5f3f0',fg='#a09a91').pack(side='left',padx=16,pady=(11,0))
        composer=tk.Frame(self.main,bg='white',padx=18,pady=14,highlightbackground='#e9e5df',highlightthickness=1); composer.pack(fill='x')
        tk.Label(composer,text='✦  告诉我你的计划',bg='white',fg='#d06d52',font=('Microsoft YaHei',11,'bold')).pack(anchor='w')
        self.input=tk.Text(composer,height=2,font=('Microsoft YaHei',11),wrap='word',relief='flat',bg='white',fg='#36322f',insertbackground='#d06d52'); self.input.pack(fill='x',pady=(10,5)); self.input.insert('1.0','明天在南京博物院参观，约2小时')
        bar=tk.Frame(composer,bg='white'); bar.pack(fill='x'); tk.Label(bar,text='出行方式',bg='white',fg='#918a82').pack(side='left'); self.mode=tk.StringVar(value='公交'); ttk.Combobox(bar,textvariable=self.mode,values=['公交','驾车','步行'],state='readonly',width=8).pack(side='left',padx=8)
        self.add=tk.Button(bar,text='  规划行程  →  ',command=self.submit,bg='#d8755b',fg='white',activebackground='#bc5e46',activeforeground='white',relief='flat',padx=12,pady=7,cursor='hand2'); self.add.pack(side='right'); self.status=tk.Label(bar,text='',bg='white',fg='#918a82'); self.status.pack(side='right',padx=10)
        row=tk.Frame(self.main,bg='#f5f3f0'); row.pack(fill='x',pady=(16,8))
        tk.Label(row,text='行程看板',font=('Microsoft YaHei',15,'bold'),bg='#f5f3f0',fg='#292725').pack(side='left')
        self.day=tk.StringVar(value=dt.date.today().isoformat())
        self.month_label=tk.Label(row,bg='#f5f3f0',fg='#514a44',font=('Microsoft YaHei',11,'bold')); self.month_label.pack(side='left',padx=(20,12))
        tk.Button(row,text='‹',command=lambda:self.shift(-1),bg='#f5f3f0',relief='flat',font=('Arial',17),fg='#7d756c',cursor='hand2').pack(side='left')
        tk.Button(row,text='›',command=lambda:self.shift(1),bg='#f5f3f0',relief='flat',font=('Arial',17),fg='#7d756c',cursor='hand2').pack(side='left')
        tk.Button(row,text='今天',command=self.today,bg='white',fg='#68605a',relief='flat',padx=12,pady=4,cursor='hand2').pack(side='left',padx=8)
        tk.Label(row,text='月视图  ·  滚动查看全部行程',bg='#f5f3f0',fg='#aaa299').pack(side='right')
        self.week_header=tk.Frame(self.main,bg='#f5f3f0'); self.week_header.pack(fill='x',padx=(0,15))
        self.canvas=tk.Canvas(self.main,bg='#f5f3f0',highlightthickness=0)
        scroll=ttk.Scrollbar(self.main,orient='vertical',command=self.canvas.yview); self.canvas.configure(yscrollcommand=scroll.set); scroll.pack(side='right',fill='y'); self.canvas.pack(fill='both',expand=True)
        self.gridframe=tk.Frame(self.canvas,bg='#eeeae5'); self.canvas_window=self.canvas.create_window((0,0),window=self.gridframe,anchor='nw')
        self.gridframe.bind('<Configure>',lambda e:self.canvas.configure(scrollregion=self.canvas.bbox('all')))
        self.canvas.bind('<Configure>',self.resize_board)
        self.canvas.bind_all('<MouseWheel>',self.on_wheel)
        self.refresh()
    def resize_board(self,event):
        self.canvas.itemconfigure(self.canvas_window,width=event.width)
        # Wrap text to fit each day without truncating card contents.
        for label in getattr(self,'wrapping_labels',[]):
            if label.winfo_exists(): label.configure(wraplength=max(60,event.width//7-26))
    def on_wheel(self,event):
        if self.canvas.winfo_exists(): self.canvas.yview_scroll(int(-event.delta/120),'units')
    def shift(self,n):
        date=dt.date.fromisoformat(self.day.get()); year,month=divmod(date.year*12+date.month-1+n,12)
        self.day.set(dt.date(year,month+1,1).isoformat()); self.refresh()
    def today(self):
        self.day.set(dt.date.today().isoformat()); self.refresh()
    def refresh(self):
        date=dt.date.fromisoformat(self.day.get()); weeks=calendar.Calendar(firstweekday=0).monthdatescalendar(date.year,date.month)
        first,last=weeks[0][0],weeks[-1][-1]
        self.month_label.config(text=f'{date.year} 年 {date.month} 月')
        for widget in self.gridframe.winfo_children(): widget.destroy()
        for widget in self.week_header.winfo_children(): widget.destroy()
        self.wrapping_labels=[]
        with db() as c:
            c.row_factory=sqlite3.Row
            data=[dict(r) for r in c.execute('SELECT * FROM events WHERE day BETWEEN ? AND ? ORDER BY day,start,id',(first.isoformat(),last.isoformat()))]
        by_day={}
        for e in data: by_day.setdefault(e['day'],[]).append(e)
        width=max(60,self.canvas.winfo_width()//7-26)
        for i in range(7):
            self.week_header.grid_columnconfigure(i,weight=1,uniform='week'); self.gridframe.grid_columnconfigure(i,weight=1,uniform='week')
            header=tk.Label(self.week_header,text=f"周{'一二三四五六日'[i]}",bg='#f5f3f0',fg='#77716a',font=('Microsoft YaHei',9,'bold'),pady=7)
            header.grid(row=0,column=i,sticky='ew')
        for old_row in range(6): self.gridframe.grid_rowconfigure(old_row,weight=0,minsize=0)
        for r,week in enumerate(weeks):
            self.gridframe.grid_rowconfigure(r,weight=0,minsize=88)
            for i,day in enumerate(week):
                items=by_day.get(day.isoformat(),[]); is_today=day==dt.date.today(); current=day.month==date.month
                bg='#f2efeb' if current else '#f8f6f3'
                column=tk.Frame(self.gridframe,bg=bg,padx=3,pady=5,highlightbackground='#e6e0da',highlightthickness=1)
                column.grid(row=r,column=i,sticky='nsew')
                day_label=tk.Label(column,text=str(day.day)+('  · 今天' if is_today else ''),bg=bg,fg='#d06d52' if is_today else ('#66605a' if current else '#c1b8af'),font=('Microsoft YaHei',9,'bold'),anchor='w')
                day_label.pack(fill='x',padx=3,pady=(0,5))
                for e in items:
                    card=tk.Frame(column,bg='white',padx=6,pady=7,highlightbackground='#e8dfd9',highlightthickness=1); card.pack(fill='x',pady=(0,6))
                    def add_label(text,fg,font):
                        label=tk.Label(card,text=text,bg='white',fg=fg,font=font,anchor='w',justify='left',wraplength=width)
                        label.pack(fill='x',anchor='w',pady=(0,3)); self.wrapping_labels.append(label)
                    add_label(hm(e['start'])+' – '+hm(e['start']+e['duration']),'#d06d52',('Arial',9,'bold'))
                    add_label(e['title'],'#302c29',('Microsoft YaHei',10,'bold'))
                    add_label('地点：'+e['location'],'#77716a',('Microsoft YaHei',9))
                    add_label('耗时：'+str(e['duration'])+' 分钟','#77716a',('Microsoft YaHei',9))
                    if e.get('notes'): add_label('备注：'+e['notes'],'#77716a',('Microsoft YaHei',9))
                    tk.Button(card,text='删除',command=lambda event_id=e['id']:self.delete(event_id),bg='white',fg='#a99a90',relief='flat',cursor='hand2',font=('Microsoft YaHei',8)).pack(anchor='e')
        self.canvas.yview_moveto(0)
    def delete(self,event_id):
        if messagebox.askyesno('删除行程','确定删除这项行程吗？',parent=self):
            with db() as c: c.execute('DELETE FROM events WHERE id=?',(event_id,))
            self.refresh()
    def config_dialog(self):
        win=tk.Toplevel(self); win.title('API 设置'); win.transient(self); win.grab_set(); frame=ttk.Frame(win,padding=20); frame.pack(fill='both',expand=True); cfg=settings(); values={}
        for i,(key,label) in enumerate([('deepseek_key','DeepSeek API Key'),('amap_key','高德 Web 服务 API Key')]):
            ttk.Label(frame,text=label).grid(row=i*2,column=0,sticky='w'); var=tk.StringVar(value=cfg.get(key,'')); values[key]=var; ttk.Entry(frame,textvariable=var,show='*',width=55).grid(row=i*2+1,column=0,pady=(3,12))
        ttk.Label(frame,text='密钥明文存储于本机用户目录，请勿分享配置文件。').grid(row=4,column=0,sticky='w')
        def save():
            CONFIG.write_text(json.dumps({k:v.get().strip() for k,v in values.items()},ensure_ascii=False),encoding='utf-8')
            try: CONFIG.chmod(0o600)
            except OSError: pass
            win.destroy()
        ttk.Button(frame,text='保存',command=save).grid(row=5,column=0,sticky='e',pady=(12,0))
    def submit(self):
        text=self.input.get('1.0','end').strip(); mode=self.mode.get(); cfg=settings()
        if not text: return
        self.add.config(state='disabled'); self.status.config(text='正在解析和计算路线…')
        def work():
            try:
                e=parse(text,cfg.get('deepseek_key')); validate(e); e['notes']=text; place,coord,note=geocode(e['location'],cfg.get('amap_key')); e['coord']=coord; e['location']=place
                existing=rows(e['day']); result=plan(e,existing,lambda a,b:travel(a,b,cfg.get('amap_key'),mode))
                self.after(0,lambda:self.confirm(e,result,note,mode))
            except Exception as ex:
                msg=str(ex); self.after(0,lambda:messagebox.showerror('无法规划',msg))
            finally: self.after(0,lambda:(self.add.config(state='normal'),self.status.config(text='')))
        threading.Thread(target=work,daemon=True).start()
    def confirm(self,e,result,note,mode):
        start,before,after,bs,src,prev,nxt=result
        info=f"日期：{e['day']}\n活动：{e['title']}\n备注：{e['notes']}\n地点：{e['location']}\n开始：{hm(start)}　结束：{hm(start+e['duration'])}\n耗时：{e['duration']} 分钟\n交通：{mode}\n地点来源：{note}\n"
        if prev: info+=f"\n上一行程 → 本行程：{before} 分钟（{bs}）"
        if nxt: info+=f"\n本行程 → 下一行程：{after} 分钟（{src}）"
        info+='\n\n确认保存？'
        if messagebox.askyesno('核对规划',info):
            # Recompute against current saved state to prevent stale confirmation.
            try:
                cfg=settings(); revised=plan(e,rows(e['day']),lambda a,b:travel(a,b,cfg.get('amap_key'),mode))
                if revised[0]!=start: raise ValueError('行程已有变化，请重新规划。')
                with db() as c: c.execute('INSERT INTO events(day,title,location,coord,start,duration,fixed,notes) VALUES(?,?,?,?,?,?,?,?)',(e['day'],e['title'],e['location'],e['coord'],start,e['duration'],int(e.get('start') is not None),e['notes']))
                self.day.set(e['day']); self.refresh()
            except Exception as ex: messagebox.showerror('保存失败',str(ex))

if __name__=='__main__': App().mainloop()
