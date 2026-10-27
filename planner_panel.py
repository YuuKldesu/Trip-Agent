"""Interactive itinerary review, using the existing Tk desktop runtime."""
import tkinter as tk
from tkinter import ttk
import threading

MODES = ('公交', '步行', '骑行', '驾车')

class PlannerPanel(tk.Toplevel):
    def __init__(self, app, events, cfg, core):
        super().__init__(app)
        self.app=app; self.events=events; self.cfg=cfg; self.core=core; self.cards=[]
        self.title('完善行程'); self.geometry('980x750'); self.minsize(760,600); self.configure(bg='#f5f3f0')
        self.transient(app); self.grab_set(); self.protocol('WM_DELETE_WINDOW',self.cancel)
        head=tk.Frame(self,bg='#f5f3f0',padx=24,pady=14); head.pack(fill='x')
        tk.Label(head,text='完善行程',bg='#f5f3f0',fg='#272522',font=('Microsoft YaHei',19,'bold')).pack(anchor='w')
        tk.Label(head,text='核对每个活动；选择具体地点，补充时间，再生成路线。',bg='#f5f3f0',fg='#817a72').pack(anchor='w',pady=(3,0))
        footer=tk.Frame(self,bg='#f5f3f0',padx=24,pady=12); footer.pack(side='bottom',fill='x')
        self.status=tk.Label(footer,text='',bg='#f5f3f0',fg='#a34e3a',wraplength=630,justify='left'); self.status.pack(side='left')
        self.submit=tk.Button(footer,text='生成行程  →',bg=app.theme['accent'],fg='white',activebackground=app.theme['hover'],activeforeground='white',relief='flat',padx=20,pady=8,command=self.finish); self.submit.pack(side='right')
        tk.Button(footer,text='取消',bg='#f5f3f0',fg='#6f6860',relief='flat',padx=12,pady=8,command=self.cancel).pack(side='right',padx=8)
        container=tk.Frame(self,bg='#f5f3f0'); container.pack(fill='both',expand=True,padx=24)
        self.canvas=tk.Canvas(container,bg='#f5f3f0',highlightthickness=0); scroll=ttk.Scrollbar(container,orient='vertical',command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=scroll.set); scroll.pack(side='right',fill='y'); self.canvas.pack(side='left',fill='both',expand=True)
        self.list_frame=tk.Frame(self.canvas,bg='#f5f3f0'); self.window=self.canvas.create_window(0,0,anchor='nw',window=self.list_frame)
        self.list_frame.bind('<Configure>',lambda _: self.canvas.configure(scrollregion=self.canvas.bbox('all')))
        self.canvas.bind('<Configure>',lambda e:self.canvas.itemconfigure(self.window,width=e.width))
        for n,event in enumerate(events): self.build_card(n,event)
        self.bind_all('<MouseWheel>',self.scroll_wheel)
    def scroll_wheel(self,event):
        if self.winfo_exists() and self.state()=='normal': self.canvas.yview_scroll(int(-event.delta/120),'units')
    def restore(self):
        if self.winfo_exists(): self.deiconify(); self.grab_set(); self.lift(); self.status.config(text='请调整后重新生成行程。')
    def text(self,parent,value,size=10,color='#77716a',bold=False):
        widget=tk.Label(parent,text=value,bg='white',fg=color,font=('Microsoft YaHei',size,'bold' if bold else 'normal'),anchor='w'); widget.pack(anchor='w'); return widget
    def build_card(self,index,event):
        card=tk.Frame(self.list_frame,bg='white',padx=16,pady=14,highlightbackground='#e6e0da',highlightthickness=1); card.pack(fill='x',pady=(0,10))
        self.text(card,f'{index+1:02d}  {event.get("title") or "待补充活动"}',12,'#272522',True)
        grid=tk.Frame(card,bg='white'); grid.pack(fill='x',pady=(10,4))
        for c in (1,3,5): grid.columnconfigure(c,weight=1)
        def label(row,col,value): tk.Label(grid,text=value,bg='white',fg='#7c746b',anchor='w').grid(row=row,column=col,sticky='w',padx=(0,4),pady=3)
        def entry(row,col,value,width=14):
            var=tk.StringVar(value=value); ttk.Entry(grid,textvariable=var,width=width).grid(row=row,column=col,sticky='ew',padx=(0,12),pady=3); return var
        start=event.get('start'); duration=event.get('duration'); end=event.get('end')
        if end is None and isinstance(start,int) and isinstance(duration,int) and start+duration<1440: end=start+duration
        label(0,0,'活动'); title=entry(0,1,event.get('title') or '',18)
        label(0,2,'日期'); day=entry(0,3,event.get('day') or '',14)
        label(0,4,'截止'); deadline=entry(0,5,self.core.hm(event['latest_end']) if isinstance(event.get('latest_end'),int) else '',8)
        label(1,0,'开始'); begin=entry(1,1,self.core.hm(start) if isinstance(start,int) else '',10)
        label(1,2,'结束'); finish=entry(1,3,self.core.hm(end) if isinstance(end,int) else '',10)
        label(1,4,'耗时'); mins=entry(1,5,str(duration) if isinstance(duration,int) else '',8)
        notice=tk.Label(card,text='开始和结束时间均填写时，自动计算耗时；留空开始时间表示由系统安排。',bg='white',fg='#aaa299',anchor='w',font=('Microsoft YaHei',9)); notice.pack(fill='x')
        def calc(*_):
            try:
                a=self.core.parse_clock(begin.get()); b=self.core.parse_clock(finish.get())
                if b>a: mins.set(str(b-a)); notice.config(text=f'预计耗时 {b-a} 分钟',fg=self.app.theme['accent'])
                else: notice.config(text='结束时间必须晚于开始时间（暂不支持跨天）',fg='#bf5546')
            except ValueError: notice.config(text='开始和结束时间均填写时，自动计算耗时；留空开始时间表示由系统安排。',fg='#aaa299')
        begin.trace_add('write',calc); finish.trace_add('write',calc)
        quick=tk.Frame(card,bg='white'); quick.pack(anchor='w',pady=(4,8))
        for number,labeltext in [(30,'30 分钟'),(60,'1 小时'),(90,'1.5 小时'),(120,'2 小时')]:
            tk.Button(quick,text=labeltext,bg='#f4f2ef',fg='#655e58',relief='flat',padx=7,command=lambda n=number,m=mins,st=begin,en=finish:self.quick_duration(n,m,st,en)).pack(side='left',padx=(0,6))
        row=tk.Frame(card,bg='white'); row.pack(fill='x')
        tk.Label(row,text='地点',bg='white',fg='#7c746b').pack(side='left')
        query=tk.StringVar(value=event.get('location') or ''); ttk.Entry(row,textvariable=query).pack(side='left',fill='x',expand=True,padx=8)
        state={'candidates':[],'chosen':None,'busy':False}
        search=tk.Button(row,text='搜索地点',bg='#f2efec',relief='flat',padx=8,command=lambda:self.search_place(state,query,search,choice,status)); search.pack(side='left')
        choice=ttk.Combobox(card,state='readonly'); choice.pack(fill='x',pady=(6,0))
        def selected(_):
            i=choice.current(); state['chosen']=state['candidates'][i] if i>=0 else None
            self.reset_comparisons()
        choice.bind('<<ComboboxSelected>>',selected)
        def query_change(*_): state['chosen']=None; state['candidates']=[]; choice.set(''); choice['values']=(); self.reset_comparisons()
        query.trace_add('write',query_change)
        status=tk.Label(card,text='请搜索并选择地点',bg='white',fg='#ad664f',anchor='w'); status.pack(fill='x',pady=(3,4))
        travelrow=tk.Frame(card,bg='white'); travelrow.pack(fill='x',pady=(3,0))
        tk.Label(travelrow,text='前往此处',bg='white',fg='#7c746b').pack(side='left')
        selected_mode=event.get('mode_in') if event.get('mode_in') in MODES else ''
        mode=tk.StringVar(value=selected_mode)
        combo=ttk.Combobox(travelrow,textvariable=mode,state='readonly',width=12,values=MODES); combo.pack(side='left',padx=8)
        tk.Label(travelrow,text='首项仅在需要衔接已有行程时使用；公交包含地铁及步行。',bg='white',fg='#aaa299',font=('Microsoft YaHei',9)).pack(side='left')
        route_label=tk.Label(card,text='',bg='white',fg='#77716a',anchor='w',wraplength=800,justify='left'); route_label.pack(fill='x',pady=(5,0))
        compare=tk.Button(card,text='比较与上一项之间的交通',bg='#f2efec',fg='#655e58',relief='flat',command=lambda:self.compare(index));
        if index: compare.pack(anchor='w',pady=(4,0))
        note=tk.StringVar(value=event.get('notes') if isinstance(event.get('notes'),str) else '')
        noterow=tk.Frame(card,bg='white'); noterow.pack(fill='x',pady=(8,0)); tk.Label(noterow,text='备注',bg='white',fg='#7c746b').pack(side='left'); ttk.Entry(noterow,textvariable=note).pack(side='left',fill='x',expand=True,padx=8)
        self.cards.append(dict(event=event,title=title,day=day,begin=begin,end=finish,mins=mins,deadline=deadline,query=query,state=state,choice=choice,status=status,mode=mode,route_label=route_label,note=note))
    def quick_duration(self,number,mins,begin,end):
        mins.set(str(number))
        if begin.get().strip():
            try:
                endtime=self.core.parse_clock(begin.get())+number
                if endtime<1440: end.set(self.core.hm(endtime))
            except ValueError: pass
    def reset_comparisons(self):
        for item in self.cards: item['route_label'].config(text='')
    def search_place(self,state,query,button,choice,status):
        keyword=query.get().strip()
        if not keyword: status.config(text='请先填写地点名称'); return
        state['chosen']=None; button.config(state='disabled'); status.config(text='正在搜索…')
        def task():
            try:
                options=self.core.find_places(keyword,self.cfg.get('amap_key'))
                def done():
                    if not self.winfo_exists(): return
                    button.config(state='normal')
                    if query.get().strip()!=keyword: status.config(text='地点已更改，请重新搜索'); return
                    state['candidates']=options; choice['values']=[p['name']+' · '+p['note'] for p in options]; choice.set('')
                    status.config(text=f'找到 {len(options)} 个候选，请从下拉框选择具体地点。')
                self.after(0,done)
            except Exception as exc:
                msg=str(exc)
                def fail():
                    if self.winfo_exists(): button.config(state='normal'); status.config(text=msg)
                self.after(0,fail)
        threading.Thread(target=task,daemon=True).start()
    def compare(self,index):
        prev=self.cards[index-1]; curr=self.cards[index]
        if not prev['state']['chosen'] or not curr['state']['chosen']:
            curr['route_label'].config(text='请先选择前后两个具体地点。',fg='#b45743'); return
        a=prev['state']['chosen']['coord']; b=curr['state']['chosen']['coord']; curr['route_label'].config(text='正在查询交通方案…',fg='#77716a')
        def task():
            result=[]
            for mode in MODES:
                try:
                    duration,source=self.core.travel(a,b,self.cfg.get('amap_key'),mode); result.append(f'{mode} 约 {duration} 分钟（{source}）')
                except Exception: result.append(mode+' 暂不可用')
            def done():
                if self.winfo_exists() and prev['state']['chosen'] and curr['state']['chosen'] and prev['state']['chosen']['coord']==a and curr['state']['chosen']['coord']==b:
                    curr['route_label'].config(text='  ｜  '.join(result),fg='#77716a')
            self.after(0,done)
        threading.Thread(target=task,daemon=True).start()
    def finish(self):
        try:
            built=[]
            for i,c in enumerate(self.cards):
                e=dict(c['event']); e['title']=c['title'].get().strip(); e['day']=c['day'].get().strip()
                e['location']=c['query'].get().strip(); chosen=c['state']['chosen']
                if not chosen: raise ValueError(f'第 {i+1} 项：请搜索并选择具体地点。')
                e['location']=chosen['name']; e['coord']=chosen['coord']; e['place_source']=chosen['note']
                e['start']=self.core.parse_clock(c['begin'].get()) if c['begin'].get().strip() else None
                end=self.core.parse_clock(c['end'].get()) if c['end'].get().strip() else None
                if end is not None and e['start'] is None: raise ValueError(f'第 {i+1} 项：填写结束时间时，请同时填写开始时间。')
                if end is not None:
                    try: e['duration']=self.core.duration_between(c['begin'].get(),c['end'].get())
                    except ValueError as exc: raise ValueError(f'第 {i+1} 项：{exc}') from exc
                else:
                    try: e['duration']=int(c['mins'].get())
                    except ValueError: raise ValueError(f'第 {i+1} 项：请填写耗时，或填写开始和结束时间。')
                if e.get('is_return') and e['duration']==0: self.core.validate(dict(e,duration=1))
                else: self.core.validate(e)
                e['latest_end']=self.core.parse_clock(c['deadline'].get()) if c['deadline'].get().strip() else None
                if e.get('deadline_missing') and e['latest_end'] is None: raise ValueError(f'第 {i+1} 项：请补充截止时间。')
                e['mode_in']=c['mode'].get() or None; e['notes']=c['note'].get().strip()
                built.append(e)
            self.grab_release(); self.withdraw(); self.app.plan_prepared(built,self.cfg,self)
        except (ValueError,TypeError) as exc: self.status.config(text=str(exc))
    def cancel(self):
        self.unbind_all('<MouseWheel>'); self.app.bind_all('<MouseWheel>',self.app.on_wheel)
        self.destroy(); self.app.add.config(state='normal'); self.app.status.config(text='')
