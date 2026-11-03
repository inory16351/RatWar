# 화방 팔 3차 · 4차 — 두 손으로 쥔 분사관을 한 장으로 (코덱스)

2차는 분사관 · 소매를 잘라 붙여 손이 떠 보임 → 사용자: 그림 자체를 새로
팔 + 무기 + 군복을 같이 그리면 출력 심의(violence)에 걸렸음 → "분무기 막대를 두 손으로 든 쥐 팔"로, 총 그림 참조 없이
참고: `ref_wand.png` (분사관 생김새) · `ref_sleeve.png` (소매 · 손 그림체)
받은 뒤: `arm.png` → 마젠타 빼서 `out/fx/arm_flame.png`

## ARM
```
Create ONE sprite image for a cute flat 2D side-view cartoon game about mice. Match the attached references: ref_sleeve.png for the sleeve and paw painting style (flat cel-shaded, soft matte colors, no outlines, khaki-olive sleeve with a darker cuff and a pink mouse paw), ref_wand.png for the exact shape of the long olive-green sprayer wand.
Draw ONLY a pair of mouse arms seen exactly from the side, holding the long olive-green sprayer wand from ref_wand.png level and pointing to the RIGHT, the same way a farmer holds a long garden sprayer wand with BOTH hands:
- the NEAR arm: the shoulder end at the far LEFT edge (cut cleanly, like ref_sleeve.png), khaki-olive sleeve going right, its pink paw gripping the REAR pistol handle of the wand;
- the FAR arm: only its forearm is visible, slightly darker (in shadow), coming from behind the near arm's paw, the sleeve running along under the tube, its pink paw gripping the FRONT handle under the middle of the tube.
Both paws must clearly wrap around the handles, connected to their sleeves, nothing floating. The wand is about 3 times as long as the near arm. A thick black rubber hose hangs from under the rear handle. No body, no head, no other objects.
One single sprite, centered with empty space around it, on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text.
```

---

### 4차: 총처럼 비스듬히 (사용자: 3차 `arm_v3_level.png` 는 수평이라 어색, 총처럼 기울여 들어야)
실제 M2-2: 뒤 손잡이(발사 방아쇠 · 안전쇠)를 허리 높이에서 쥐고, 앞 손잡이(점화 방아쇠)를 앞으로 뻗은 손이 받쳐
분사관 끝이 위로 들린 채 몸 앞으로 내밂 — 소총을 허리에 들고 쏘는 자세와 비슷

## ARM2
```
Create ONE sprite image for a cute flat 2D side-view cartoon game about mice. Match the attached references: ref_sleeve.png for the sleeve and paw painting style (flat cel-shaded, soft matte colors, no outlines, khaki-olive sleeve with a darker cuff and a pink mouse paw), ref_wand.png for the exact shape of the long olive-green sprayer wand.
Draw ONLY a pair of mouse arms seen exactly from the side, holding the long olive-green sprayer wand from ref_wand.png with BOTH hands at hip level, the wand TILTED UPWARD about 20 degrees, its nozzle tip pointing up and to the RIGHT, like holding a rifle at the hip:
- the NEAR arm: the shoulder end at the top LEFT (cut cleanly, like ref_sleeve.png), the upper arm going DOWN and the elbow bent, the forearm coming forward so its pink paw grips the REAR pistol handle of the wand, which is low on the left;
- the FAR arm: only its forearm is visible, slightly darker (in shadow), reaching forward and up from behind, its pink paw gripping the FRONT handle under the middle of the tilted tube.
Both paws must clearly wrap around the handles, connected to their sleeves, nothing floating. The wand is about 3 times as long as the near forearm. A thick black rubber hose hangs down from under the rear handle. No body, no head, no other objects.
One single sprite, centered with empty space around it, on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text.
```

### 5차: 아래팔만 (사용자: 4차 `arm_v4_fullarm.png` 는 팔이 이중 — 몸통 그림에 이미 위팔 · 팔꿈치까지 있음)
리그의 `arm` 파츠는 원래 "아래팔 + 총"만 (몸통 그림의 위팔 끝 = 가슴 앞 분홍 아래팔 시작에서 이어짐). 참고 `ref_body.png` = 팔 파츠 뺀 쥐 옆모습

## ARM3
```
Create ONE sprite image for a cute flat 2D side-view cartoon game about mice. Match the attached references: ref_body.png shows the mouse body this sprite will be attached to (match its flat cel-shaded style and its khaki camouflage uniform colors exactly; the sprite attaches where its pink forearm sticks out in front of the chest), ref_wand.png for the exact shape of the long olive-green sprayer wand.
Draw ONLY two mouse FOREARMS with paws holding the long olive-green sprayer wand from ref_wand.png with BOTH hands at hip level, the wand TILTED UPWARD about 20 degrees with the nozzle tip pointing up and to the RIGHT, like holding a rifle at the hip:
- the NEAR forearm: starts at the far LEFT edge (cut cleanly at the elbow, a short rolled khaki sleeve then pink fur forearm like in ref_body.png), going slightly down and to the right, its pink paw gripping the REAR pistol handle of the wand at the left;
- the FAR forearm: slightly darker (in shadow), coming from behind the near forearm, running forward under the tube, its pink paw gripping the FRONT handle under the middle of the tilted tube.
NO upper arms, NO shoulders, NO body. Both paws must clearly wrap around the handles. The wand is about 4 times as long as the near forearm. A thick black rubber hose hangs down from under the rear handle.
One single sprite, centered with empty space around it, on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text.
```

### 6차: 4차 그림 그대로, 위팔만 빼기 (사용자: 5차 `arm_v5_rejected.png` 는 소매 · 손이 이전 그림과 너무 다름)
참고 `ref_prev.png` = 4차 그림 (첨부는 이것만)

## ARM4
```
Edit the attached image ref_prev.png and keep EVERYTHING exactly the same — same painting style, same khaki sleeves with darker cuffs, same pink paws, same olive sprayer wand, same tilt, same colors, same magenta background — with ONLY this change: remove the near arm's UPPER ARM and shoulder (the big sleeve that goes up to the top-left corner). The near arm now starts at the ELBOW: its khaki sleeve begins cut cleanly at the left edge of the sprite, slightly above and to the left of the near paw, and runs as a short forearm into the cuff and the pink paw that grips the rear handle, exactly as before. Keep the far forearm and its paw on the front handle unchanged. Nothing above the near forearm on the left side.
One single sprite on a solid pure magenta #FF00FF background. No text.
```
