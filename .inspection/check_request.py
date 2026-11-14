"""Exercise only the grading function with fake UI, image, and API dependencies."""
import marshal
import re
import sys
import types
import zlib
from pathlib import Path
from upgrade_model import read_archive


class RequestsCaptured(BaseException):
    pass


def find_grade(code):
    if code.co_name == 'grade_paper':
        return code
    for item in code.co_consts:
        if isinstance(item, types.CodeType):
            found = find_grade(item)
            if found:
                return found


def check(path):
    _, entries, _, _ = read_archive(Path(path).read_bytes())
    entry = next(item for item in entries if item[0] == 'geography')
    code = find_grade(marshal.loads(zlib.decompress(entry[2])))
    requests = []
    clients = []

    def create(**request):
        requests.append(request)
        if len(requests) == 2:
            raise RequestsCaptured()
        return types.SimpleNamespace(choices=[types.SimpleNamespace(
            message=types.SimpleNamespace(content='Student answer with sufficient text'))])

    def client(**options):
        clients.append(options)
        return types.SimpleNamespace(chat=types.SimpleNamespace(
            completions=types.SimpleNamespace(create=create)))

    app = types.SimpleNamespace(
        is_grading=True,
        path_entry=types.SimpleNamespace(get=lambda *args: 'Reference answer'),
        api_key_entry=types.SimpleNamespace(get=lambda: 'offline-test-placeholder'),
        screenshot=lambda: 'OFFLINE_IMAGE_PLACEHOLDER',
    )
    environment = {
        '__builtins__': {'len': len, 'Exception': Exception, 'str': str, 'print': print},
        'tk': types.SimpleNamespace(END='end'), 're': re, 'OpenAI': client,
    }
    function = types.FunctionType(code, environment)
    try:
        function(app)
    except RequestsCaptured:
        pass
    assert len(requests) == 2, 'Both OCR and grading requests must be constructed'
    assert [r['model'] for r in requests] == ['qwen3.8-flash'] * 2
    image = requests[0]['messages'][0]['content'][0]
    assert image['type'] == 'image_url'
    assert image['image_url']['url'] == 'data:image/jpeg;base64,OFFLINE_IMAGE_PLACEHOLDER'
    assert 65536 <= image['min_pixels'], f"min_pixels {image['min_pixels']} is below 65536"
    assert image['min_pixels'] <= image['max_pixels'] <= 16777216
    assert clients[0]['base_url'] == 'https://dashscope.aliyuncs.com/compatible-mode/v1'
    assert 'Student answer with sufficient text' in requests[1]['messages'][1]['content']
    print(f"PASS: both model requests captured; min_pixels={image['min_pixels']}; max_pixels={image['max_pixels']}")
    print('No real screenshots, network requests, keyboard input, or GUI execution performed.')


if __name__ == '__main__':
    check(sys.argv[1])
