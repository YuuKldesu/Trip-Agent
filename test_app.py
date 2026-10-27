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
