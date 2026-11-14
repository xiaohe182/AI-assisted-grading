import marshal
import re
import sys
import threading
import time
import types
import unittest
import zlib
from unittest.mock import patch
from pathlib import Path
from upgrade_model import read_archive

OCR_MODE = '--ocr' in sys.argv
if OCR_MODE:
    sys.argv.remove('--ocr')
ARTIFACT = sys.argv.pop(1)


def methods_from_exe():
    _, entries, _, _ = read_archive(Path(ARTIFACT).read_bytes())
    item = next(e for e in entries if e[0] == 'geography')
    module = marshal.loads(zlib.decompress(item[2]))
    cls = next(c for c in module.co_consts if isinstance(c, types.CodeType) and c.co_name == 'AIGradingApp')
    return {c.co_name: c for c in cls.co_consts if isinstance(c, types.CodeType)}


class Harness:
    def __init__(self, fail=False):
        self.main = threading.get_ident()
        self.release = threading.Event()
        self.entered = threading.Event()
        self.requests = []
        self.errors = []
        self.writes = []
        self.callbacks = {}
        self.next_id = 0
        self.fail = fail
        self.options = None

        def ui():
            assert threading.get_ident() == self.main, 'UI touched from worker thread'

        class Widget:
            def __init__(widget, value=''):
                widget.value = value
                widget.state = None
            def get(widget, *args):
                ui()
                return widget.value
            def config(widget, **kwargs):
                ui()
                widget.state = kwargs.get('state', widget.state)
            def delete(widget, *args):
                ui()
                widget.value = ''
            def insert(widget, *args):
                ui()
                widget.value += str(args[-1])

        def after(delay, fn):
            ui()
            self.next_id += 1
            self.callbacks[self.next_id] = (delay, fn)
            return self.next_id

        def cancel(identifier):
            ui()
            self.callbacks.pop(identifier, None)

        def error(*args):
            ui()
            self.errors.append(args)

        def create(**kwargs):
            self.requests.append((threading.get_ident(), kwargs))
            self.entered.set()
            if len(self.requests) == 1:
                self.release.wait(0.35)
            if self.fail:
                raise TimeoutError('simulated network timeout')
            content = '学生作答：地形影响气候和降水' if len(self.requests) == 1 else '5'
            return types.SimpleNamespace(choices=[types.SimpleNamespace(message=types.SimpleNamespace(content=content))])

        class Client:
            def __init__(client, **options):
                self.options = options
                client.chat = types.SimpleNamespace(completions=types.SimpleNamespace(create=create))
            def __enter__(client):
                return client
            def __exit__(client, *args):
                pass
            def close(client):
                pass

        def write(value):
            ui()
            self.writes.append(value)

        env = dict(tk=types.SimpleNamespace(NORMAL='normal', DISABLED='disabled', END='end'),
                   messagebox=types.SimpleNamespace(showwarning=error, showerror=error),
                   OpenAI=Client, re=re, time=types.SimpleNamespace(sleep=lambda _: None),
                   pyautogui=types.SimpleNamespace(write=write, press=lambda _: ui()))
        self.app = types.SimpleNamespace(
            is_grading=False, isfirst=False, after_id=None,
            path_entry=Widget('参考答案'), api_key_entry=Widget('offline-placeholder'),
            result_text=Widget(), grade_btn=Widget(), stop_btn=Widget(), select_area_btn=Widget(),
            root=types.SimpleNamespace(after=after, after_cancel=cancel),
            screenshot=lambda: 'OFFLINE_IMAGE',
        )
        for name in ('grade_paper', 'stop_grading', 'reset_grading_state'):
            setattr(self.app, name, types.MethodType(types.FunctionType(methods_from_exe()[name], env), self.app))

    def pump(self, until, duration=2):
        deadline = time.monotonic() + duration
        while not until() and time.monotonic() < deadline:
            due = [(i, fn) for i, (delay, fn) in list(self.callbacks.items()) if delay < 1000]
            for identifier, fn in due:
                if identifier in self.callbacks:
                    del self.callbacks[identifier]
                    fn()
            time.sleep(0.005)
        assert until(), 'Expected UI outcome was not observed'


class ResponsiveTests(unittest.TestCase):
    def test_network_does_not_block_ui(self):
        h = Harness()
        started = time.monotonic()
        h.app.grade_paper()
        try:
            self.assertLess(time.monotonic() - started, 0.1, 'grading blocks the event loop')
            self.assertTrue(h.entered.wait(1))
            self.assertNotEqual(h.requests[0][0], h.main)
        finally:
            h.release.set()

    def test_success_updates_ui_and_keeps_model_parameters(self):
        h = Harness()
        h.release.set()
        h.app.grade_paper()
        h.pump(lambda: bool(h.writes) or bool(h.errors))
        self.assertEqual(h.errors, [])
        self.assertEqual(h.writes, ['5'])
        self.assertIn('评分结果：5分', h.app.result_text.value)
        self.assertEqual(len(h.requests), 2)
        self.assertTrue(all(tid != h.main for tid, request in h.requests))
        self.assertEqual([request['model'] for _, request in h.requests],
                         ['qwen-vl-ocr' if OCR_MODE else 'qwen3.8-flash', 'qwen3.8-flash'])
        if OCR_MODE:
            self.assertNotIn('extra_body', h.requests[0][1], 'OCR must not receive a thinking-mode option')
            self.assertEqual(h.requests[1][1].get('extra_body'), {'enable_thinking': False})
        image = h.requests[0][1]['messages'][0]['content'][0]
        self.assertGreaterEqual(image['min_pixels'], 65536)
        self.assertEqual(h.options['max_retries'], 0)
        self.assertGreater(h.options['timeout'], 0)
        self.assertLessEqual(h.options['timeout'], 60)

    def test_stop_discards_late_results_and_prevents_auto_input(self):
        h = Harness()
        h.app.grade_paper()
        self.assertTrue(h.entered.wait(1))
        h.app.stop_grading()
        self.assertFalse(h.app.is_grading)
        h.release.set()
        h.pump(lambda: h.app.grade_btn.state == 'normal')
        self.assertEqual(h.writes, [])
        self.assertEqual(len(h.requests), 1, 'stopped job must not submit a scoring request')

    def test_network_failure_restores_controls_without_typing(self):
        h = Harness(fail=True)
        h.release.set()
        h.app.grade_paper()
        h.pump(lambda: bool(h.errors))
        self.assertFalse(h.app.is_grading)
        self.assertEqual(h.app.grade_btn.state, 'normal')
        self.assertEqual(h.writes, [])

    def test_double_click_does_not_start_duplicate_requests(self):
        h = Harness()
        h.app.grade_paper()
        self.assertTrue(h.entered.wait(1))
        h.app.grade_paper()
        self.assertEqual(len(h.requests), 1)
        h.app.stop_grading()
        h.release.set()
        h.pump(lambda: h.app.grade_btn.state == 'normal')

    def test_total_timeout_restores_ui_and_ignores_old_response(self):
        h = Harness()
        h.app.grade_paper()
        self.assertTrue(h.entered.wait(1))
        poll_id, (_, poll) = next(iter(h.callbacks.items()))
        del h.callbacks[poll_id]
        future = time.monotonic() + 131
        with patch('time.monotonic', return_value=future):
            poll()
        self.assertFalse(h.app.is_grading)
        self.assertEqual(h.app.grade_btn.state, 'normal')
        self.assertIn('超时', h.app.result_text.value)
        h.release.set()
        h.app._grading_worker.join(1)
        self.assertEqual(h.writes, [])
        self.assertEqual(len(h.requests), 1)

    def test_stop_during_first_score_delay_cancels_typing(self):
        h = Harness()
        h.app.isfirst = True
        h.release.set()
        h.app.grade_paper()
        h.pump(lambda: any(delay == 1000 for delay, _ in h.callbacks.values()))
        h.app.stop_grading()
        self.assertFalse(h.app.is_grading)
        self.assertEqual(h.writes, [])
        self.assertFalse(any(delay == 1000 for delay, _ in h.callbacks.values()))


if __name__ == '__main__':
    unittest.main()
