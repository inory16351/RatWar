# 화염방사기 · 칼 찌르기 · 부비트랩 — 그림 요청 (시트 1장, 코덱스)

지금 화방 쥐(공병 + 화염방사기)는 소총 든 팔 그림 그대로라 불만 날아가고, 칼 찌르기는 반짝 효과만 있음.
→ 팔 그림 두 개(화방 · 칼) · 화방 연료 배낭 · 효과 그림 두 개(불줄기 · 칼 궤적) · 부비트랩을 받아 리그 팔을 바꿔 끼우고 등에 배낭을 메게 하고 찌르기 · 뿜기 자세를 붙임. 부비트랩은 지금 아이콘 대신 바닥 소품 그림으로.


## SHEET — 3×2, 6칸

```
Create ONE sprite sheet image for a 2D side-view cartoon war game about mice soldiers. Match the attached reference image EXACTLY in style: flat cel-shaded painted sprites, soft matte colors, no black outlines, simple shapes, same scale and same khaki/olive sleeve colors as the reference arms.
Layout: a 3x2 grid (3 columns, 2 rows) of 6 separate sprites, each centered in its own cell with lots of empty space around it, nothing touching the cell edges or each other. Solid pure magenta #FF00FF background everywhere (no gradient, no shadow on the background).
Cell 1 (top row, left): a mouse soldier's ARM seen from the side, pointing to the RIGHT, khaki-olive uniform sleeve with a pink mouse paw at the end, holding a FLAMETHROWER WAND (a short metal fuel lance with a nozzle tip, a small pilot flame at the nozzle, a thin black hose hanging from the back end). Same pose, angle and size as the reference arms: the shoulder end on the left, the weapon pointing right.
Cell 2 (top row, middle): the same mouse soldier ARM seen from the side, pointing to the RIGHT, stretched forward in a stabbing thrust, the pink paw gripping a short COMBAT KNIFE (dark handle, steel blade pointing right). Shoulder end on the left.
Cell 3 (top row, right): a FLAMETHROWER FUEL BACKPACK seen from the side, as worn on a soldier's back (the wearer would face RIGHT, so the straps are on the right side): two tall olive-green metal fuel tanks side by side with rounded tops, a small round pressure tank between them, canvas shoulder straps, and a short black hose coming out of the bottom. No character, only the backpack.
Cell 4 (bottom row, left): a horizontal JET OF FIRE seen from the side, shooting from left to right: narrow and bright yellow-white at the left end, widening into a big billowing orange and red flame cloud at the right end with a few dark smoke puffs at the very tip. Flat cartoon flames, long and wide (about 3 times longer than tall).
Cell 5 (bottom row, middle): a single bright crescent SLASH ARC swoosh, pale yellow-white with a soft warm edge, curving from top-left to bottom-right, like a quick knife swing trail. No characters.
Cell 6 (bottom row, right): a hidden BOOBY TRAP lying on the ground, seen from a slightly high 3/4 angle: a small round olive-green land mine half buried in a patch of dirt, with a thin tripwire stretched from it to a short wooden stake, a few dry grass blades around. Small and low, wider than tall.
No text, no labels, no grid lines, no extra objects.
```

받은 뒤: `sheet.png`

---

### 첨부
- `arms_ref.png` (2차 땐 첨부 안 함: 총 든 팔이 심의에 걸림) — 지금 쓰는 옆모습 팔 3개 (왼쪽부터 쥐 특공 기관단총 · 쥐 공병 소총 · 고양이 화방병). 그림체 · 크기 · 소매 색을 이것에 맞춤



## FLAME — 2×1, 2칸 (다시 뽑기, 2026-11-03)

1차 화방 팔은 짧은 권총 같아 작고 이상함 → 2차 대전 미군 M2-2 화염방사기 생김새로 다시.
(실물: 연료통 두 개를 나란히 세우고 그 사이 위쪽에 작은 압축가스통, 모래시계 모양 등판 틀 · 육각 뚜껑,
긴 분사관(wand)에 앞뒤 손잡이 두 개 · 끝에 점화 실린더, 뒤 손잡이 밑으로 굵은 호스)
> 팔 + 군복 + 무기 모양을 같이 그리면 출력 심의(violence)에 걸림 (화염방사기 · 분무기 문구 둘 다) →
> **물건만** "1940년대 농약 분무기 부품"으로 받고, 분사관은 1차 팔 그림의 소매 · 손 밑에 겹쳐 붙임 (`compose_flame.py`)

(아래 두 블록을 따로 뽑음: 같이 뽑으면 화염방사기로 읽혀 걸림)

받은 뒤: `flame_v2.png` → `out/fx/arm_flame.png` · `tank.png` 바꿔 끼움 (1차는 `v1/`)

---

## COMMONX

```
Create ONE sprite image for a cute flat 2D cartoon game. Match the attached reference image in painting style only: flat cel-shaded, soft matte colors, no black outlines, simple shapes. Do not draw any arms, hands, people or animals. One single object, centered with lots of empty space around it, on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text, no labels, no extra objects.
```

## WAND

```
Create ONE sprite image for a cute flat 2D cartoon game. Match the attached reference image in painting style only: flat cel-shaded, soft matte colors, no black outlines, simple shapes. Do not draw any arms, hands, people or animals. One single object, centered with lots of empty space around it, on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text, no labels, no extra objects.
Object: a LONG straight olive-green metal pipe tool, seen exactly from the side, lying horizontally, its tip pointing to the RIGHT. Very long and thin (about 8 times longer than tall). From left to right: a rear pistol-style handle with a lever hanging down, a long straight tube, a second small handle hanging down under the middle, a thick short cylinder with a knurled ring near the front, and a small cone tip at the right end. A thick black rubber hose comes out of the bottom of the rear handle and curves down and back to the left.
```

## TANK2

```
Create ONE sprite image for a cute flat 2D cartoon game. Match the attached reference image in painting style only: flat cel-shaded, soft matte colors, no black outlines, simple shapes. Do not draw any arms, hands, people or animals. One single object, centered with lots of empty space around it, on a solid pure magenta #FF00FF background everywhere (no gradient, no shadow). No text, no labels, no extra objects.
Object: a vintage 1940s olive-green BACKPACK UNIT seen exactly from the side, standing upright (its shoulder straps and carrying frame on the RIGHT side): two tall olive-green steel cylinders side by side with rounded tops and small hexagonal screw caps, a smaller grey-green cylinder lying horizontally across the top between them, metal bands around the cylinders, an hourglass-shaped steel carrying frame, canvas shoulder straps, a small valve at the bottom, and a thick black hose leaving the bottom and curving to the right. Tall object.
```

## 처리 (제가 함)
- `tools/cut_grid.py sheet.png <폴더> 3 2 arm_flame arm_knife tank jet slash trap`
- 팔 두 개 → `Resources/Art/out/fx/arm_flame.png` · `arm_knife.png` (리그 `arm` 그림을 그 자리에서 바꿔 끼움)
- 배낭 → `out/fx/tank.png` (화방 쥐 몸통 뒤에 붙임)
- 부비트랩 → `out/props/` 아니면 `out/fx/trap.png` (쥐에게만 보이는 바닥 표시를 아이콘 대신 이 그림으로)
- 불줄기 → `out/fx/jet.png` (총구에서 표적까지 늘여 깜빡이며 붙임), 칼 궤적 → `out/fx/slash.png`
