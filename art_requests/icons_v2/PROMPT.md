# 새 능력 아이콘 — 이미지 요청 (시트 1장, 4×3)

참고: `ref_knife.png` · `ref_dominate.png` (기존 아이콘 128px — 같은 그림체로)

## 2차 (1차는 그림체가 너무 달라서 다시) — 참고: `ref_existing.png` (기존 아이콘 16개)

```
Create a sprite sheet of 12 game ability icons for a 2D cartoon war game where the soldiers are rats fighting cats (WW1/WW2 trench mood).
The attached image shows 16 EXISTING icons from this game. The new icons must look like they belong to the same set: SAME simple flat cel-shaded cartoon style, bold clean shapes, only 2-3 shading tones per color, thin dark-brown outlines, LOW detail, chunky readable silhouettes, warm muted palette (olive green, brown, tan, brass, cream) with a few flat yellow/orange accent bursts like the existing muzzle flashes. NO painterly rendering, NO soft glows, NO realistic texture, NO heavy particle effects, NO dirt piles under objects unless the existing set does it. Mostly a single object per icon, like the existing grenade / bandage / dynamite icons. No frame, no border, no background plate, no text.
Layout: a 4x3 grid of 12 separate icons, each centered in its own square cell with generous empty space around it, nothing touching or overlapping. Solid pure magenta #FF00FF background everywhere (no gradients, no shadows on the background).
Row 1:
1 BARRAGE — a light machine gun (no character) firing a wide fan of 5-6 simple yellow bullet streaks.
2 BOOBY TRAP — a round black bomb with a lit fuse spark, a thin tripwire running from it to a small wooden peg.
3 SMOKE GRENADE — an olive cylindrical smoke grenade with a simple flat grey cloud puffing out of the top.
4 SHADOW DASH — a combat knife with three bold dark-purple speed lines behind it (no character).
Row 2:
5 PAINKILLER — a glass syringe with amber liquid.
6 MASS SUPPLY — a closed olive-green medical crate with a red cross and two bandage rolls on top.
7 FLARE — a red signal flare stick with a flat orange-yellow flame burst at the top and a small parachute.
8 CALL ARTILLERY — an old field telephone handset next to a red target crosshair.
Row 3:
9 WHISPER OF FEAR — a flat purple ghostly wisp shaped like a whispering mouth with a few purple sound arcs (no cat).
10 MIND CLEANSE — a flat light-blue swirl with three small white sparkle stars.
11 WEAK SPOT — a red crosshair over a cracked grey metal helmet.
12 FLAMETHROWER — a brass flamethrower gun with hose and fuel tank (no character), short flat orange flame from the nozzle, like the existing flame icon.
Same lighting and style for all 12, each icon fills about 80% of its cell.
```

받은 뒤: `sheet.png` 로 저장 → `tools/cut_grid.py` 로 잘라 `Resources/Art/out/icons/` 에 (barrage trap smoke dash painkiller supply flare artillery terror cleanse weakspot flamer)

## 3차 (2차도 아직 입체감이 강함 → 더 플랫하게)

```
Same 12 icons, same layout (4 columns x 3 rows, solid pure magenta #FF00FF background, nothing touching, no text, no frame), same subjects as the 2nd version shown in the attached sheet_v2.png.
Make them MUCH FLATTER: pure flat vector style like simple mobile game UI icons. Solid flat color fills, at most ONE darker flat shadow shape per color, NO gradients, NO glossy highlights, NO metallic shine, NO texture, NO soft glow, NO rim light. Uniform medium-thick dark brown outline (#3a2a1e) around every shape. Simple geometric shapes, minimal small details. Muted warm palette: olive green, tan, brown, brass yellow, cream, muted red, with flat yellow/orange accent shapes for flashes and fire.
1 machine gun firing a fan of bullet streaks, 2 round bomb with tripwire to a wooden peg, 3 smoke grenade with a flat grey cloud, 4 combat knife with purple speed lines,
5 syringe, 6 medical crate with red cross and two bandage rolls, 7 signal flare with small parachute and flame, 8 telephone handset with red crosshair,
9 purple ghost wisp mouth with sound arcs, 10 light-blue swirl with sparkle stars, 11 red crosshair over cracked helmet, 12 brass flamethrower with short flame.
```
