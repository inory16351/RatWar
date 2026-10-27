# 붉은 물감 튐 · 단면 — 이미지 요청 (시트 1장)

> 1차 요청(피 · 상처 · 절단 표현)은 심의에 걸림 → 그림 자체는 "붉은 물감 · 잉크 튐"과 "소시지 단면" 같은 중립 모양으로 받음.
> 색은 게임에서 어차피 검붉게 입히니(틴트) 모양만 맞으면 됨.

## 첨부
- `ref_gore.jpg` 는 쓰지 말 것 (게임 화면 속 핏자국 · 시체 때문에 다시 걸릴 수 있음). **첨부 없이** 요청

---

## 시트 — 4×4, 16칸

```
Create a sprite sheet of flat vector shapes for a cute 2D cartoon game: flat 2-3 tone shapes, no outlines, no texture, simple clean edges. A 4x4 grid of 16 separate sprites, each centered in its own cell with generous empty space between cells, nothing touching or overlapping. Solid pure magenta #FF00FF background everywhere.
All sprites are made of thick DARK RED PAINT (#7a1018 base with lighter #9a1c24 highlights), like spilled ink or paint.
Row 1 — paint PUDDLES seen straight from above, flat on the ground: 1 small round puddle, 2 medium irregular puddle, 3 large wide puddle with a darker center, 4 long smeared streak puddle.
Row 2 — paint SPLAT marks seen straight from above: 5 small splat with little round dots around it, 6 star-shaped splat, 7 directional spray of many small dots, 8 cluster of scattered round dots.
Row 3 — paint SPLASHES in the air, seen from the side: 9 splash bursting outward from a point, 10 arcing jet stream of paint, 11 three falling teardrop drops of different sizes, 12 small cloud of fine mist dots.
Row 4 — round CROSS-SECTION caps, like a sliced sausage end seen at an angle: 13 large round cap (dark red ring, pink-red center, one small white dot in the middle), 14 medium cap (same look), 15 small cap (same look), 16 four small irregular dark red lumps.
Same flat style, no outlines, no text, no characters, no shadows on the background.
```

받은 뒤: `sheet.png` 로 저장

### 또 걸리면
- 4줄(단면)을 빼고 1~3줄만 4×3 으로 요청 → 단면은 나중에 따로
- "DARK RED PAINT" → "BURGUNDY INK" 로 바꿔 보기 (색은 게임에서 다시 입힘)

---

## 처리 (제가 함)
- 칸 안 큰 덩어리만 자르고 마젠타 제거 → `Resources/Art/out/fx/` (`pool0~3`, `splat0~3`, `spray0~3`, `stump_neck · stump_arm · stump_leg`, `gib0~3`)
- 1줄 웅덩이: 시체 밑 · 잘린 부위 밑 번지는 웅덩이 (지금은 `blood0` 을 색만 바꿔 씀)
- 2줄 자국: 떨어진 방울 · 맞은 자리 바닥 자국
- 3줄 공중 튐: 맞은 순간 튐 · 잘린 자리에서 뿜어지는 줄기
- 4줄 단면: 잘린 부위 자리(목 · 어깨 · 무릎)에 덮어 붙여 끊긴 단면이 보이게 + 조각
