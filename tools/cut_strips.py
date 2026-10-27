# ChatGPT 시트(마젠타 배경)에서 가로 띠들을 잘라 투명 PNG 로 저장
# 사용: python -I tools/cut_strips.py <시트.png> <출력폴더> <이름1> <이름2> ...
#   위에서부터 큰 가로 띠 순서대로 이름을 붙임. 마젠타는 거리 기반으로 부드럽게 투명 + 가장자리 마젠타 번짐 제거
import sys, os
import numpy as np
from PIL import Image

src, out, names = sys.argv[1], sys.argv[2], sys.argv[3:]
im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
# 마젠타 정도: R · B 높고 G 낮음
mag = np.clip(((r + b) / 2 - g) / 255.0, 0, 1)
soft = np.clip((0.62 - mag) / 0.22, 0, 1)   # 색 되살리기용 (넉넉한 경계)
alpha = np.clip((0.42 - mag) / 0.16, 0, 1)  # 투명도용 (빡빡한 경계 → 연한 그림의 분홍 테두리 없음)
# 가장자리 번짐: 반투명 픽셀 = 그림색 × a + 마젠타 × (1 − a) 로 보고 그림색만 되살림
M = np.array([255.0, 0.0, 255.0])
a = np.maximum(soft, 0.3)[..., None]
un = (im - (1 - a) * M) / a
# 되살린 색이 초록으로 넘어가지 않게: G 가 R · B 둘 다보다 커지면 회색 쪽으로 눌러 줌
un[..., 1] = np.minimum(un[..., 1], np.maximum(un[..., 0], un[..., 2]))
im = np.where((soft < 1)[..., None], np.clip(un, 0, 255), im)
rgba = np.dstack([im, alpha * 255]).astype(np.uint8)

# 가로 띠 찾기: 줄마다 불투명 비율
rows = (alpha > 0.5).mean(axis=1)
on = rows > 0.02
bands, start = [], None
for y, v in enumerate(on):
    if v and start is None: start = y
    if not v and start is not None:
        if y - start > 20: bands.append((start, y))
        start = None
if start is not None and len(on) - start > 20: bands.append((start, len(on)))
bands = sorted(bands, key=lambda t: t[1] - t[0], reverse=True)[:len(names)]
bands.sort()
os.makedirs(out, exist_ok=True)
for (y0, y1), n in zip(bands, names):
    strip = rgba[y0:max(y0 + 1, y1 - 3)]  # 아래 끝 줄(마젠타와 섞인 선) 버림
    cols = np.where((strip[..., 3] > 128).any(axis=0))[0]
    x0, x1 = (cols[0], cols[-1] + 1) if len(cols) else (0, strip.shape[1])
    img = Image.fromarray(strip[:, x0:x1])
    img.save(os.path.join(out, n + ".png"))
    print(n, img.size)
