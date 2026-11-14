"""Replacement grading methods; UI work remains on the Tk event thread."""


class ResponsiveMethods:
    def grade_paper(self):
        import queue
        import threading
        import time
        import re

        if getattr(self, '_grading_busy', False):
            return
        self.after_id = None
        reference = self.path_entry.get('1.0', tk.END).strip()
        key = self.api_key_entry.get().strip()
        if not reference:
            messagebox.showwarning('警告', '请先输入参考答案')
            return
        if not key:
            messagebox.showwarning('警告', '请先输入 API Key')
            return
        self.is_grading = True
        self._grading_busy = True
        self._grading_cancel = threading.Event()
        cancel = self._grading_cancel
        results = queue.Queue()
        started = time.monotonic()
        self.grade_btn.config(state=tk.DISABLED)
        self.select_area_btn.config(state=tk.DISABLED)
        self.stop_btn.config(state=tk.NORMAL)

        def status(text):
            self.result_text.config(state=tk.NORMAL)
            self.result_text.delete('1.0', tk.END)
            self.result_text.insert(tk.END, text)
            self.result_text.config(state=tk.DISABLED)

        status('正在识别答案，请稍候…（可点击停止评分）')

        def worker():
            try:
                image = self.screenshot()
                if cancel.is_set():
                    return
                if not image:
                    raise ValueError('请先设定有效的识别区域')
                with OpenAI(api_key=key,
                            base_url='https://dashscope.aliyuncs.com/compatible-mode/v1',
                            timeout=60.0, max_retries=0) as client:
                    completion = client.chat.completions.create(
                        model='qwen3.8-flash',
                        messages=[{'role': 'user', 'content': [
                            {'type': 'image_url', 'image_url': {'url': 'data:image/jpeg;base64,' + image},
                             'min_pixels': 65536, 'max_pixels': 1003520},
                            {'type': 'text', 'text': 'Read all the text in the image.'}]}],
                        extra_body={'enable_thinking': False})
                    if cancel.is_set():
                        return
                    text = completion.choices[0].message.content
                    if not text:
                        raise ValueError('模型未返回识别文字，请缩小识别区域后重试')
                    text = re.sub(r'[（(]\d+分[）)]', '', text)
                    text = re.sub(r'^\d{1,2}[.、]\s*', '', text)
                    text = re.sub(r'[（(]\d+[）)]', '', text)
                    if len(text.strip()) < 4:
                        score = 0
                    else:
                        results.put(('phase', '正在根据参考答案评分，请稍候…（可点击停止评分）'))
                        completion = client.chat.completions.create(
                            model='qwen3.8-flash',
                            messages=[
                                {'role': 'system', 'content': '你是一个专业的地理教师，负责根据标准答案和评分标准评判学生答案并打分'},
                                {'role': 'user', 'content': '帮我改个试卷吧。标准答案是：' + reference +
                                 '以下是学生作答答案：' + text + '。你只要返回分数数值就可以，不用分析过程。'}])
                        if cancel.is_set():
                            return
                        content = completion.choices[0].message.content or ''
                        numbers = re.findall(r'\d+', content)
                        if not numbers:
                            raise ValueError('无法从 AI 返回结果中提取分数，请重试')
                        score = int(numbers[0])
                if not cancel.is_set():
                    results.put(('done', (text, score)))
            except Exception as error:
                if not cancel.is_set():
                    results.put(('error', str(error)))
            finally:
                results.put(('finished', None))

        def poll():
            # This callback is scheduled only from the UI thread.
            if getattr(self, '_grading_cancel', None) is not cancel:
                return
            if time.monotonic() - started >= 130:
                cancel.set()
                self.reset_grading_state()
                status('本次评分等待超时，已停止。请检查网络后重试。')
                return
            while True:
                try:
                    kind, value = results.get_nowait()
                except queue.Empty:
                    break
                if kind == 'finished':
                    if cancel.is_set():
                        self.reset_grading_state()
                        status('已停止评分')
                    return
                if cancel.is_set():
                    continue
                if kind == 'phase':
                    status(value)
                elif kind == 'error':
                    self.reset_grading_state()
                    status('评分失败，已停止。可以重新开始评分。')
                    messagebox.showerror('评分失败', value)
                    return
                elif kind == 'done':
                    text, score = value
                    status('识别文本：' + text + '\n评分结果：' + str(score) + '分')
                    # Use a timer instead of sleeping before automatic input.
                    delay = 1000 if self.isfirst else 0
                    self.isfirst = False

                    def apply_score():
                        self.after_id = None
                        if cancel.is_set() or not self.is_grading:
                            self.reset_grading_state()
                            return
                        try:
                            pyautogui.write(str(score))
                            pyautogui.press('enter')
                        except Exception as error:
                            self.reset_grading_state()
                            messagebox.showerror('自动填分失败', str(error))
                            return
                        self._grading_busy = False
                        self.after_id = self.root.after(2000, self.grade_paper)

                    self.after_id = self.root.after(delay, apply_score)
                    return
            self.root.after(100, poll)

        thread = threading.Thread(target=worker, daemon=True)
        self._grading_worker = thread
        try:
            thread.start()
        except Exception as error:
            self.reset_grading_state()
            messagebox.showerror('无法开始评分', str(error))
            return
        self.root.after(100, poll)

    def stop_grading(self):
        self.is_grading = False
        cancel = getattr(self, '_grading_cancel', None)
        if cancel is not None:
            cancel.set()
        if self.after_id is not None:
            self.root.after_cancel(self.after_id)
            self.after_id = None
        self.stop_btn.config(state=tk.DISABLED)
        worker = getattr(self, '_grading_worker', None)
        if worker is None or not worker.is_alive():
            self.reset_grading_state()
            text = '已停止评分'
        else:
            text = '已停止自动填分，正在结束当前请求…'
        self.result_text.config(state=tk.NORMAL)
        self.result_text.delete('1.0', tk.END)
        self.result_text.insert(tk.END, text)
        self.result_text.config(state=tk.DISABLED)

    def reset_grading_state(self):
        self.is_grading = False
        self._grading_busy = False
        cancel = getattr(self, '_grading_cancel', None)
        if cancel is not None:
            cancel.set()
        self.stop_btn.config(state=tk.DISABLED)
        self.grade_btn.config(state=tk.NORMAL)
        self.select_area_btn.config(state=tk.NORMAL)
