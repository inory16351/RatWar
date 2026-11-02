# 컷씬 시트(2×2, 검은 홈) → 4장 JPG. 검은 홈은 밝기로 찾아서 칸 경계를 정함
# 사용: python -I tools/cut_slides.py <시트.png> <출력폴더> <이름>   → <이름>_1.jpg ~ _4.jpg (왼위 · 오위 · 왼아래 · 오아래)
import sys, os
import numpy as np
from PIL import Image

src, out, name = sys.argv[1], sys.argv[2], sys.argv[3]
im = Image.open(src).convert("RGB")
a = np.asarray(im).astype(np.float32).mean(axis=2)
H, W = a.shape

def spans(prof, n):
    # 밝은 구간(그림)만 골라 가장 긴 n 개
    on = prof > 28
    runs, s = [], None
    for i, v in enumerate(on):
        if v and s is None: s = i
        if not v and s is not None: runs.append((s, i)); s = None
    if s is not None: runs.append((s, len(on)))
    runs = sorted(sorted(runs, key=lambda r: r[1] - r[0])[-n:])
    return runs

cols = spans(a.mean(axis=0), 2)
rows = spans(a.mean(axis=1), 2)
os.makedirs(out, exist_ok=True)
k = 1
for (y0, y1) in rows:
    for (x0, x1) in cols:
        p = im.crop((x0 + 2, y0 + 2, x1 - 2, y1 - 2))
        p.save(os.path.join(out, f"{name}_{k}.jpg"), quality=90)
        print(f"{name}_{k}.jpg {p.size}")
        k += 1
