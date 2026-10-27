import unittest, datetime as dt, tempfile, sqlite3
from pathlib import Path
from unittest.mock import patch
import ssl, urllib.error, urllib.request, io
import app
from app import parse_local, validate, plan, travel

class Tests(unittest.TestCase):
    def test_parse(self):
        e=parse_local('明天在南京博物院参观，约2小时',dt.date(2026,10,27))
        self.assertEqual((e['day'],e['location'],e['duration']),('2026-10-28','南京博物院',120)); validate(e)
    def test_title_has_location(self):
        e=parse_local('明天在南京博物院参观，约2小时',dt.date(2026,10,27))
        self.assertIn('南京博物院',e['title'])
        self.assertIn('参观',e['title'])
    def test_old_database_migrates_without_data_loss(self):
        original=app.DB
        try:
            with tempfile.TemporaryDirectory() as folder:
                app.DB=Path(folder)/'test.sqlite3'
                with sqlite3.connect(app.DB) as c:
                    c.execute('CREATE TABLE events(id INTEGER PRIMARY KEY, day TEXT NOT NULL, title TEXT NOT NULL, location TEXT NOT NULL, coord TEXT NOT NULL, start INTEGER NOT NULL, duration INTEGER NOT NULL, fixed INTEGER NOT NULL DEFAULT 0)')
                    c.execute("INSERT INTO events(day,title,location,coord,start,duration) VALUES ('2026-10-27','参观','南京博物院','118,32',480,60)")
                events=app.rows('2026-10-27')
                self.assertEqual(events[0]['title'],'参观')
                self.assertEqual(events[0]['notes'],'')
                self.assertEqual(events[0]['completed'],0)
        finally: app.DB=original
    def test_short_gap_boundaries(self):
        first={'day':'2026-10-27','start':600,'duration':60}
        self.assertEqual(app.short_gap(first,{'day':'2026-10-27','start':692}),32)
        self.assertEqual(app.short_gap(first,{'day':'2026-10-27','start':660}),0)
        self.assertIsNone(app.short_gap(first,{'day':'2026-10-27','start':720}))
        self.assertIsNone(app.short_gap(first,{'day':'2026-10-28','start':692}))
        self.assertIsNone(app.short_gap(first,{'day':'2026-10-27','start':650}))
    def test_temporary_ssl_eof_retries(self):
        error=urllib.error.URLError(ssl.SSLEOFError('UNEXPECTED_EOF_WHILE_READING'))
        response=io.BytesIO(b'{"status":"1"}')
        with patch('app.urllib.request.urlopen',side_effect=[error,response]) as mock, patch('app.time.sleep'):
            result=app.fetch('https://restapi.amap.com/test',{'key':'secret'},'高德地点查询')
        self.assertEqual(result['status'],'1')
        self.assertEqual(mock.call_count,2)
    def test_ssl_error_does_not_expose_key(self):
        error=urllib.error.URLError(ssl.SSLEOFError('UNEXPECTED_EOF_WHILE_READING'))
        with patch('app.urllib.request.urlopen',side_effect=error), patch('app.time.sleep'):
            with self.assertRaises(ValueError) as caught: app.fetch('https://restapi.amap.com/test',{'key':'secret'},'高德路线查询')
        self.assertIn('高德路线查询',str(caught.exception))
        self.assertNotIn('secret',str(caught.exception))
    def test_edit_clock_validation(self):
        self.assertEqual(app.parse_clock('09:30'),570)
        for value in ['24:00','09:60','9点半','']:
            with self.subTest(value=value), self.assertRaises(ValueError): app.parse_clock(value)
    def test_editing_same_event_excludes_itself(self):
        current=[{'id':1,'start':540,'duration':60,'coord':'A'}, {'id':2,'start':720,'duration':60,'coord':'B'}]
        changed={'day':'2026-10-27','title':'参观','location':'A','start':540,'duration':60,'coord':'A'}
        app.validate(changed)
        start,*_=app.plan(changed,[e for e in current if e['id']!=1],lambda a,b:(30,'test'))
        self.assertEqual(start,540)
        changed['start']=680
        with self.assertRaises(ValueError): app.plan(changed,[e for e in current if e['id']!=1],lambda a,b:(30,'test'))
    def test_theme_defaults_and_palette(self):
        self.assertEqual(app.theme_for(None),app.THEMES['暖珊瑚'])
        self.assertEqual(app.theme_for('不存在'),app.THEMES['暖珊瑚'])
        for theme in app.THEMES.values():
            self.assertEqual(set(theme),{'accent','hover','tint'})
            for color in theme.values(): self.assertRegex(color,r'^#[0-9a-fA-F]{6}$')
    def test_bailian_request_and_region(self):
        response={'choices':[{'message':{'content':'{"day":"2026-10-28","location":"南京博物院","title":"参观博物院","duration":120,"start":null}'}}]}
        with patch('app.request_json',return_value=response) as mock:
            e=app.parse('明天去南京博物院参观2小时',{'llm_provider':'阿里云百炼','bailian_key':'test-key','bailian_region':'新加坡'})
        req=mock.call_args.args[0]
        self.assertTrue(req.full_url.startswith('https://dashscope-intl.aliyuncs.com/compatible-mode/v1/'))
        import json
        body=json.loads(req.data)
        self.assertEqual(body['model'],'qwen-plus'); self.assertFalse(body['enable_thinking']); self.assertEqual(e['duration'],120)
    def test_bailian_requires_own_key(self):
        with self.assertRaisesRegex(ValueError,'未填写 API Key'):
            app.parse('明天在南京博物院参观，约2小时',{'llm_provider':'阿里云百炼','deepseek_key':'some-other-key'})
    def test_workspace_endpoint_validation(self):
        endpoint=app.bailian_url('北京','https://llm-example.cn-beijing.maas.aliyuncs.com/compatible-mode/v1')
        self.assertEqual(endpoint,'https://llm-example.cn-beijing.maas.aliyuncs.com/compatible-mode/v1/chat/completions')
        for host in ['evil.example','evil.cn-beijing.maas.aliyuncs.com.evil.example','llm-demo.ap-southeast-1.maas.aliyuncs.com']:
            with self.subTest(host=host), self.assertRaises(ValueError): app.bailian_url('北京',host)
    def test_bailian_error_code_is_safe(self):
        payload=io.BytesIO(b'{"error":{"code":"invalid_api_key","message":"do not echo"}}')
        error=urllib.error.HTTPError('https://example.com',401,'Unauthorized',{},payload)
        with patch('app.urllib.request.urlopen',side_effect=error):
            with self.assertRaises(ValueError) as caught: app.request_json(urllib.request.Request('https://example.com'),'百炼')
        self.assertIn('invalid_api_key',str(caught.exception)); self.assertNotIn('do not echo',str(caught.exception))
    def test_place_candidates_keep_campuses(self):
        response={'status':'1','pois':[{'name':'南京大学(仙林校区)','address':'仙林大道','cityname':'南京市','location':'118.96,32.12'},{'name':'南京大学(鼓楼校区)','address':'汉口路','cityname':'南京市','location':'118.77,32.05'},{'name':'外地大学','cityname':'苏州市','location':'120,31'}]}
        with patch('app.fetch',return_value=response): result=app.find_places('南京大学','test-key')
        self.assertEqual(len(result),2); self.assertIn('仙林校区',result[0]['name']); self.assertIn('鼓楼校区',result[1]['name'])
    def test_bicycle_route_uses_high_de_api(self):
        with patch('app.fetch',return_value={'errcode':0,'data':{'paths':[{'duration':660}]}}) as mock:
            duration,source=app.travel('118,32','119,32','test-key','骑行')
        self.assertEqual(duration,11); self.assertIn('/v4/direction/bicycling',mock.call_args.args[0])
    def test_distinct_transport_on_both_legs(self):
        existing=[dict(start=540,duration=60,coord='A'),dict(start=840,duration=60,coord='B')]
        travel_times={('A','C'):(25,'骑行'),('C','B'):(40,'公交')}
        result=app.plan(dict(start=630,duration=120,coord='C'),existing,lambda a,b:travel_times[(a,b)])
        self.assertEqual(result[1:5],(25,40,'骑行','公交'))
    def test_multiple_event_extraction(self):
        import json
        output={'events':[{'day':'2026-10-29','title':'游览夫子庙','location':'夫子庙','duration':120,'start':None},{'day':'2026-10-29','title':'逛老门东','location':'老门东','duration':30,'start':None,'mode_in':'步行'},{'day':'2026-10-29','title':'返回南京大学','location':'南京大学','duration':0,'start':None,'mode_in':'公交','is_return':True,'latest_end':1080}]}
        response={'choices':[{'message':{'content':json.dumps(output,ensure_ascii=False)}}]}
        with patch('app.request_json',return_value=response): result=app.parse('后天早上去夫子庙玩2小时，然后步行去老门东30分钟，最后坐地铁回南京大学，下午六点前回来',{'llm_provider':'阿里云百炼','bailian_key':'test'},multiple=True)
        self.assertEqual(len(result['events']),3); self.assertEqual(result['events'][-1]['latest_end'],1080); self.assertEqual(result['events'][1]['mode_in'],'步行')
    def test_return_deadline_and_sequence(self):
        prior=[{'start':900,'duration':30,'coord':'A'}]
        result=app.plan({'duration':0,'start':None,'coord':'B','earliest':930,'latest_end':960},prior,lambda a,b:(25,'公交'))
        self.assertEqual(result[0],955)
        with self.assertRaises(ValueError): app.plan({'duration':0,'start':None,'coord':'B','earliest':930,'latest_end':950},prior,lambda a,b:(25,'公交'))
    def test_local_multiple_is_not_silently_truncated(self):
        with self.assertRaisesRegex(ValueError,'多项活动'): app.parse('明天去夫子庙玩2小时然后去老门东玩30分钟',{'llm_provider':'本地规则'},multiple=True)
    def test_multiple_events_extract_distinct_notes(self):
        import json
        output={'events':[{'day':'2026-10-29','title':'游览夫子庙','location':'夫子庙','duration':120,'start':None,'notes':'与朋友一起游览'}, {'day':'2026-10-29','title':'逛老门东','location':'老门东','duration':30,'start':None,'notes':'拍照'}]}
        response={'choices':[{'message':{'content':json.dumps(output,ensure_ascii=False)}}]}
        with patch('app.request_json',return_value=response) as mock:
            result=app.parse('明天和朋友去夫子庙，再去老门东拍照',{'llm_provider':'阿里云百炼','bailian_key':'test'},multiple=True)
        self.assertEqual([e['notes'] for e in result['events']],['与朋友一起游览','拍照'])
        body=json.loads(mock.call_args.args[0].data)
        self.assertIn('不虚构',body['messages'][0]['content'])
    def test_all_dates_and_completion_persist(self):
        original=app.DB
        try:
            with tempfile.TemporaryDirectory() as folder:
                app.DB=Path(folder)/'test.sqlite3'
                with app.db() as c:
                    c.executemany("INSERT INTO events(day,title,location,coord,start,duration) VALUES(?,?,?,?,?,?)",[('2026-11-01','第二项','夫子庙','118,32',600,30),('2026-10-31','第一项','夫子庙','118,32',540,30)])
                entries=app.all_rows(); self.assertEqual([e['title'] for e in entries],['第一项','第二项'])
                app.update_completed(entries[0]['id'],True); self.assertEqual(app.all_rows()[0]['completed'],1)
                app.update_completed(entries[0]['id'],False); self.assertEqual(app.all_rows()[0]['completed'],0)
        finally: app.DB=original
    def test_visible_list_date_tracks_scroll(self):
        headings=[('2026-10-28',20),('2026-10-31',280),('2026-11-01',560)]
        self.assertEqual(app.visible_list_date(headings,0),'2026-10-28')
        self.assertEqual(app.visible_list_date(headings,275),'2026-10-31')
        self.assertEqual(app.visible_list_date(headings,600),'2026-11-01')
        self.assertIsNone(app.visible_list_date([],0))
    def test_duration_from_start_end(self):
        self.assertEqual(app.duration_between('09:15','11:45'),150)
        for start,end in [('10:00','10:00'),('23:00','01:00'),('08:00','21:00')]:
            with self.subTest(start=start,end=end), self.assertRaises(ValueError): app.duration_between(start,end)
    def test_fixed_time(self):
        e=parse_local('2026-11-02下午2点在夫子庙吃饭，90分钟'); self.assertEqual(e['start'],840)
    def test_gap_accounts_for_both_trips(self):
        events=[dict(start=540,duration=60,coord='A'),dict(start=780,duration=60,coord='B')]
        e=dict(duration=90,start=None,coord='C'); self.assertEqual(plan(e,events,lambda a,b:(30,'test'))[0],630)
    def test_reject_conflict(self):
        events=[dict(start=540,duration=60,coord='A')]
        with self.assertRaises(ValueError): plan(dict(duration=60,start=610,coord='C'),events,lambda a,b:(30,'test'))
    def test_late_activity_rejected(self):
        with self.assertRaises(ValueError): plan(dict(duration=120,start=1260,coord='C'),[],lambda a,b:(0,''))
    def test_estimate(self): self.assertGreater(travel('118.8022,31.9745','118.7857,32.0417','','公交')[0],0)

if __name__=='__main__': unittest.main()
