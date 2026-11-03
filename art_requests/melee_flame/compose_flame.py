# 화방 팔 2차: 코덱스가 팔 + 무기를 같이 못 그려서(심의) 물건만 받고 여기서 합침
#   wand.png (분사관) 를 1차 칼 팔(arm_knife.png)의 소매 · 주먹 밑에 겹침. 칼날은 잘라 냄
#   tank2.png (배낭) 는 마젠타만 빼서 tank.png
# 사용: python -I compose_flame.py <out/fx 폴더>
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

def cut(path):
    im = np.asarray(Image.open(path).convert('RGB')).astype(float)
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    a = 1 - np.clip(((np.minimum(r, b) - g) - 60) / 100, 0, 1)
    M = np.array([255, 0, 255.])
    fg = np.clip(np.where(a[..., None] > 0.05, (im - (1 - a[..., None]) * M) / np.maximum(a[..., None], 0.05), 0), 0, 255)
    lab, n = ndimage.label(a > 0.3)
    sizes = ndimage.sum(np.ones_like(a), lab, range(1, n + 1))
    keep = ndimage.binary_dilation(np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 300]), iterations=3)
    a = a * keep
    ys, xs = np.where(a > 0.02)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]; fg = fg[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    mean = fg[a > 0.5].mean(0)
    fg = np.where(a[..., None] > 0.02, fg, mean)
    return Image.fromarray(np.dstack([fg, a * 255]).astype(np.uint8), 'RGBA')

out = sys.argv[1]
wand = cut('wand.png'); tank = cut('tank2.png')
tank.save(out + '/tank.png')
arm = Image.open(out + '/arm_knife.png')
arm = arm.crop((0, 0, 232, arm.height))            # 칼날 · 날밑 빼고 소매 + 주먹만
ARM_W = 232; K = ARM_W * 2.4 / wand.width          # 분사관 길이 = 팔의 2.4배
wand = wand.resize((round(wand.width * K), round(wand.height * K)), Image.LANCZOS)
# 분사관 뒤 손잡이 위(관과 만나는 곳)를 주먹 가운데에
gx, gy = round(wand.width * 0.15), round(wand.height * 0.41)
fx, fy = 190, 78
ox, oy = fx - gx, fy - gy
x0, y0 = min(0, ox), min(0, oy)
W = max(arm.width, ox + wand.width) - x0; H = max(arm.height, oy + wand.height) - y0
c = Image.new('RGBA', (W, H), (0, 0, 0, 0))
c.alpha_composite(wand, (ox - x0, oy - y0))
c.alpha_composite(arm, (-x0, -y0))
# 다른 총처럼 두 손: 먼 쪽 팔은 아래팔(소매 끝 + 주먹)만, 몸 쪽 아래에서 비스듬히 올라와 앞 손잡이를 쥠 (살짝 어둡게)
#   칸 순서: 분사관 → 먼 아래팔 → 가까운 팔
import math
src = Image.open(out + '/arm_knife.png')
sa = np.asarray(src).astype(float)
dark = (sa[..., :3].max(-1) < 90) & (np.arange(sa.shape[1])[None, :] > 150)     # 주먹 밑 칼자루 빼기
sa[..., 3] = np.where(dark, 0, sa[..., 3]); sa[..., :3] *= 0.8
src = Image.fromarray(sa.astype(np.uint8), 'RGBA')
# 아래팔 = 민 소매(40~112, 길게 늘임) + 소매 끝동 · 주먹(112~232, 그대로). 팔꿈치 쪽은 가까운 주먹 뒤에 숨음
CUT, SLEN = 112, 150
sleeve = src.crop((40, 0, CUT, src.height)).resize((SLEN, src.height), Image.LANCZOS)
fist = src.crop((CUT, 0, 232, src.height))
fore = Image.new('RGBA', (SLEN + fist.width, src.height), (0, 0, 0, 0))
fore.alpha_composite(sleeve, (0, 0)); fore.alpha_composite(fist, (SLEN, 0))
fv = np.asarray(fore).astype(float); fv[:, :40, 3] *= np.linspace(0, 1, 40)[None, :]   # 팔꿈치 끝 각진 모서리를 흐리게
fore = Image.fromarray(fv.astype(np.uint8), 'RGBA')
ANG = 8                                                                          # 팔꿈치(왼쪽)가 살짝 아래로
fx0, fy0 = SLEN + (190 - CUT), 78                                               # 아래팔 그림에서 주먹 가운데
fw, fh = fore.size
fore = fore.rotate(ANG, resample=Image.BICUBIC, expand=True)
t = math.radians(ANG); cx, cy = fw / 2, fh / 2; dx_, dy_ = fx0 - cx, fy0 - cy
rx = cx + dx_ * math.cos(t) + dy_ * math.sin(t) + (fore.width - fw) / 2      # 돌린 뒤 주먹 자리 (PIL 은 반시계)
ry = cy - dx_ * math.sin(t) + dy_ * math.cos(t) + (fore.height - fh) / 2
hx, hy = ox + round(wand.width * 0.565), oy + round(wand.height * 0.40)        # 앞 손잡이가 관에 붙은 곳
px_, py_ = round(hx - rx), round(hy - ry + 8)
x1, y1 = min(x0, px_), min(y0, py_)
W2 = max(-x0 + c.width + x0 - x1, px_ + fore.width - x1); H2 = max(c.height + (y0 - y1), py_ + fore.height - y1)
c2 = Image.new('RGBA', (W2, H2), (0, 0, 0, 0))
c2.alpha_composite(wand, (ox - x1, oy - y1))
c2.alpha_composite(fore, (px_ - x1, py_ - y1))
c2.alpha_composite(arm, (-x1, -y1))
c = c2
# 투명 자리 RGB 를 평균색으로 (필터링 번짐 막기)
A = np.asarray(c).astype(float); m = A[..., 3] > 128
A[..., :3] = np.where(A[..., 3:] > 5, A[..., :3], A[m][:, :3].mean(0))
Image.fromarray(A.astype(np.uint8), 'RGBA').save(out + '/arm_flame.png')
print('arm_flame', W, H, 'shoulder at', -x0, -y0, 'tank', tank.size)
