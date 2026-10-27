# 쥐 옆모습 다리 합치기: 원본 시트에서 겹쳐 선 다리 한 짝을 세로로 갈라 thighF/thighN · shinF/shinN 반쪽으로 잘라 둠
# → 무릎을 따로 꺾으면(쓰러짐 · 걷기) 반쪽 군화 두 개로 벌어짐. 반쪽 두 장을 제자리에 겹쳐 온전한 다리 한 짝을 만들고
#    앞 · 뒤 다리 모두 그 그림을 씀. 관절 축은 두 반쪽 축의 가운데.
# 쓰기: python -I tools/merge_legs.py <rigs 폴더>   (고양이처럼 이미 온전한 다리는 건드리지 않음)
import json, sys, os
from PIL import Image

def merge(rig_dir):
    path = os.path.join(rig_dir, 'rig.json')
    with open(path, encoding='utf-8') as f: rig = json.load(f)
    side = rig.get('views', {}).get('side')
    if not side: return False
    P = side['parts']
    if not all(n in P for n in ('thighF', 'thighN', 'shinF', 'shinN')): return False
    # 반쪽인지: 두 그림 가로 범위가 거의 맞닿음(겹침 ≤ 8px)이고 합친 폭 안에 들어감 — 쥐 0..4px, 고양이는 축 간격이 넓은 온전한 두 다리
    if abs(P['thighF']['pivot'][0] - P['thighN']['pivot'][0]) > 30: return False
    for a, b in (('thighF', 'thighN'), ('shinF', 'shinN')):
        pa, pb = P[a], P[b]
        x0, y0 = min(pa['x'], pb['x']), min(pa['y'], pb['y'])
        x1, y1 = max(pa['x'] + pa['w'], pb['x'] + pb['w']), max(pa['y'] + pa['h'], pb['y'] + pb['h'])
        ims = [Image.open(os.path.join(rig_dir, P[n]['img'])).convert('RGBA') for n in (a, b)]
        k = ims[0].width / pa['w']  # 그림 px / 리그 px
        out = Image.new('RGBA', (round((x1 - x0) * k), round((y1 - y0) * k)), (0, 0, 0, 0))
        for n, im in zip((a, b), ims):  # 뒤(F) 먼저, 앞(N) 위에
            q = P[n]
            im = im.resize((round(q['w'] * k), round(q['h'] * k)), Image.LANCZOS) if im.width != round(q['w'] * k) else im
            out.alpha_composite(im, (round((q['x'] - x0) * k), round((q['y'] - y0) * k)))
        # 투명 픽셀 RGB 를 그림 평균색으로 (필터링 때 테두리 번짐 방지)
        px = out.load(); s = [0, 0, 0]; c = 0
        for yy in range(out.height):
            for xx in range(out.width):
                r, g, bb, al = px[xx, yy]
                if al > 200: s[0] += r; s[1] += g; s[2] += bb; c += 1
        avg = tuple(v // max(1, c) for v in s)
        for yy in range(out.height):
            for xx in range(out.width):
                if px[xx, yy][3] == 0: px[xx, yy] = avg + (0,)
        for n in (a, b):
            out.save(os.path.join(rig_dir, P[n]['img']))
            P[n].update(x=x0, y=y0, w=x1 - x0, h=y1 - y0)
    # 축: 엉덩이 · 무릎 · 발 = 두 반쪽 축의 가운데
    hip = [(P['thighF']['pivot'][i] + P['thighN']['pivot'][i]) / 2 for i in (0, 1)]
    knee = [(P['thighF']['end'][i] + P['thighN']['end'][i]) / 2 for i in (0, 1)]
    foot = [(P['shinF']['end'][i] + P['shinN']['end'][i]) / 2 for i in (0, 1)]
    for t, s in (('thighF', 'shinF'), ('thighN', 'shinN')):
        P[t]['pivot'], P[t]['end'] = list(hip), list(knee)
        P[s]['pivot'], P[s]['end'] = list(knee), list(foot)
    with open(path, 'w', encoding='utf-8', newline='\r\n') as f:
        json.dump(rig, f, ensure_ascii=False, indent=1)
    return True

if __name__ == '__main__':
    root = sys.argv[1]
    for d in sorted(os.listdir(root)):
        p = os.path.join(root, d)
        if os.path.isdir(p) and os.path.exists(os.path.join(p, 'rig.json')):
            print(d, 'merged' if merge(p) else 'skip')
