# 화방 팔(arm.png)의 먼 쪽 아래팔 소매가 몸 앞 허공에서 시작해 떠 보임 → 소매를 몸 쪽(왼쪽)으로 이어 그린 조각을 따로 만듦
# 소매 띠를 세로줄 단위로 기울기(≈13°)를 따라 왼쪽으로 이어 붙임. arm_flame.png 와 같은 크기 · 자리
# 게임에선 몸통 뒤에 그려 팔이 몸 뒤에서 나오는 것처럼 (UnitView.ArmSwap)
# 사용: python -I far_sleeve.py <out/fx 폴더>
import sys, math
import numpy as np
from PIL import Image
im = np.asarray(Image.open('arm.png').convert('RGB')).astype(float)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
a = 1 - np.clip(((np.minimum(r, b) - g) - 60) / 100, 0, 1)
X0, Y0, X1, Y1 = 39, 121, 1712, 820                      # arm_flame 이 잘린 상자
T = math.tan(math.radians(12.9))                         # 소매가 오른쪽으로 올라가는 기울기
def cy(x): return 478 - (x - 600) * T                    # 소매 띠 가운데 높이 (x=600 에서 478)
HALF = 78                                                # 띠 반 두께 (관 밑 · 아래 끝동 위)
SRC = range(640, 760)                                    # 깨끗한 소매 구간
khaki = (a > 0.5) & (r - b > 25) & (r > g - 8) & (r > 90) & (r < 215)
mean = im[khaki].mean(0)
out = np.zeros((im.shape[0], im.shape[1], 4))
for x in range(230, 660):
    xs = SRC[(x - 230) % len(SRC)]
    for dy in range(-HALF + 34, HALF):                   # 위쪽 34px 는 관이 지나가는 자리라 뺌
        ys = round(cy(xs) + dy + 12); yd = round(cy(x) + dy + 12)
        out[yd, x, :3] = im[ys, xs] if khaki[ys, xs] else mean; out[yd, x, 3] = 255
out[..., :3] *= 0.82
# 위아래 가장자리 · 왼쪽 끝은 흐리게
out[..., 3] *= 1
o = Image.fromarray(out.astype(np.uint8), 'RGBA').crop((X0, Y0, X1 + 2, Y1 + 1))
o = o.resize((838, 350), Image.LANCZOS)
A = np.asarray(o).astype(float); m = A[..., 3] > 128
A[..., :3] = np.where(A[..., 3:] > 5, A[..., :3], A[m][:, :3].mean(0))
Image.fromarray(A.astype(np.uint8), 'RGBA').save(sys.argv[1] + '/arm_flame_far.png')
