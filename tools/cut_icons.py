# 아이콘 격자 시트(마젠타 배경)를 칸마다 잘라 128px 정사각 투명 PNG 로
# 사용: python -I tools/cut_icons.py <시트> <출력폴더> <열> <행> <이름1> <이름2> ...   (이름 '-' = 건너뜀)
# cut_grid.py 와 다른 점: 보라 · 분홍 그림이 지워지지 않게 '순수 마젠타'만 빼고, 떨어진 조각(속도선 · 음파)도 남김
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage

src, out, cols, rows = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
names = sys.argv[5:]
im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
H, W, _ = im.shape
cw, ch = W / cols, H / rows
os.makedirs(out, exist_ok=True)
for i, n in enumerate(names):
    if n == '-': continue
    cx, cy = i % cols, i // cols
    cell = im[int(cy * ch):int((cy + 1) * ch), int(cx * cw):int((cx + 1) * cw)]
    r, g, b = cell[..., 0], cell[..., 1], cell[..., 2]
    # 마젠타 정도: r · b 높고 g 낮고 r ≈ b
    mag = (np.minimum(r, b) - g) / 255.0 - np.abs(r - b) / 255.0 * 1.5
    a = np.clip((0.55 - mag) / 0.2, 0, 1)
    soft = (a < 1)[..., None]
    cell = np.where(soft, np.dstack([r, np.minimum(g, np.maximum(r, b)), np.minimum(b, np.maximum(r, g))]), cell)
    mask = a > 0.4
    lab, k = ndimage.label(mask)
    sizes = ndimage.sum(mask, lab, range(1, k + 1)); big = sizes.max()
    keep = np.zeros_like(mask)
    for j, sz in enumerate(sizes):
        if sz < max(60, big * 0.004): continue
        ys, xs = np.where(lab == j + 1)
        edge = ys.min() == 0 or xs.min() == 0 or ys.max() == lab.shape[0] - 1 or xs.max() == lab.shape[1] - 1
        if edge and sz < big * 0.05: continue  # 칸 가장자리 찌꺼기
        keep |= lab == j + 1
    a = np.where(ndimage.binary_dilation(keep, iterations=2), a, 0)
    rgba = np.dstack([cell, a * 255]).astype(np.uint8)
    ys, xs = np.where(rgba[..., 3] > 20)
    rgba = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    img = Image.fromarray(rgba)
    s = max(img.size)
    c = Image.new('RGBA', (s, s), (0, 0, 0, 0)); c.paste(img, ((s - img.width) // 2, (s - img.height) // 2))
    c.resize((128, 128), Image.LANCZOS).save(os.path.join(out, n + '.png'))
    print(n, img.size)
