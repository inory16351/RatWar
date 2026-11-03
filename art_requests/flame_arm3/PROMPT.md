# 화방 팔 7차 — 고양이 화방 팔 느낌의 쥐 팔 (코덱스)

6차(`flame_arm2/arm.png`)까지는 분사관이 길고 팔이 몸통과 따로 놀아 이상함 → 사용자: 고양이 화방 팔(`rigs/cat_catflame/parts/side/arm.png`) 느낌으로 쥐 버전 새로
리그의 쥐 옆모습 `arm` 파츠는 "아래팔 + 무기"만 (위팔 · 소매는 몸통 그림에 있음, 가슴 앞에서 분홍 아래팔이 나옴)
참고: `ref_cat_arm.png` (자세 · 크기 · 짧은 분사관) · `ref_rat_arm.png` (지금 쥐 소총 팔 파츠 = 그림체 · 붙는 자리) · `ref_rat_body.png` (그 파츠가 붙은 쥐 전체)
받은 뒤: `arm.png` → 마젠타 빼서 `out/fx/arm_flame.png`

## ARM
```
Create ONE sprite image for a cute flat 2D side-view cartoon game about mice. Match the attached references exactly in painting style: flat cel-shaded, soft matte colors, no outlines.
ref_rat_body.png shows the mouse soldier this sprite belongs to. ref_rat_arm.png is the sprite part currently used for his forearms: two pink mouse forearms with paws coming out in front of the chest, holding a rifle. This new sprite REPLACES ref_rat_arm.png, so it must attach to the body in exactly the same place and look like the same mouse: same pink fur forearms and paws, same scale, the forearms starting at the LEFT edge exactly like in ref_rat_arm.png.
Instead of the rifle, the two paws hold a short dark-grey metal sprayer wand the way ref_cat_arm.png holds it: compact, close to the chest, held almost level and tilted only slightly upward, the nozzle pointing to the RIGHT. The near paw grips the rear pistol handle under the wand, the far paw (slightly darker, in shadow) grips the front of the wand. The wand sticks out to the right about as far as the rifle in ref_rat_arm.png. A short black rubber hose hangs down from under the rear handle. Copy the pose, proportions and the dark metal wand look of ref_cat_arm.png, but with the pink mouse paws of ref_rat_arm.png (no gloves, no cat coat, no strap).
No body, no head, no sleeve above the elbow, no other objects. One single sprite on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text.
```
