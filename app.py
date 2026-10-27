import tkinter as tk
import sys
from planner_panel import PlannerPanel
from tkinter import ttk, messagebox, simpledialog
import sqlite3, os, json, re, math, urllib.request, urllib.parse, urllib.error, datetime as dt, threading, calendar, ssl, socket, time
from pathlib import Path

HOME = Path.home() / '.nanjing_trip_agent'
HOME.mkdir(exist_ok=True)
DB = HOME / 'trips.sqlite3'
CONFIG = HOME / 'config.json'
PLACES = {'南京南站':'118.8022,31.9745','南京站':'118.7978,32.0871','新街口':'118.7857,32.0417','夫子庙':'118.7886,32.0215','南京博物院':'118.8484,32.0448','玄武湖':'118.8030,32.0733','中山陵':'118.8529,32.0617','南京大学':'118.7792,32.0572','总统府':'118.8036,32.0444'}

THEMES = {
    '暖珊瑚': {'accent':'#c65b40','hover':'#a94831','tint':'#faece6'},
    '湖水蓝': {'accent':'#247a87','hover':'#1a5d67','tint':'#e4f1f2'},
    '森林绿': {'accent':'#397553','hover':'#28573b','tint':'#e7f1e9'},
    '薰衣草紫': {'accent':'#715997','hover':'#584477','tint':'#f0eaf7'},
    '深海蓝': {'accent':'#315f98','hover':'#234775','tint':'#e6eef9'},
}
def theme_for(name): return THEMES.get(name, THEMES['暖珊瑚'])

def db():
    con = sqlite3.connect(DB)
    con.execute('CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY, day TEXT NOT NULL, title TEXT NOT NULL, location TEXT NOT NULL, coord TEXT NOT NULL, start INTEGER NOT NULL, duration INTEGER NOT NULL, fixed INTEGER NOT NULL DEFAULT 0)')
    columns = {r[1] for r in con.execute('PRAGMA table_info(events)')}
    if 'notes' not in columns: con.execute("ALTER TABLE events ADD COLUMN notes TEXT NOT NULL DEFAULT ''")
    if 'completed' not in columns: con.execute('ALTER TABLE events ADD COLUMN completed INTEGER NOT NULL DEFAULT 0')
    return con

def settings():
    try: return json.loads(CONFIG.read_text(encoding='utf-8'))
    except (OSError, ValueError): return {}

def bailian_url(region, host=''):
    hosts={'北京':'dashscope.aliyuncs.com','新加坡':'dashscope-intl.aliyuncs.com'}
    if region not in hosts: raise ValueError('百炼地域配置无效，请在设置中重新选择。')
    host=host.strip().lower()
    if host:
        host=host.removeprefix('https://').rstrip('/')
        if host.endswith('/compatible-mode/v1'): host=host[:-len('/compatible-mode/v1')]
        suffix={'北京':'.cn-beijing.maas.aliyuncs.com','新加坡':'.ap-southeast-1.maas.aliyuncs.com'}[region]
        if not re.fullmatch(r'[a-z0-9-]+'+re.escape(suffix),host):
            raise ValueError('百炼 API Host 格式或地域不正确，请复制控制台中的业务空间专属域名。')
    else: host=hosts[region]
    return 'https://'+host+'/compatible-mode/v1/chat/completions'

def request_json(req, stage, timeout=12, retries=0):
    for attempt in range(retries+1):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as response:
                return json.load(response)
        except urllib.error.HTTPError as exc:
            hint = ('身份验证失败，请核对 API Key、服务商和地域。' if exc.code==401 else '账户余额不足，请检查余额。' if exc.code==402 else '请检查服务权限和配置。')
            code=''
            try:
                payload=json.loads(exc.read(65536)); error=payload.get('error',{})
                raw=error.get('code','') if isinstance(error,dict) else ''
                if isinstance(raw,str) and re.fullmatch(r'[A-Za-z0-9_.-]{1,80}',raw): code=f'\n服务错误代码：{raw}'
            except (ValueError,TypeError,AttributeError): pass
            raise ValueError(f'{stage}：服务返回 HTTP {exc.code}。{hint}{code}') from exc
        except (urllib.error.URLError, ssl.SSLError, socket.timeout, ConnectionError, TimeoutError) as exc:
            reason=getattr(exc,'reason',exc)
            temporary=isinstance(reason,(ssl.SSLEOFError, socket.timeout, ConnectionError, TimeoutError)) or 'UNEXPECTED_EOF_WHILE_READING' in str(reason)
            if attempt < retries and temporary:
                time.sleep(0.4*(attempt+1)); continue
            if isinstance(reason,ssl.SSLCertVerificationError):
                hint='证书验证失败，请检查系统时间、代理或杀毒软件的 HTTPS 检查设置。'
            elif 'UNEXPECTED_EOF_WHILE_READING' in str(reason) or isinstance(reason,ssl.SSLEOFError):
                hint='HTTPS 连接被提前中断，可能与网络、代理或服务暂时不可用有关。请检查代理设置，或切换网络后重试。'
            else:
                hint='无法连接服务，请检查网络、代理和防火墙设置后重试。'
            raise ValueError(f'{stage}：{hint}\n错误类型：{type(reason).__name__}') from exc

def fetch(url, params, stage='高德请求'):
    req = urllib.request.Request(url+'?'+urllib.parse.urlencode(params), headers={'User-Agent':'NanjingTripAgent/1.0'})
    return request_json(req,stage,timeout=12,retries=2)

def find_places(place,key):
    if not key:
        matches=[{'name':name,'coord':coord,'note':'本地示例坐标，未核实校区或具体入口'} for name,coord in PLACES.items() if name in place or place in name]
        if not matches: raise ValueError('未配置高德 Key：本地模式仅支持预置地点。请配置高德 Key。')
        return matches
    data=fetch('https://restapi.amap.com/v3/place/text',{'key':key,'keywords':place,'city':'南京','citylimit':'true','offset':25,'page':1},'高德地点查询')
    if data.get('status')!='1': raise ValueError('高德地点查询失败：'+str(data.get('info')))
    matches=[]
    for p in data.get('pois',[]):
        if p.get('cityname')!='南京市' or not isinstance(p.get('location'),str) or not p['location']: continue
        name=p.get('name') or '未命名地点'; address=p.get('address') or p.get('adname') or ''
        if not isinstance(address,str): address=''
        matches.append({'name':name+('（'+address+'）' if address else ''),'coord':p['location'],'note':'高德地点搜索结果'})
    if not matches: raise ValueError('未找到南京市内的地点，请输入更具体的地点名称。')
    return matches

def geocode(place,key):
    candidates=find_places(place,key)
    if len(candidates)!=1: raise ValueError('地点存在多个搜索结果，请选择具体地点。')
    p=candidates[0]; return p['name'],p['coord'],p['note']

def travel(a,b,key,mode):
    if a == b: return 0, '同一地点'
    if key:
        kind = {'驾车':'driving','步行':'walking','公交':'transit/integrated','骑行':'bicycling'}[mode]
        params = {'key':key,'origin':a,'destination':b}
        if mode == '公交': params.update(city='南京',cityd='南京')
        prefix='v4' if mode=='骑行' else 'v3'
        data = fetch('https://restapi.amap.com/'+prefix+'/direction/'+kind, params, '高德路线查询')
        if mode=='骑行':
            if data.get('errcode') != 0: raise ValueError('高德骑行路线查询失败：'+str(data.get('errmsg') or data.get('info') or data.get('errcode')))
            route=data.get('data',{})
        else:
            if data.get('status') != '1': raise ValueError('高德路线查询失败：'+str(data.get('info')))
            route=data.get('route',{})
        paths=route.get('transits' if mode=='公交' else 'paths',[])
        if not paths: raise ValueError('地图未找到路线，请调整交通方式或地点。')
        return math.ceil(float(paths[0]['duration'])/60), '高德实时估算'
    x1,y1=map(float,a.split(',')); x2,y2=map(float,b.split(','))
    lat=math.radians((y1+y2)/2); km=math.hypot((x1-x2)*111*math.cos(lat),(y1-y2)*111)
    speed={'驾车':25,'步行':4,'公交':18,'骑行':12}[mode]
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

def parse(text, cfg, multiple=False):
    provider=cfg.get('llm_provider') or ('DeepSeek' if cfg.get('deepseek_key') else '本地规则')
    if provider=='本地规则':
        if multiple and re.search(r'然后|接着|最后|再去|之后|随后|再到|回到|返回',text):
            raise ValueError('多项活动或返程需要选择阿里云百炼或 DeepSeek 解析；本地规则暂不支持，请在设置中切换。')
        result=parse_local(text)
        return {'events':[result]} if multiple else result
    if provider=='DeepSeek':
        key=cfg.get('deepseek_key','').strip(); stage='DeepSeek 解析'; url='https://api.deepseek.com/chat/completions'
        extra={'model':'deepseek-flash','thinking':{'type':'disabled'}}
    elif provider=='阿里云百炼':
        key=cfg.get('bailian_key','').strip(); stage='阿里云百炼解析'
        region=cfg.get('bailian_region','北京')
        url=bailian_url(region,cfg.get('bailian_host',''))
        extra={'model':'qwen-plus','enable_thinking':False}
    else: raise ValueError('大模型服务配置无效，请在设置中重新选择。')
    if not key: raise ValueError(f'请选择的 {provider} 未填写 API Key，请到设置填写，或选择本地规则。')
    today=dt.date.today().isoformat()
    body={**extra,'messages':[{'role':'system','content':'你是行程信息提取器，只返回JSON对象，字段 day(YYYY-MM-DD), location(地点名称), title(包含具体活动及对象或地点的完整名称，如参观南京博物院，不要仅写参观), duration(整数分钟), start(当天零点起分钟数，未指定精确开始时间为null)。无法确定日期、地点或耗时则相应字段为null，不要猜测。仅提取，不规划。今天是'+today},{'role':'user','content':text}], 'response_format':{'type':'json_object'},'temperature':0}
    if multiple:
        body['messages'][0]['content']=(
            '只返回JSON对象，包含 events 数组，按用户要求的顺序逐项完整提取，每项字段：'
            'day YYYY-MM-DD、title、location、duration整数分钟、start整数(精确开始时间距午夜的分钟，未指定则null)、end整数（明确指定结束时刻距午夜的分钟，否则null）、'
            'earliest整数(最早开始时刻，早上为480，下午为720，未限制为null)、'
            'latest_end整数(必须结束或到达的最晚分钟，未限制为null)、'
            'notes(仅针对该项活动的简短中文摘要，提取用户明确提到的具体要求、同行人或限制，不重复完整输入、不虚构；无额外细节时为空字符串)、'
            'mode_in(到此活动的交通方式，仅公交、步行、骑行、驾车之一，未指定为null)、'
            'is_return布尔值（回到某地为true，该项duration为0）；日期继承用户已说明的日期。'
            '例如后天早上去夫子庙玩2小时，然后步行去老门东30分钟，最后坐地铁回南京大学下午六点前回来：'
            '三项分别为夫子庙、老门东、返回南京大学，后两项mode_in分别为步行和公交，返回项latest_end为1080。'
            '不要遗漏返程或截止时间；未指定上午或下午的截止时间不要猜测，填写null；需要用户补充的截止时间在该项增加 deadline_missing:true。时间未说明使用null。今天是'+today)
    req=urllib.request.Request(url,data=json.dumps(body,ensure_ascii=False).encode(),headers={'Authorization':'Bearer '+key,'Content-Type':'application/json'},method='POST')
    result=request_json(req,stage,timeout=30)
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
        # Skip gaps that cannot fit even before adding travel time.
        base_start=max(480,event.get('earliest') or 0,prev['start']+prev['duration'] if prev else 480)
        base_end=min(1320-duration,(event.get('latest_end') if event.get('latest_end') is not None else 1320)-duration,nxt['start']-duration if nxt else 1320-duration)
        if base_start>base_end or (specified is not None and not base_start<=specified<=base_end): continue
        before,bs=travel_fn(prev['coord'],event['coord']) if prev else (0,'无前序行程')
        after,asrc=travel_fn(event['coord'],nxt['coord']) if nxt else (0,'无后续行程')
        earliest=max(480,event.get('earliest') or 0,prev['start']+prev['duration']+before if prev else 480)
        latest=min(1320-duration,(event.get('latest_end') if event.get('latest_end') is not None else 1320)-duration,nxt['start']-after-duration if nxt else 1320-duration)
        candidate=specified if specified is not None else earliest
        if earliest<=candidate<=latest:
            best=(candidate,before,after,bs,asrc,prev,nxt); break
    if best is None: raise ValueError('无法在 08:00–22:00 内找到可行时段，或指定时间与已有行程及通行时间冲突。请调整日期、时间或耗时。')
    return best

def hm(m): return f'{m//60:02d}:{m%60:02d}'
def parse_clock(value):
    match=re.fullmatch(r'(\d{1,2}):(\d{2})',value.strip())
    if not match: raise ValueError('开始时间请使用 HH:MM 格式，例如 09:30。')
    hour,minute=map(int,match.groups())
    if hour>23 or minute>59: raise ValueError('开始时间无效。')
    return hour*60+minute

def duration_between(start,end):
    a=parse_clock(start); b=parse_clock(end)
    if b<=a: raise ValueError('结束时间必须晚于开始时间，暂不支持跨天。')
    if b-a>720: raise ValueError('活动耗时不能超过 720 分钟。')
    return b-a

def short_gap(previous, current):
    if previous['day'] != current['day']: return None
    gap=current['start']-(previous['start']+previous['duration'])
    return gap if 0 <= gap < 60 else None
def rows(day):
    with db() as c:
        c.row_factory=sqlite3.Row
        return [dict(r) for r in c.execute('SELECT * FROM events WHERE day=? ORDER BY start,id',(day,))]

def visible_list_date(headings,top):
    if not headings: return None
    chosen=headings[0][0]
    for date,y in headings:
        if y<=top+10: chosen=date
        else: break
    return chosen

def all_rows():
    with db() as c:
        c.row_factory=sqlite3.Row
        return [dict(r) for r in c.execute('SELECT * FROM events ORDER BY day,start,id')]

def update_completed(event_id,completed):
    with db() as c: c.execute('UPDATE events SET completed=? WHERE id=?',(int(bool(completed)),event_id))

class App(tk.Tk):
    def __init__(self):
        super().__init__(); self.title('南京行程 Agent'); self.geometry('1000x700'); self.minsize(800,580)
        self.theme=theme_for(settings().get('theme'))
        self.configure(bg='#f5f3f0')
        self.option_add('*Font', ('Microsoft YaHei', 10))
        self.main=tk.Frame(self,bg='#f5f3f0',padx=30,pady=14); self.main.pack(fill='both',expand=True)
        top=tk.Frame(self.main,bg='#f5f3f0'); top.pack(fill='x')
        tk.Label(top,text='NANJING  /  PLANNER',bg='#f5f3f0',fg='#a09a91',font=('Arial',9,'bold')).pack(side='left')
        tk.Button(top,text='设置  ⚙',command=self.config_dialog,bg='#f5f3f0',fg='#56514d',relief='flat',cursor='hand2').pack(side='right')
        title=tk.Frame(self.main,bg='#f5f3f0'); title.pack(fill='x',pady=(8,8))
        tk.Label(title,text='我的日程',font=('Microsoft YaHei',25,'bold'),bg='#f5f3f0',fg='#252525').pack(side='left')
        tk.Label(title,text='把时间留给值得的事',font=('Microsoft YaHei',10),bg='#f5f3f0',fg='#a09a91').pack(side='left',padx=16,pady=(11,0))
        composer=tk.Frame(self.main,bg='white',padx=14,pady=7,highlightbackground='#e9e5df',highlightthickness=1); composer.pack(fill='x')
        self.composer_label=tk.Label(composer,text='✦  告诉我你的计划',bg='white',fg=self.theme['accent'],font=('Microsoft YaHei',11,'bold')); self.composer_label.pack(anchor='w')
        self.input=tk.Text(composer,height=2,font=('Microsoft YaHei',11),wrap='word',relief='flat',bg='white',fg='#36322f',insertbackground=self.theme['accent']); self.input.pack(fill='x',pady=(4,2)); self.input.insert('1.0','明天在南京博物院参观，约2小时')
        bar=tk.Frame(composer,bg='white'); bar.pack(fill='x')
        self.add=tk.Button(bar,text='  规划行程  →  ',command=self.submit,bg=self.theme['accent'],fg='white',activebackground=self.theme['hover'],activeforeground='white',relief='flat',padx=10,pady=4,cursor='hand2'); self.add.pack(side='right'); self.status=tk.Label(bar,text='',bg='white',fg='#918a82'); self.status.pack(side='right',padx=10)
        row=tk.Frame(self.main,bg='#f5f3f0'); row.pack(fill='x',pady=(16,8))
        tk.Label(row,text='行程看板',font=('Microsoft YaHei',15,'bold'),bg='#f5f3f0',fg='#292725').pack(side='left')
        self.day=tk.StringVar(value=dt.date.today().isoformat())
        self.month_label=tk.Label(row,bg='#f5f3f0',fg='#514a44',font=('Microsoft YaHei',11,'bold')); self.month_label.pack(side='left',padx=(20,12))
        self.month_controls=tk.Frame(row,bg='#f5f3f0'); self.month_controls.pack(side='left')
        tk.Button(self.month_controls,text='‹',command=lambda:self.shift(-1),bg='#f5f3f0',relief='flat',font=('Arial',17),fg='#7d756c',cursor='hand2').pack(side='left')
        tk.Button(self.month_controls,text='›',command=lambda:self.shift(1),bg='#f5f3f0',relief='flat',font=('Arial',17),fg='#7d756c',cursor='hand2').pack(side='left')
        tk.Button(self.month_controls,text='今天',command=self.today,bg='white',fg='#68605a',relief='flat',padx=12,pady=4,cursor='hand2').pack(side='left',padx=8)
        self.view=tk.StringVar(value='month')
        self.list_button=tk.Button(row,text='列表',command=lambda:self.set_view('list'),bg='white',fg='#68605a',relief='flat',padx=10,pady=5,cursor='hand2'); self.list_button.pack(side='right',padx=(3,0))
        self.month_button=tk.Button(row,text='月历',command=lambda:self.set_view('month'),bg='white',fg='#68605a',relief='flat',padx=10,pady=5,cursor='hand2'); self.month_button.pack(side='right')
        self.week_header=tk.Frame(self.main,bg='#f5f3f0'); self.week_header.pack(fill='x',padx=(0,15))
        self.canvas=tk.Canvas(self.main,bg='#f5f3f0',highlightthickness=0)
        self.scroll=ttk.Scrollbar(self.main,orient='vertical',command=self.canvas.yview); self.canvas.configure(yscrollcommand=self.on_scroll); self.scroll.pack(side='right',fill='y'); self.canvas.pack(fill='both',expand=True)
        self.gridframe=tk.Frame(self.canvas,bg='#eeeae5'); self.canvas_window=self.canvas.create_window((0,0),window=self.gridframe,anchor='nw')
        self.gridframe.bind('<Configure>',lambda e:self.canvas.configure(scrollregion=self.canvas.bbox('all')))
        self.canvas.bind('<Configure>',self.resize_board)
        self.canvas.bind_all('<MouseWheel>',self.on_wheel)
        self.refresh()
    def on_scroll(self,first,last):
        self.scroll.set(first,last)
        if self.view.get()=='list':
            if not getattr(self,'_scroll_update_pending',False):
                self._scroll_update_pending=True; self.after_idle(self.update_list_date)
    def update_list_date(self):
        self._scroll_update_pending=False
        if self.view.get()!='list': return
        headings=getattr(self,'list_headings',[])
        if not headings: self.month_label.config(text='全部行程'); return
        top=self.canvas.canvasy(0)
        positions=[(date,heading.winfo_y()+heading.master.winfo_y()) for date,heading in headings if heading.winfo_exists()]
        chosen=visible_list_date(positions,top)
        if chosen is None: self.month_label.config(text='全部行程'); return
        d=dt.date.fromisoformat(chosen)
        self.month_label.config(text=f"{d.year} 年 {d.month} 月 {d.day} 日  ·  周{'一二三四五六日'[d.weekday()]}")
    def resize_board(self,event):
        self.canvas.itemconfigure(self.canvas_window,width=event.width)
        # Wrap text to fit each day without truncating card contents.
        for label in getattr(self,'wrapping_labels',[]):
            if label.winfo_exists(): label.configure(wraplength=max(60,event.width//7-26) if self.view.get()=='month' else max(100,event.width-175))
    def on_wheel(self,event):
        if self.canvas.winfo_exists(): self.canvas.yview_scroll(int(-event.delta/120),'units')
    def shift(self,n):
        date=dt.date.fromisoformat(self.day.get()); year,month=divmod(date.year*12+date.month-1+n,12)
        self.day.set(dt.date(year,month+1,1).isoformat()); self.refresh()
    def today(self):
        self.day.set(dt.date.today().isoformat()); self.refresh()
    def set_view(self,view):
        self.view.set(view); self.refresh()
    def refresh(self):
        self.month_button.config(bg=self.theme['accent'] if self.view.get()=='month' else 'white',fg='white' if self.view.get()=='month' else '#68605a')
        self.list_button.config(bg=self.theme['accent'] if self.view.get()=='list' else 'white',fg='white' if self.view.get()=='list' else '#68605a')
        if self.view.get()=='list':
            self.month_controls.pack_forget(); self.refresh_list(); return
        if not self.month_controls.winfo_ismapped(): self.month_controls.pack(side='left',after=self.month_label)
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
                day_label=tk.Label(column,text=str(day.day)+('  · 今天' if is_today else ''),bg=bg,fg=self.theme['accent'] if is_today else ('#66605a' if current else '#c1b8af'),font=('Microsoft YaHei',9,'bold'),anchor='w')
                day_label.pack(fill='x',padx=3,pady=(0,5))
                for e in items:
                    card=tk.Frame(column,bg='white',padx=6,pady=7,highlightbackground='#e8dfd9',highlightthickness=1); card.pack(fill='x',pady=(0,6))
                    def add_label(text,fg,font):
                        label=tk.Label(card,text=text,bg='white',fg=fg,font=font,anchor='w',justify='left',wraplength=width)
                        label.pack(fill='x',anchor='w',pady=(0,3)); self.wrapping_labels.append(label)
                    add_label(hm(e['start'])+' – '+hm(e['start']+e['duration']),self.theme['accent'],('Arial',9,'bold'))
                    add_label(('✓  ' if e['completed'] else '')+e['title'],'#aaa39c' if e['completed'] else '#302c29',('Microsoft YaHei',10,'overstrike') if e['completed'] else ('Microsoft YaHei',10,'bold'))
                    add_label('地点：'+e['location'],'#aaa39c' if e['completed'] else '#77716a',('Microsoft YaHei',9))
                    add_label('耗时：'+str(e['duration'])+' 分钟','#77716a',('Microsoft YaHei',9))
                    if e.get('notes'): add_label('备注：'+e['notes'],'#77716a',('Microsoft YaHei',9))
                    self.bind_card(card,e['id'])
        self.canvas.yview_moveto(0)
    def refresh_list(self):
        self.month_label.config(text='全部行程')
        for widget in self.gridframe.winfo_children(): widget.destroy()
        for widget in self.week_header.winfo_children(): widget.destroy()
        for row in range(6): self.gridframe.grid_rowconfigure(row,weight=0,minsize=0)
        self.wrapping_labels=[]; self.list_headings=[]; self.completion_widgets={}
        events=all_rows()
        wrapper=tk.Frame(self.gridframe,bg='#f5f3f0',padx=8,pady=8); wrapper.grid(row=0,column=0,columnspan=7,sticky='nsew')
        if not events:
            tk.Label(wrapper,text='还没有行程',bg='#f5f3f0',fg='#a39a90',font=('Microsoft YaHei',11)).pack(anchor='w',pady=30)
        current_day=None; previous=None
        for e in events:
            if e['day']!=current_day:
                current_day=e['day']; previous=None; d=dt.date.fromisoformat(current_day)
                day_events=sum(1 for item in events if item['day']==current_day)
                heading=tk.Frame(wrapper,bg='#f5f3f0'); heading.pack(fill='x',pady=(16,10)); self.list_headings.append((current_day,heading))
                tk.Label(heading,text=f"{d.day}  周{'一二三四五六日'[d.weekday()]}",bg='#f5f3f0',fg='#292725',font=('Microsoft YaHei',15,'bold')).pack(side='left')
                tk.Label(heading,text=f'{d.year} 年 {d.month} 月',bg='#f5f3f0',fg='#9b948e',font=('Microsoft YaHei',11)).pack(side='left',padx=12)
                tk.Label(heading,text=f'{day_events} 项',bg='#f5f3f0',fg='#a39a90').pack(side='right')
            if previous is not None:
                gap=short_gap(previous,e)
                if gap is not None:
                    gapline=tk.Frame(wrapper,bg='#f5f3f0'); gapline.pack(fill='x',pady=(0,8))
                    tk.Label(gapline,text=f'◷  间隔 {gap} 分钟',bg=self.theme['tint'],fg=self.theme['accent'],padx=10,pady=4,font=('Microsoft YaHei',9,'bold')).pack(side='left',padx=(105,0))
            row=tk.Frame(wrapper,bg='#f5f3f0'); row.pack(fill='x',pady=(0,8))
            timebox=tk.Frame(row,bg='#ebeef0',width=92,padx=8,pady=10); timebox.pack(side='left',fill='y',padx=(0,12)); timebox.pack_propagate(False)
            tk.Label(timebox,text=hm(e['start']),bg='#ebeef0',fg='#292725',font=('Arial',12,'bold')).pack()
            tk.Label(timebox,text=str(e['duration'])+' 分钟',bg='#ebeef0',fg='#8a8d90',font=('Microsoft YaHei',9)).pack(pady=3)
            tk.Label(timebox,text=hm(e['start']+e['duration']),bg='#ebeef0',fg='#292725',font=('Arial',12,'bold')).pack()
            card=tk.Frame(row,bg='white',padx=15,pady=10,highlightbackground='#e8e4df',highlightthickness=1); card.pack(side='left',fill='both',expand=True)
            mark=tk.Button(card,text='●' if e['completed'] else '○',command=lambda entry=e:self.set_completed(entry),bg='white',activebackground='white',fg=self.theme['accent'],activeforeground=self.theme['accent'],relief='flat',borderwidth=0,font=('Arial',19),cursor='hand2')
            mark.pack(side='left',anchor='n',padx=(0,8))
            actions=tk.Frame(card,bg='white'); actions.pack(side='right')
            tk.Button(actions,text='编辑',command=lambda event_id=e['id']:self.edit_event(event_id),bg='white',fg=self.theme['accent'],relief='flat',cursor='hand2').pack()
            tk.Button(actions,text='删除',command=lambda event_id=e['id']:self.delete(event_id),bg='white',fg='#a99a90',relief='flat',cursor='hand2').pack()
            title_label=None
            for text,fg,font in [(e['title'],'#a6a09b' if e['completed'] else '#302c29',('Microsoft YaHei',12,'overstrike') if e['completed'] else ('Microsoft YaHei',12,'bold')),('⌖  '+e['location'],'#77716a',('Microsoft YaHei',10)),('备注：'+e['notes'] if e.get('notes') else '', '#9a928b',('Microsoft YaHei',9))]:
                if text:
                    label=tk.Label(card,text=text,bg='white',fg=fg,font=font,anchor='w',justify='left',wraplength=max(100,self.canvas.winfo_width()-175))
                    label.pack(fill='x',anchor='w',pady=(0,4)); self.wrapping_labels.append(label)
                    if title_label is None: title_label=label
            self.completion_widgets[e['id']]=(mark,title_label)
            self.bind_card(card,e['id'])
            previous=e
        self.canvas.yview_moveto(0); self.after_idle(self.update_list_date)
    def set_completed(self,entry):
        event_id=entry['id']; completed=not bool(entry['completed'])
        try: update_completed(event_id,completed)
        except Exception as ex: messagebox.showerror('保存失败',str(ex)); return
        entry['completed']=int(completed)
        mark,title=self.completion_widgets[event_id]
        mark.config(text='●' if completed else '○')
        if title: title.config(fg='#a6a09b' if completed else '#302c29',font=('Microsoft YaHei',12,'overstrike') if completed else ('Microsoft YaHei',12,'bold'))
    def bind_card(self,widget,event_id):
        # Buttons retain their own actions; clicking the card or its text opens the editor.
        if isinstance(widget,(tk.Frame,tk.Label)):
            widget.configure(cursor='hand2')
            widget.bind('<Button-1>',lambda event:self.edit_event(event_id))
            for child in widget.winfo_children(): self.bind_card(child,event_id)
    def edit_event(self,event_id):
        if getattr(self,'editor_panel',None) and self.editor_panel.winfo_exists():
            self.editor_panel.lift(); return
        with db() as c:
            c.row_factory=sqlite3.Row
            row=c.execute('SELECT * FROM events WHERE id=?',(event_id,)).fetchone()
        if row is None: messagebox.showerror('无法编辑','这项行程已不存在。'); self.refresh(); return
        old=dict(row)
        # A standalone borderless editor with explicit move/resize controls.
        # No modal grab or forced focus: the window must never capture all input.
        win=tk.Toplevel(self); self.editor_panel=win
        win.withdraw(); win.overrideredirect(True); win.configure(bg='#f7f8fa',highlightbackground='#d5dbe5',highlightthickness=1)
        self.update_idletasks()
        width=min(900,max(700,self.winfo_screenwidth()-40)); height=min(690,max(540,self.winfo_screenheight()-80))
        x=max(0,(self.winfo_screenwidth()-width)//2); y=max(0,(self.winfo_screenheight()-height)//2)
        win.geometry(f'{width}x{height}+{x}+{y}')
        header=tk.Frame(win,bg='white',padx=28,pady=15,highlightbackground='#e4e7ec',highlightthickness=1); header.pack(fill='x')
        heading=tk.Frame(header,bg='white'); heading.pack(fill='x',pady=(7,0))
        def begin_move(event): win._move_offset=(event.x_root-win.winfo_x(),event.y_root-win.winfo_y())
        def move_window(event):
            if hasattr(win,'_move_offset'):
                dx,dy=win._move_offset; win.geometry(f'{win.winfo_width()}x{win.winfo_height()}{event.x_root-dx:+d}{event.y_root-dy:+d}')
        for handle in (header,heading):
            handle.bind('<ButtonPress-1>',begin_move); handle.bind('<B1-Motion>',move_window)
        def resize_handle(edge,x,y,w,h,cursor):
            try: grip=tk.Frame(win,bg='#dce1e9',cursor=cursor)
            except tk.TclError: grip=tk.Frame(win,bg='#dce1e9',cursor='arrow')
            grip.place(relx=x,rely=y,anchor='center',relwidth=w,relheight=h,width=10 if w==0 else 0,height=10 if h==0 else 0); grip.lift()
            def start_resize(event):
                win._resize=(event.x_root,event.y_root,win.winfo_x(),win.winfo_y(),win.winfo_width(),win.winfo_height())
            def resize(event):
                if not hasattr(win,'_resize'): return
                rx,ry,ox,oy,ow,oh=win._resize; dx=event.x_root-rx; dy=event.y_root-ry
                left=ox+dx if 'w' in edge else ox; top=oy+dy if 'n' in edge else oy
                right=ox+ow+dx if 'e' in edge else ox+ow; bottom=oy+oh+dy if 's' in edge else oy+oh
                if right-left<700: left=right-700 if 'w' in edge else left; right=left+700
                if bottom-top<540: top=bottom-540 if 'n' in edge else top; bottom=top+540
                win._pending_geometry=f'{right-left}x{bottom-top}{left:+d}{top:+d}'
                if not getattr(win,'_resize_job',None):
                    def apply_resize():
                        win._resize_job=None
                        if win.winfo_exists(): win.geometry(win._pending_geometry)
                    win._resize_job=win.after(16,apply_resize)
            def finish_resize(event):
                if getattr(win,'_resize_job',None):
                    win.after_cancel(win._resize_job); win._resize_job=None
                    win.geometry(win._pending_geometry)
            grip.bind('<ButtonPress-1>',start_resize); grip.bind('<B1-Motion>',resize); grip.bind('<ButtonRelease-1>',finish_resize)
            return grip
        def add_grips():
            for edge,x,y,w,h,cursor in [('n',.5,0,.98,0,'sb_v_double_arrow'),('s',.5,1,.98,0,'sb_v_double_arrow'),('w',0,.5,0,.97,'sb_h_double_arrow'),('e',1,.5,0,.97,'sb_h_double_arrow'),('nw',0,0,0,0,'sizing'),('ne',1,0,0,0,'sizing'),('sw',0,1,0,0,'sizing'),('se',1,1,0,0,'sizing')]:
                resize_handle(edge,x,y,w,h,cursor)
        win.bind('<Escape>',lambda event:win.destroy())
        title=tk.StringVar(value=old['title'])
        tk.Entry(heading,textvariable=title,bg='white',fg='#172b4d',font=('Microsoft YaHei',18,'bold'),relief='flat',highlightthickness=0).pack(side='left',fill='x',expand=True)
        tk.Button(heading,text='✕',command=win.destroy,bg='white',fg='#626f86',activebackground='#edf0f5',relief='flat',font=('Arial',13),cursor='hand2',padx=8).pack(side='right',padx=(12,0))
        tk.Label(heading,text='已完成' if old.get('completed') else '待进行',bg='#e9f2ff' if not old.get('completed') else '#e6f4ea',fg='#2458a6' if not old.get('completed') else '#28784a',font=('Microsoft YaHei',9,'bold'),padx=10,pady=5).pack(side='right')
        footer=tk.Frame(win,bg='white',padx=28,pady=13,highlightbackground='#e4e7ec',highlightthickness=1); footer.pack(side='bottom',fill='x')
        status=tk.Label(footer,text='',bg='white',fg='#6b778c',font=('Microsoft YaHei',9)); status.pack(side='left')
        save=tk.Button(footer,text='保存',bg=self.theme['accent'],fg='white',activebackground=self.theme['hover'],activeforeground='white',relief='flat',padx=18,pady=7,cursor='hand2'); save.pack(side='right')
        tk.Button(footer,text='取消',bg='white',fg='#44546f',relief='flat',padx=12,pady=7,cursor='hand2',command=win.destroy).pack(side='right',padx=6)
        content=tk.Frame(win,bg='#f7f8fa',padx=28,pady=20); content.pack(fill='both',expand=True)
        detail=tk.Frame(content,bg='white',padx=20,pady=17,highlightbackground='#e5e9f0',highlightthickness=1); detail.pack(fill='both',expand=True)
        fields={'title':title}
        def caption(parent,text): tk.Label(parent,text=text,bg='white',fg='#626f86',font=('Microsoft YaHei',9,'bold')).pack(anchor='w',pady=(0,5))
        def input_field(parent,key,value):
            var=tk.StringVar(value=value); fields[key]=var
            entry=tk.Entry(parent,textvariable=var,bg='#f7f8fa',fg='#172b4d',relief='flat',highlightbackground='#d5dbe5',highlightcolor=self.theme['accent'],highlightthickness=1,font=('Microsoft YaHei',10))
            entry.pack(fill='x',ipady=7,pady=(0,13)); return entry
        info_row=tk.Frame(detail,bg='white'); info_row.pack(fill='x')
        date_column=tk.Frame(info_row,bg='white'); date_column.pack(side='left',fill='x',expand=True,padx=(0,8))
        location_column=tk.Frame(info_row,bg='white'); location_column.pack(side='left',fill='x',expand=True,padx=(8,0))
        caption(date_column,'日期')
        date_row=tk.Frame(date_column,bg='white'); date_row.pack(fill='x',pady=(0,13))
        date_value=tk.StringVar(value=old['day']); fields['day']=date_value
        date_entry=tk.Entry(date_row,textvariable=date_value,bg='#f7f8fa',fg='#172b4d',relief='flat',highlightbackground='#d5dbe5',highlightcolor=self.theme['accent'],highlightthickness=1,font=('Microsoft YaHei',10))
        date_entry.pack(side='left',fill='x',expand=True,ipady=7)
        def dismiss_popup():
            pop=getattr(win,'active_popup',None)
            if pop is not None and pop.winfo_exists(): pop.destroy()
            win.active_popup=None; win.popup_trigger=None
        def outside_click(event):
            pop=getattr(win,'active_popup',None); trigger=getattr(win,'popup_trigger',None)
            if pop is None or not pop.winfo_exists(): return
            widget=event.widget
            if widget is trigger or str(widget)==str(pop) or str(widget).startswith(str(pop)+'.'): return
            dismiss_popup()
        win.bind('<Button-1>',outside_click,add='+')
        def place_popup(pop,trigger):
            pop.update_idletasks()
            x=max(0,min(trigger.winfo_rootx()-win.winfo_rootx(),win.winfo_width()-pop.winfo_reqwidth()))
            below=trigger.winfo_rooty()-win.winfo_rooty()+trigger.winfo_height()+4
            y=below if below+pop.winfo_reqheight()<=win.winfo_height() else max(0,trigger.winfo_rooty()-win.winfo_rooty()-pop.winfo_reqheight()-4)
            pop.place(x=x,y=y); pop.lift(); win.active_popup=pop; win.popup_trigger=trigger
        def open_calendar(event=None):
            if getattr(win,'date_popup',None) and win.date_popup.winfo_exists():
                dismiss_popup(); return
            dismiss_popup()
            try: selected=dt.date.fromisoformat(date_value.get().strip())
            except ValueError: selected=dt.date.today()
            month=[selected.replace(day=1)]
            pop=tk.Frame(win,bg='white',highlightbackground='#d5dbe5',highlightthickness=1); win.date_popup=pop
            panel=tk.Frame(pop,bg='white',padx=12,pady=10); panel.pack(fill='both',expand=True)
            nav=tk.Frame(panel,bg='white'); nav.pack(fill='x')
            label=tk.Label(nav,bg='white',fg='#172b4d',font=('Microsoft YaHei',10,'bold')); label.pack(side='left',expand=True)
            days=tk.Frame(panel,bg='white'); days.pack(fill='both',expand=True,pady=(8,0))
            def close():
                dismiss_popup()
            def select(day): date_value.set(day.isoformat()); close()
            for col,name in enumerate('一二三四五六日'):
                days.grid_columnconfigure(col,weight=1,uniform='day')
                tk.Label(days,text=name,bg='white',fg='#8993a4',font=('Microsoft YaHei',9)).grid(row=0,column=col,sticky='ew',pady=3)
            day_buttons=[]
            for r in range(6):
                for c in range(7):
                    button=tk.Button(days,bg='white',activebackground='#edf0f5',relief='flat',borderwidth=0,font=('Arial',10),cursor='hand2',width=3,pady=4)
                    button.grid(row=r+1,column=c,sticky='ew',padx=1,pady=1); day_buttons.append(button)
            def render():
                current=month[0]; label.config(text=f'{current.year} 年 {current.month} 月')
                first=current-dt.timedelta(days=current.weekday())
                for i,button in enumerate(day_buttons):
                    day=first+dt.timedelta(days=i); chosen=day.isoformat()==date_value.get().strip()
                    button.config(text=str(day.day),command=lambda d=day:select(d),bg=self.theme['accent'] if chosen else 'white',fg='white' if chosen else ('#172b4d' if day.month==current.month else '#aeb6c3'))
            def shift_month(step):
                y,m=divmod(month[0].year*12+month[0].month-1+step,12)
                if 1<=y<=9999: month[0]=dt.date(y,m+1,1); render()
            tk.Button(nav,text='‹',command=lambda:shift_month(-1),bg='white',fg='#44546f',relief='flat',font=('Arial',15),cursor='hand2').pack(side='left')
            tk.Button(nav,text='›',command=lambda:shift_month(1),bg='white',fg='#44546f',relief='flat',font=('Arial',15),cursor='hand2').pack(side='right')
            calendar_actions=tk.Frame(panel,bg='white'); calendar_actions.pack(fill='x',pady=(5,0))
            tk.Button(calendar_actions,text='关闭',command=close,bg='white',fg='#626f86',relief='flat',cursor='hand2').pack(side='left')
            tk.Button(calendar_actions,text='今天',command=lambda:select(dt.date.today()),bg='white',fg=self.theme['accent'],relief='flat',cursor='hand2').pack(side='right')
            render(); place_popup(pop,date_entry)
        date_entry.bind('<Button-1>',open_calendar)
        caption(location_column,'地点'); input_field(location_column,'location',old['location'])
        time_row=tk.Frame(detail,bg='white'); time_row.pack(fill='x')
        left=tk.Frame(time_row,bg='white'); left.pack(side='left',fill='x',expand=True,padx=(0,8))
        right=tk.Frame(time_row,bg='white'); right.pack(side='left',fill='x',expand=True,padx=(8,0))
        def time_picker(parent,value):
            var=tk.StringVar(value=value)
            selector=tk.Entry(parent,textvariable=var,bg='#f7f8fa',fg='#172b4d',relief='flat',highlightbackground='#d5dbe5',highlightcolor=self.theme['accent'],highlightthickness=1,font=('Microsoft YaHei',10))
            selector.pack(fill='x',ipady=7,pady=(0,13))
            def open_times(event=None):
                if getattr(win,'popup_trigger',None) is selector and getattr(win,'active_popup',None) and win.active_popup.winfo_exists(): return
                dismiss_popup()
                pop=tk.Frame(win,bg='white',padx=10,pady=8,highlightbackground='#d5dbe5',highlightthickness=1)
                tk.Label(pop,text='选择时间',bg='white',fg='#172b4d',font=('Microsoft YaHei',9,'bold')).pack(anchor='w',pady=(0,5))
                lists=tk.Frame(pop,bg='white'); lists.pack()
                hour_list=tk.Listbox(lists,height=7,width=5,exportselection=False,relief='flat',bg='#f7f8fa',activestyle='none')
                minute_list=tk.Listbox(lists,height=7,width=5,exportselection=False,relief='flat',bg='#f7f8fa',activestyle='none')
                hour_list.pack(side='left',padx=(0,8)); minute_list.pack(side='left')
                for n in range(24): hour_list.insert('end',f'{n:02d}')
                for n in range(60): minute_list.insert('end',f'{n:02d}')
                try: current=parse_clock(var.get()); hour,minute=divmod(current,60)
                except ValueError: hour,minute=0,0
                hour_list.selection_set(hour); minute_list.selection_set(minute); hour_list.see(hour); minute_list.see(minute)
                def changed(event):
                    h=hour_list.curselection(); m=minute_list.curselection()
                    if h and m: var.set(f'{h[0]:02d}:{m[0]:02d}')
                hour_list.bind('<<ListboxSelect>>',changed); minute_list.bind('<<ListboxSelect>>',changed)
                def wheel(event,box): box.yview_scroll(int(-event.delta/120),'units'); return 'break'
                for box in (hour_list,minute_list): box.bind('<MouseWheel>',lambda e,b=box:wheel(e,b))
                tk.Button(pop,text='完成',command=dismiss_popup,bg='white',fg=self.theme['accent'],relief='flat',cursor='hand2').pack(anchor='e',pady=(5,0))
                place_popup(pop,selector)
            selector.bind('<Button-1>',open_times)
            return var
        caption(left,'开始时间'); fields['start']=time_picker(left,hm(old['start']))
        caption(right,'结束时间'); end_var=time_picker(right,hm(old['start']+old['duration']))
        fields['duration']=tk.StringVar(value=str(old['duration']))
        duration_label=tk.Label(detail,text=f"预计耗时  ·  {old['duration']} 分钟",bg='white',fg='#5c6b82',font=('Microsoft YaHei',9),anchor='w'); duration_label.pack(fill='x',pady=(0,14))
        def sync_duration(*_):
            try:
                st=parse_clock(fields['start'].get()); en=parse_clock(end_var.get())
                if en>st:
                    fields['duration'].set(str(en-st)); duration_label.config(text=f'预计耗时  ·  {en-st} 分钟',fg='#5c6b82')
                elif en==st and old['duration']==0:
                    fields['duration'].set('0'); duration_label.config(text='到达时间点',fg='#5c6b82')
                else: duration_label.config(text='结束时间须晚于开始时间',fg='#ae5147')
            except ValueError: duration_label.config(text='请输入 24 小时时间，如 09:30',fg='#ae5147')
        end_var.trace_add('write',sync_duration); fields['start'].trace_add('write',sync_duration)
        traffic_row=tk.Frame(detail,bg='white'); traffic_row.pack(fill='x',pady=(0,12))
        caption(traffic_row,'交通校验方式')
        mode=tk.StringVar(value='公交'); ttk.Combobox(traffic_row,textvariable=mode,values=['公交','驾车','步行','骑行'],state='readonly',width=16).pack(side='left')
        tk.Label(traffic_row,text='保存时检查与相邻行程的通行时间',bg='white',fg='#8993a4',font=('Microsoft YaHei',9),anchor='w').pack(side='left',padx=(12,0))
        caption(detail,'备注')
        notes=tk.Text(detail,height=5,wrap='word',bg='#f7f8fa',fg='#172b4d',relief='flat',highlightbackground='#d5dbe5',highlightcolor=self.theme['accent'],highlightthickness=1,font=('Microsoft YaHei',10),padx=8,pady=7); notes.pack(fill='both',expand=True); notes.insert('1.0',old.get('notes',''))
        def submit_edit():
            try:
                new={'title':fields['title'].get().strip(),'day':fields['day'].get().strip(),'start':parse_clock(fields['start'].get()),'location':fields['location'].get().strip(),'duration':int(fields['duration'].get().strip()),'notes':notes.get('1.0','end').strip()}
                end_time=parse_clock(end_var.get())
                if old['duration']==0 and end_time==new['start']: new['duration']=0
                elif end_time<=new['start']: raise ValueError('结束时间必须晚于开始时间。')
                else: new['duration']=end_time-new['start']
                if old['duration']==0 and new['duration']==0: validate(dict(new,duration=1))
                else: validate(new)
            except (ValueError,TypeError) as ex: messagebox.showerror('请检查输入',str(ex),parent=win); return
            mode_value=mode.get(); cfg=settings(); save.config(state='disabled'); status.config(text='正在检查地点和行程冲突…')
            def worker():
                try:
                    if new['location']==old['location']: new['coord']=old['coord']
                    else: new['location'],new['coord'],_=self.choose_place(new['location'],cfg.get('amap_key'))
                    current=[item for item in rows(new['day']) if item['id']!=event_id]
                    result=plan(new,current,lambda a,b:travel(a,b,cfg.get('amap_key'),mode_value))
                    def finish():
                        if not win.winfo_exists(): return
                        start,before,after,bs,src,prev,nxt=result
                        msg=f"活动：{new['title']}\n日期：{new['day']}  {hm(start)} – {hm(start+new['duration'])}\n地点：{new['location']}"
                        if prev: msg+=f"\n上一项到此处：{before} 分钟（{bs}）"
                        if nxt: msg+=f"\n此处到下一项：{after} 分钟（{src}）"
                        if not messagebox.askyesno('确认修改',msg+'\n\n保存修改？',parent=win): save.config(state='normal'); status.config(text=''); return
                        try:
                            with db() as c:
                                c.row_factory=sqlite3.Row
                                latest=c.execute('SELECT * FROM events WHERE id=?',(event_id,)).fetchone()
                                current=[dict(x) for x in c.execute('SELECT * FROM events WHERE day=? AND id<>? ORDER BY start,id',(new['day'],event_id))]
                                if latest is None or dict(latest)!=old or current!=current_snapshot: raise ValueError('相关行程已发生变化，请重新打开编辑。')
                                c.execute('UPDATE events SET day=?,title=?,location=?,coord=?,start=?,duration=?,fixed=?,notes=? WHERE id=?',(new['day'],new['title'],new['location'],new['coord'],new['start'],new['duration'],1,new['notes'],event_id))
                            self.day.set(new['day']); win.destroy(); self.refresh()
                        except Exception as ex: messagebox.showerror('保存失败',str(ex),parent=win); save.config(state='normal'); status.config(text='')
                    current_snapshot=current
                    self.after(0,finish)
                except Exception as ex:
                    msg=str(ex)
                    def fail():
                        if win.winfo_exists(): messagebox.showerror('无法保存',msg,parent=win); save.config(state='normal'); status.config(text='')
                    self.after(0,fail)
            threading.Thread(target=worker,daemon=True).start()
        save.config(command=submit_edit)
        add_grips(); win.deiconify(); win.lift()
    def delete(self,event_id):
        if messagebox.askyesno('删除行程','确定删除这项行程吗？',parent=self):
            with db() as c: c.execute('DELETE FROM events WHERE id=?',(event_id,))
            self.refresh()
    def config_dialog(self):
        win=tk.Toplevel(self); win.title('设置'); win.transient(self); win.grab_set(); frame=ttk.Frame(win,padding=20); frame.pack(fill='both',expand=True); cfg=settings(); values={}
        ttk.Label(frame,text='自然语言解析方式').grid(row=0,column=0,sticky='w')
        provider=tk.StringVar(value=cfg.get('llm_provider') or ('DeepSeek' if cfg.get('deepseek_key') else '本地规则'))
        ttk.Combobox(frame,textvariable=provider,values=['本地规则','DeepSeek','阿里云百炼'],state='readonly',width=20).grid(row=1,column=0,sticky='w',pady=(3,8))
        for i,(key,label) in enumerate([('deepseek_key','DeepSeek 官方 API Key'),('bailian_key','阿里云百炼 API Key'),('amap_key','高德 Web 服务 API Key')]):
            ttk.Label(frame,text=label).grid(row=2+i*2,column=0,sticky='w'); var=tk.StringVar(value=cfg.get(key,'')); values[key]=var
            ttk.Entry(frame,textvariable=var,show='*',width=62).grid(row=3+i*2,column=0,sticky='ew',pady=(2,8))
        ttk.Label(frame,text='百炼地域（须与 Key 所属地域一致）').grid(row=8,column=0,sticky='w')
        region=tk.StringVar(value=cfg.get('bailian_region','北京')); ttk.Combobox(frame,textvariable=region,values=['北京','新加坡'],state='readonly',width=20).grid(row=9,column=0,sticky='w',pady=(2,8))
        ttk.Label(frame,text='百炼 API Host（可选：业务空间专属域名）').grid(row=10,column=0,sticky='w')
        host=tk.StringVar(value=cfg.get('bailian_host','')); ttk.Entry(frame,textvariable=host,width=62).grid(row=11,column=0,sticky='ew',pady=(2,8))
        ttk.Label(frame,text='密钥明文存储于本机用户目录，请勿分享配置文件。').grid(row=12,column=0,sticky='w')
        ttk.Label(frame,text='主题色').grid(row=13,column=0,sticky='w',pady=(14,4))
        selected=tk.StringVar(value=cfg.get('theme') if cfg.get('theme') in THEMES else '暖珊瑚')
        palette=tk.Frame(frame); palette.grid(row=14,column=0,sticky='w')
        for name,colors in THEMES.items():
            item=tk.Frame(palette); item.pack(side='left',padx=(0,8))
            tk.Label(item,text='●',fg=colors['accent'],font=('Arial',16)).pack(side='left')
            ttk.Radiobutton(item,text=name,variable=selected,value=name).pack(side='left')
        def save():
            cfg.update({k:v.get().strip() for k,v in values.items()}); cfg.update(theme=selected.get(),llm_provider=provider.get(),bailian_region=region.get(),bailian_host=host.get().strip())
            CONFIG.write_text(json.dumps(cfg,ensure_ascii=False),encoding='utf-8')
            try: CONFIG.chmod(0o600)
            except OSError: pass
            self.theme=theme_for(selected.get())
            self.composer_label.config(fg=self.theme['accent']); self.input.config(insertbackground=self.theme['accent'])
            self.add.config(bg=self.theme['accent'],activebackground=self.theme['hover']); self.refresh()
            win.destroy()
        ttk.Button(frame,text='保存',command=save).grid(row=15,column=0,sticky='e',pady=(12,0))
    def ask_choice(self,title,description,choices):
        # Called by a worker; Tk widgets are created on the UI thread.
        done=threading.Event(); answer={'value':None}
        def show():
            dialog=tk.Toplevel(self); dialog.title(title); dialog.transient(self); dialog.grab_set(); dialog.geometry('700x420')
            frame=ttk.Frame(dialog,padding=18); frame.pack(fill='both',expand=True)
            ttk.Label(frame,text=description,wraplength=650,justify='left').pack(anchor='w',pady=(0,10))
            box=tk.Listbox(frame,selectmode='browse',font=('Microsoft YaHei',10),height=12,exportselection=False)
            scrollbar=ttk.Scrollbar(frame,orient='vertical',command=box.yview); box.configure(yscrollcommand=scrollbar.set)
            scrollbar.pack(side='right',fill='y'); box.pack(fill='both',expand=True)
            for item in choices: box.insert('end',item)
            footer=ttk.Frame(frame); footer.pack(fill='x',pady=(10,0))
            def close(): dialog.grab_release(); dialog.destroy(); done.set()
            def select():
                selected=box.curselection()
                if not selected: messagebox.showinfo('请选择','请先选择一项。',parent=dialog); return
                answer['value']=selected[0]; close()
            ttk.Button(footer,text='取消',command=close).pack(side='right',padx=5); ttk.Button(footer,text='确认',command=select).pack(side='right')
            box.bind('<Double-Button-1>',lambda e:select()); dialog.protocol('WM_DELETE_WINDOW',close)
        self.after(0,show); done.wait()
        if answer['value'] is None: raise ValueError('已取消本次规划。')
        return answer['value']
    def choose_place(self,place,key):
        options=find_places(place,key)
        if len(options)==1 and key: p=options[0]
        else:
            i=self.ask_choice('选择地点',f'“{place}”可能对应多个地点，请选择确切位置；找不到时请取消并输入更具体名称。', [p['name']+'　'+p['note'] for p in options]); p=options[i]
        return p['name'],p['coord'],p['note']
    def ask_text(self,title,prompt):
        done=threading.Event(); result={'value':None}
        def show():
            result['value']=simpledialog.askstring(title,prompt,parent=self); done.set()
        self.after(0,show); done.wait()
        if result['value'] is None: raise ValueError('已取消本次规划。')
        return result['value'].strip()
    def submit(self):
        text=self.input.get('1.0','end').strip(); cfg=settings()
        if not text: return
        self.add.config(state='disabled'); self.status.config(text='正在识别活动…')
        def work():
            try:
                parsed=parse(text,cfg,multiple=True); events=parsed.get('events')
                if not isinstance(events,list) or not 1<=len(events)<=15 or any(not isinstance(e,dict) for e in events): raise ValueError('未能识别活动列表，或活动超过15项，请调整输入。')
                self.after(0,lambda:PlannerPanel(self,events,cfg,sys.modules[__name__]))
            except Exception as ex:
                msg=str(ex)
                def fail(): self.add.config(state='normal'); self.status.config(text=''); messagebox.showerror('无法识别',msg)
                self.after(0,fail)
        threading.Thread(target=work,daemon=True).start()
    def plan_prepared(self,events,cfg,panel):
        self.status.config(text='正在计算路线…')
        def work():
            try:
                snapshots={day:rows(day) for day in {e['day'] for e in events}}
                working={day:list(items) for day,items in snapshots.items()}; planned=[]; previous_by_day={}; cache={}
                for e in events:
                    previous=previous_by_day.get(e['day'])
                    if previous: e['earliest']=max(e.get('earliest') or 0,previous['start']+previous['duration'])
                    names={x['coord']:x['location'] for x in working[e['day']]}; names[e['coord']]=e['location']
                    def route(a,b):
                        if a==b: return (0,'同一地点')
                        explicit=e.get('mode_in') if b==e['coord'] else None
                        key=(a,b,explicit)
                        if key not in cache:
                            if explicit:
                                minutes,source=travel(a,b,cfg.get('amap_key'),explicit); mode=explicit
                            else:
                                opts=[]; choices=[]
                                for option in ('公交','步行','骑行','驾车'):
                                    try:
                                        mins,src=travel(a,b,cfg.get('amap_key'),option); opts.append((option,mins,src)); choices.append(f'{option} · 约 {mins} 分钟（{src}）')
                                    except Exception: pass
                                if not opts: raise ValueError('所有交通方式查询失败，请检查网络、密钥或地点。')
                                idx=self.ask_choice('选择交通方案',f"从 {names.get(a,a)} 到 {names.get(b,b)}，请选择方案：",choices)
                                mode,minutes,source=opts[idx]
                            cache[key]=(minutes,mode+' · '+source)
                        return cache[key]
                    result=plan(e,working[e['day']],route); start,before,after,bs,src,prev,nxt=result
                    e['start']=start; e['_route']=result
                    if prev: e['notes']+=('\n' if e['notes'] else '')+f'前往方式：{bs}，约 {before} 分钟'
                    if e.get('latest_end') is not None: e['notes']+=('\n' if e['notes'] else '')+f"截止时间：{hm(e['latest_end'])}"
                    planned.append(e); working[e['day']].append(e); working[e['day']].sort(key=lambda x:x['start']); previous_by_day[e['day']]=e
                self.after(0,lambda:self.confirm_batch(planned,snapshots,panel))
            except Exception as ex:
                msg=str(ex)
                def fail():
                    self.status.config(text=''); panel.restore()
                    messagebox.showerror('暂时无法安排',msg+'\n请检查截止时间、活动时长或交通方式后重试。',parent=panel)
                self.after(0,fail)
        threading.Thread(target=work,daemon=True).start()
    def confirm_batch(self,planned,snapshots,panel):
        dialog=tk.Toplevel(self); dialog.title('预览行程'); dialog.transient(self); dialog.grab_set(); dialog.geometry('760x650'); dialog.configure(bg='#f5f3f0')
        header=tk.Frame(dialog,bg='#f5f3f0',padx=24,pady=16); header.pack(fill='x')
        tk.Label(header,text='你的行程',bg='#f5f3f0',fg='#292725',font=('Microsoft YaHei',18,'bold')).pack(anchor='w')
        tk.Label(header,text='核对地点、时间和路程后，一次保存全部活动。',bg='#f5f3f0',fg='#8f877f').pack(anchor='w',pady=(4,0))
        buttons=tk.Frame(dialog,bg='#f5f3f0',padx=24,pady=12); buttons.pack(side='bottom',fill='x')
        approved={'yes':False}
        def accept(): approved['yes']=True; dialog.destroy()
        tk.Button(buttons,text='保存全部行程',command=accept,bg=self.theme['accent'],fg='white',activebackground=self.theme['hover'],activeforeground='white',relief='flat',padx=16,pady=8).pack(side='right')
        tk.Button(buttons,text='返回修改',command=dialog.destroy,bg='#f5f3f0',fg='#665f58',relief='flat',padx=12,pady=8).pack(side='right',padx=8)
        area=tk.Frame(dialog,bg='#f5f3f0',padx=24); area.pack(fill='both',expand=True)
        canvas=tk.Canvas(area,bg='#f5f3f0',highlightthickness=0); scroll=ttk.Scrollbar(area,orient='vertical',command=canvas.yview); canvas.configure(yscrollcommand=scroll.set); scroll.pack(side='right',fill='y'); canvas.pack(fill='both',expand=True)
        content=tk.Frame(canvas,bg='#f5f3f0'); window=canvas.create_window(0,0,anchor='nw',window=content)
        content.bind('<Configure>',lambda _:canvas.configure(scrollregion=canvas.bbox('all'))); canvas.bind('<Configure>',lambda e:canvas.itemconfigure(window,width=e.width))
        for e in planned:
            start,before,after,bs,src,prev,nxt=e['_route']
            if prev:
                tk.Label(content,text=f"↓  从 {prev['location']} 前往：{bs}，约 {before} 分钟",bg='#f5f3f0',fg=self.theme['accent'],anchor='w',wraplength=600,justify='left').pack(fill='x',pady=(2,6))
            card=tk.Frame(content,bg='white',padx=14,pady=12,highlightbackground='#e8e4df',highlightthickness=1); card.pack(fill='x',pady=(0,8))
            times=hm(start) if e['duration']==0 else hm(start)+' – '+hm(start+e['duration'])
            tk.Label(card,text=e['day']+'   '+times,bg='white',fg=self.theme['accent'],font=('Microsoft YaHei',10,'bold')).pack(anchor='w')
            tk.Label(card,text=e['title'],bg='white',fg='#292725',font=('Microsoft YaHei',12,'bold')).pack(anchor='w',pady=(4,0))
            tk.Label(card,text='地点：'+e['location'],bg='white',fg='#77716a',anchor='w',wraplength=620,justify='left').pack(fill='x',pady=(4,0))
            if e.get('notes'): tk.Label(card,text='备注：'+e['notes'],bg='white',fg='#928b84',anchor='w',wraplength=620,justify='left').pack(fill='x',pady=(3,0))
            if nxt: tk.Label(content,text=f"↓  前往下一项：{src}，约 {after} 分钟",bg='#f5f3f0',fg=self.theme['accent'],anchor='w',wraplength=600,justify='left').pack(fill='x',pady=(0,5))
        self.wait_window(dialog)
        self.status.config(text='')
        if not approved['yes']:
            panel.restore(); return
        try:
            with db() as c:
                c.row_factory=sqlite3.Row
                for day,snapshot in snapshots.items():
                    current=[dict(r) for r in c.execute('SELECT * FROM events WHERE day=? ORDER BY start,id',(day,))]
                    if current!=snapshot: raise ValueError('行程已有变化，请重新规划。')
                for e in planned:
                    c.execute('INSERT INTO events(day,title,location,coord,start,duration,fixed,notes) VALUES(?,?,?,?,?,?,?,?)',(e['day'],e['title'],e['location'],e['coord'],e['start'],e['duration'],1,e['notes']))
            panel.cancel(); self.day.set(planned[0]['day']); self.refresh()
        except Exception as ex:
            panel.restore(); messagebox.showerror('保存失败',str(ex),parent=panel)

if __name__=='__main__': App().mainloop()
