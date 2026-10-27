# 소품 추가 — ChatGPT 요청 (시트 2장)

## 첨부
- `ref_existing_props.jpg` (지금 쓰는 소품 16개 = 그림체 · 각도 · 크기 기준). **두 요청 모두 이것 첨부**
- 같은 대화에서 A → B 순서로 이어서 요청하면 그림체가 더 잘 맞음

---

## 시트 A — 맵 안 엄폐물 16종 (전투에 쓰임: 낮은 엄폐 8 + 높은 엄폐 8)

```
Attached image = STYLE REFERENCE: props from my 2D WW2 "rats vs cats" war game (flat 2-3 tone shapes, no outlines, no texture, muted olive / khaki / brick / brown palette, seen from the front at a slightly high 3/4 angle, sitting on the ground). Create a NEW sprite sheet in exactly this style, scale and angle: a 4x4 grid of 16 separate battlefield props, each one centered in its own cell with its base touching the bottom of the cell, generous empty space between cells, nothing touching or overlapping. Solid pure magenta #FF00FF background everywhere.
Row 1 (low cover, knee high): 1 stack of oil drums, 2 hay bale pair, 3 low rubble pile of bricks and planks, 4 overturned wooden cart.
Row 2 (low cover, knee high): 5 low fieldstone farm wall segment, 6 log pile, 7 stack of wooden supply crates stenciled with a cheese symbol, 8 sandbag corner (L shape).
Row 3 (high cover, taller than a soldier): 9 broken brick corner wall with a window hole, 10 burned-out car wreck, 11 small wooden tool shed (damaged), 12 big round haystack.
Row 4 (high cover, taller than a soldier): 13 stone well with wooden roof, 14 large splintered tree stump with roots, 15 broken church wall section with arched window, 16 tall advertising pillar with torn posters.
Same flat style, no outlines, no text, no characters, no shadows on the background.
```

받은 뒤: `sheet_a.png` 로 저장

---

## 시트 B — 맵 바깥 경치 12종 (안 지나다님, 분위기용 · 쥐 / 고양이 테마)

```
Same style as before (attached reference). Create another NEW sprite sheet: a 4x3 grid of 12 separate large SCENERY props for the outskirts of the battlefield, each centered in its own cell with its base touching the bottom of the cell, generous empty space between cells, nothing touching. Solid pure magenta #FF00FF background.
Row 1: 1 bombed two-storey village house ruin, 2 wrecked biplane nose-down in the ground, 3 broken windmill, 4 crashed observation balloon basket with deflated envelope.
Row 2: 5 giant rusty mousetrap lying in the field (war memento), 6 cat army supply tent (torn, with a cat-paw emblem on the flag), 7 field ambulance wagon, 8 anti-tank hedgehog obstacles group.
Row 3: 9 broken bridge stone arch segment, 10 water tower on wooden legs (leaning), 11 shell-scarred tree line (three trees), 12 cheese factory chimney ruin with a round cheese sign.
Same flat 2-3 tone style, no outlines, no text, no characters.
```

받은 뒤: `sheet_b.png` 로 저장

---

## 처리 (제가 함)
- 칸 안 큰 덩어리만 자르고 마젠타 제거 → `Resources/Art/out/props/` (맵 안) · `out/deco/` (바깥)
- 시트 A 는 새 맵 기호로 등록 (낮은 엄폐 · 높은 엄폐 · 부서짐 여부) → 1막 맵 · 생성 도구에 섞어 배치
- 시트 B 는 바깥 경치 무작위 배치 목록에 추가
