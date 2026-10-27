import unittest, datetime as dt, tempfile, sqlite3
from pathlib import Path
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
