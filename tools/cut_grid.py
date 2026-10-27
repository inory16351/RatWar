# ChatGPT 격자 시트(마젠타 배경)를 칸마다 잘라 투명 PNG + 피벗(발 밑) 표로
# 사용: python -I tools/cut_grid.py <시트> <출력폴더> <열> <행> <이름1> <이름2> ...   (이름 '-' = 건너뜀)
# 출력: 각 PNG + 표준출력에 assets.json 형식 줄 ({"w","h","pivot","img"})
import sys, os, json
import numpy as np
from PIL import Image
from scipy import ndimage

src, out, cols, rows = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
names = sys.argv[5:]
im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
mag = np.clip(((r + b) / 2 - g) / 255.0, 0, 1)
soft = np.clip((0.62 - mag) / 0.22, 0, 1)
alpha = np.clip((0.42 - mag) / 0.16, 0, 1)
M = np.array([255.0, 0.0, 255.0])
a = np.maximum(soft, 0.3)[..., None]
un = (im - (1 - a) * M) / a
un[..., 1] = np.minimum(un[..., 1], np.maximum(un[..., 0], un[..., 2]))
im = np.where((soft < 1)[..., None], np.clip(un, 0, 255), im)
rgba = np.dstack([im, alpha * 255]).astype(np.uint8)

H, W = alpha.shape
cw, ch = W / cols, H / rows
os.makedirs(out, exist_ok=True)
table = {}
for i, n in enumerate(names):
    if n == '-': continue
    cx, cy = i % cols, i // cols
    x0, y0, x1, y1 = int(cx * cw), int(cy * ch), int((cx + 1) * cw), int((cy + 1) * ch)
    cell = rgba[y0:y1, x0:x1].copy()
    # 칸 안 가장 큰 덩어리 + 그 곁 조각만 (옆 칸 찌꺼기 · 점 버림)
    mask = cell[..., 3] > 100
    lab, k = ndimage.label(ndimage.binary_dilation(mask, iterations=6))
    if k == 0: print(n, '빈 칸'); continue
    big = np.argmax(ndimage.sum(mask, lab, range(1, k + 1))) + 1
    cell[..., 3] = np.where(lab == big, cell[..., 3], 0)
    ys, xs = np.where(cell[..., 3] > 20)
    cell = cell[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    img = Image.fromarray(cell)
    img.save(os.path.join(out, n + '.png'))
    # 피벗: 아래 12% 줄의 불투명 점들 가로 가운데, 세로 = 맨 아래
    h, w = cell.shape[:2]
    bottom = cell[int(h * 0.88):, :, 3] > 100
    bx = np.where(bottom.any(axis=0))[0]
    px = float((bx.min() + bx.max()) / 2) if len(bx) else w / 2
    table[n] = {"w": w, "h": h, "pivot": [round(px, 1), h], "img": "props/" + n + ".png"}
print(json.dumps(table, ensure_ascii=False))
