# 전략 지도 배경 2차 — 13×6 칸으로 넓힘 (1장, 16:9, 코덱스)

예전 지도(`../war_map/map.png`, 10×4 칸) → 칸을 78개로 늘려 지형 배치를 다시 그림
참고: `ref_oldmap.png` (예전 지도 — 그림체 그대로) · `ref_keyart.png` · `ref_lobby.jpg` · `ref_layout2.png` (새 칸 배치: 색 = 지형)
배치 안내는 `tools/campaign/build.py` 가 만듦. 받은 뒤: `map.png` → `Resources/Art/out/war_map.jpg`

## MAP
```
Create ONE wide 16:9 illustration (landscape, 1536x864 or larger): a top-down strategic war map of a FICTIONAL continent for a 2D cartoon war game where rats fight cats (WW2 mood).
STYLE — MUST MATCH the attached old map (ref_oldmap.png) EXACTLY, as if it is the same printed map redrawn larger: flat matte 2D illustration, large simple shapes, VERY LOW detail, flat color areas, no outlines, no fine texture, plain aged beige paper with faint fold creases and a soft vignette, desaturated olive drab, khaki, mud brown, warm grey, dusty slate-blue water.
LAYOUT — follow the attached ref_layout2.png very closely: it is a hex grid where each hexagon's COLOR tells what terrain must be painted at that exact spot of the map (do NOT draw the hexagons themselves). Color legend: light grey-white = snowy pine forest and snowfields; dark green = pine/leafy forest; pale olive = open grassy plains; brown = trench lines with barbed wire and craters; dark red = the cats' imperial capital city (grey stone towers, a big domed palace, dark red banners with a simple cat-paw emblem); grey-mauve = grey stone cities and ruined factory districts with chimneys; grey = concrete coastal bunkers on a rocky coast; steel blue = a wide river (one continuous river flowing left to right across the middle of the map); blue-grey lavender = stone bridges over the river; mustard = farmland fields; terracotta = small villages with red roofs; olive = low hills; sand = sandy beaches; slate blue-grey = small harbors with piers; very dark brown = the rats' underground sewer city (round manhole covers, tunnel mouths) at the bottom center.
The TOP edge of the map is a northern sea coast (cat empire), the BOTTOM edge is a southern sea coast (rat homeland): show a thin strip of dusty slate-blue sea along the top and bottom edges and at the corners.
Small tiny icons only, no characters, no arrows, no unit markers, NO hex grid, NO text, NO labels, NO legend. Keep each terrain patch simple and readable so hex tiles can be drawn on top later.
```
