# 전략 지도 다시 만들기: layout.py + names.py → Resources/Data/campaign.json (hexes · 격자 · 시작 땅 순서) + 칸 이름 테이블
#   + art_requests/war_map2/ref_layout2.png (코덱스 지도 그림 배치 안내)
# 사용: python -I tools/campaign/build.py  (저장소 맨 위에서)
import json, collections, math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from layout import L, COLS, ROWS
from names import N
A = 'RatWarUnity/Assets/Resources/'
LANGS = ['ko', 'en', 'de', 'es', 'fr', 'ja', 'zh', 'ru']

# 격자: 지도 그림 16:9, 평평한 윗변 육각. dx = 1.5R/W, dy = √3R/H
dx = 0.070; R = dx / 1.5; dy = math.sqrt(3) * R / (9 / 16)
x0 = 0.08; y0 = 0.105

hexes = []; ni = 0; newkeys = []
for r in range(ROWS):
    for c in range(COLS):
        kind, old, fx, sp = L[r][c]
        if old is None:
            key = f'campaign.hex.{40 + ni}'; newkeys.append((key, N[ni])); ni += 1
        else:
            key = f'campaign.hex.{old}'
        h = {'name': key, 'kind': kind}
        if fx: h['fx'] = fx
        if sp: h['special'] = sp
        hexes.append(h)

# 시작 땅 순서: 사령부에서 가까운 칸부터 (같으면 남쪽 · 가운데 먼저). 난이도가 앞에서 몇 칸 가져감
def cube(i):
    c, r = i % COLS, i // COLS
    x = c; z = r - (c - (c & 1)) // 2
    return x, -x - z, z
def dist(a, b):
    A_, B_ = cube(a), cube(b)
    return max(abs(A_[k] - B_[k]) for k in range(3))
hq = next(i for i, h in enumerate(hexes) if h.get('fx') == 'hq')
order = sorted(range(len(hexes)), key=lambda i: (dist(i, hq), -(i // COLS), abs(i % COLS - hq % COLS)))
start = [i for i in order if i // COLS >= 4][:16]   # 강 남쪽 두 줄만 (강 · 다리는 공격해서 먹음)

p = A + 'Data/campaign.json'
d = json.load(open(p, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
d['cols'], d['rows'] = COLS, ROWS
d['x0'], d['dx'], d['y0'], d['dy'] = round(x0, 4), round(dx, 4), round(y0, 4), round(dy, 4)
d['start'] = start
d['hexes'] = hexes
json.dump(d, open(p, 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=1)
for li, l in enumerate(LANGS):
    tp = A + f'Tables/{l}/campaign.json'
    t = json.load(open(tp, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
    for k, v in newkeys: t[k] = v[li]
    json.dump(t, open(tp, 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=2); open(tp, 'a', encoding='utf-8').write('\n')
print('hexes', len(hexes), 'new names', len(newkeys), 'start', start, 'dy', round(dy, 4))
print('bottom y', y0 + (ROWS - 0.5) * dy + R / (9 / 16) * 0.87, 'right x', x0 + (COLS - 1) * dx + R)

# 배치 안내 그림 (지형별 색 육각, 글자 없음)
from PIL import Image, ImageDraw
W, H = 1536, 864
col = {'sewer': (90, 80, 70), 'harbor': (110, 130, 150), 'beach': (220, 200, 140), 'hills': (150, 150, 90), 'farm': (190, 180, 90),
       'village': (190, 110, 80), 'factory': (120, 110, 110), 'city': (150, 140, 140), 'capital': (110, 40, 40), 'river': (90, 130, 170),
       'bridge': (140, 140, 160), 'forest': (60, 100, 60), 'snow': (225, 230, 235), 'plains': (170, 175, 110), 'trench': (120, 95, 70), 'bunker': (130, 130, 130)}
im = Image.new('RGB', (W, H), (215, 200, 170)); dr = ImageDraw.Draw(im)
Rp = R * W
for i, h in enumerate(hexes):
    c, r = i % COLS, i // COLS
    cx = (x0 + c * dx) * W; cy = (y0 + (r + (0.5 if c % 2 else 0)) * dy) * H
    pts = [(cx + Rp * math.cos(math.radians(60 * k)), cy + Rp * math.sin(math.radians(60 * k))) for k in range(6)]
    dr.polygon(pts, fill=col[h['kind']], outline=(60, 50, 40))
im.save('art_requests/war_map2/ref_layout2.png')
