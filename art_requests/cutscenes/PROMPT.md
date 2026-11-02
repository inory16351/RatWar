# 컷씬 슬라이드 — 이미지 요청 (시트 8장, 각 2×2 = 4컷)

참고: `ref_keyart.png` (키 아트 — 그림체 기준) · `ref_lobby.jpg`
1차(`v1_rejected/`)는 지브리풍으로 세밀해져서 게임 그림체와 달라 버림 → 키 아트 기준 플랫 매트로 다시
공통 앞부분(`COMMON`) + 시트별 4컷 설명. 받은 뒤: `<시트>.png` 로 저장 → `tools/cut_slides.py` 로 4등분 → `Resources/Art/out/cut/<시트>_<1~4>.jpg`

세계관: 남쪽 **라테리아 공화국**(쥐) vs 북쪽 **펠리니아 제국**(고양이, 총통이 다스림). 2차 대전 모티브의 가상 전쟁.

## COMMON
```
Create ONE image that is a 2x2 grid of FOUR separate cinematic story panels (comic-style cutscene stills) for a 2D cartoon war game where rats fight cats in a fictional WW2-style war.
STYLE — MUST MATCH the attached key art (ref_keyart.png) as if painted by the same artist on the same day: flat matte 2D illustration, large simple shapes, VERY LOW detail, soft flat color areas with at most one or two flat shading tones, no line-art outlines (or only very soft dark edges), NO fine texture, NO hatching, NO glossy highlights, NO sparkles, NO anime / Ghibli / storybook look, simple faces with small dot eyes. Desaturated muted palette: olive drab, khaki, mud brown, warm grey, smoke beige, dusty sky with a warm orange haze; depth is made with flat grey haze layers, distant shapes become flat silhouettes. Keep backgrounds simple and hazy, not busy.
Characters: RAT soldiers of the Ratteria Republic = drawn EXACTLY like the rats in the key art: grey-brown upright rats with big round pink ears, pink nose, olive round helmets and olive WW2 uniforms, rifles. CAT soldiers of the Felinia Empire = upright anthropomorphic grey/black cats in dark grey-blue imperial uniforms and spiked grey helmets, dark red armbands with a simple white cat-paw emblem. Cartoon violence only, no gore.
Layout: each of the 4 panels is a 16:9 landscape frame; the four panels are separated by THICK solid pure black gutters (about 24px) and a black border around the whole sheet. Panels are numbered left-to-right, top-to-bottom: 1 top-left, 2 top-right, 3 bottom-left, 4 bottom-right. NO text, NO speech bubbles, NO captions, NO letters anywhere.
Panels:
```

## prologue — 프롤로그
```
1 A peaceful rat village at sunset with red roofs and cheese market stalls; in the distant northern sky, a swarm of dark grey bomber planes with cat-paw emblems approaches.
2 A huge rally in a grey imperial square: the cat DICTATOR (a large fat grey cat with a peaked cap, medals and a red sash) shouting from a balcony with dark red banners behind, rows of cat soldiers saluting below.
3 Rat refugees with suitcases and carts streaming down into a big round sewer entrance at night, rat soldiers guiding them with lanterns.
4 An underground rat command bunker lit by lanterns: an old rat general with a white mustache and a young rat officer leaning over a big campaign map on a wooden table, determined faces.
```

## b1_dunk — 됭크 해변 철수 (됭케르크)
```
1 A long sandy beach crowded with exhausted rat soldiers waiting in lines toward the sea, smoke columns rising behind the dunes.
2 A flotilla of tiny civilian boats (fishing boats, sailboats, a ferry) crewed by rats sailing toward the beach to rescue them.
3 Cat dive bombers swooping over the beach, sand explosions, rats ducking in the dunes.
4 A small group of rat soldiers with rifles holding a sandbag line on the dunes as the rearguard, looking back at the last boats leaving.
```

## b2_stalgard — 스탈가르드 시가전 (스탈린그라드)
```
1 A ruined industrial city in winter: broken factory walls, collapsed chimneys, snow falling, fires glowing in windows.
2 A rat sniper hidden in the rubble of an upper floor, aiming through a hole in a brick wall, cold breath visible.
3 Close-quarters fight in a ruined apartment hallway: rats and cats facing each other around a corner, a grenade mid-air.
4 Rat soldiers raising a small red-and-cream republic flag on top of a ruined building at dawn, the city smoking below.
```

## b3_arven — 아르벤 숲 포위전 (벌지 전투)
```
1 A dense snowy pine forest at dawn, thick fog, rat soldiers in white winter capes in foxholes, shivering.
2 Cat tanks with snow camouflage and cat infantry emerging from the fog between the trees.
3 A surrounded rat position: a rat officer reading a message from a cat envoy holding a white flag, and answering with a defiant grin.
4 The clouds break, sunlight floods the snowy forest, and rat fighter planes roar overhead as the rats cheer.
```

## b4_kurk — 쿠르크 평원 전차전 (쿠르스크)
```
1 A vast golden steppe at summer dawn with dozens of tanks on both sides lined up on the horizon, dust clouds.
2 Rat engineers digging long anti-tank ditches and placing mines in the wheat field.
3 A massive tank clash: rat tanks (olive, round turrets) and cat tanks (grey, angular) firing at close range, burning wrecks, black smoke.
4 Exhausted rat tank crews sitting on their tank at sunset, looking over a field of burned-out cat tanks.
```

## b5_norman — 노르만 해안 상륙 (노르망디)
```
1 A huge fleet of rat landing craft and warships crossing a grey stormy sea at dawn.
2 Inside a landing craft: tense rat soldiers in helmets, one praying, the ramp about to drop.
3 The beach under fire: concrete cat bunkers on the cliffs, steel beach obstacles, rats wading ashore, water splashes from bullets.
4 Rat soldiers climbing the cliff with ropes and ladders, a bunker exploding at the top.
```

## b6_arn — 아른 다리 강습 (마켓 가든)
```
1 Hundreds of round parachutes filling the sky over green fields and windmills as rat paratroopers drop.
2 Rat paratroopers running through a small town street toward a long steel bridge.
3 Rats holding the end of the bridge behind sandbags and a broken tram while cat tanks attack from the far side.
4 Cat soldiers on the bridge pushing forward; a lone rat radio operator calling for help, the bridge burning behind him.
```

## b7_felin — 펠린부르크 함락 (베를린)
```
1 Rat artillery and rocket trucks firing at night toward the grey cat capital on the horizon, the sky lit orange.
2 Street fighting among grey imperial buildings with collapsed columns, rat tanks rolling past a fallen giant cat statue.
3 The cat DICTATOR (fat grey cat, peaked cap, medals, red sash) in a dark underground bunker, panicking and pounding a map table, cat officers backing away.
4 Rat soldiers raising their red-and-cream flag on the roof of the domed imperial palace at sunrise, the city in ruins below.
```

## epilogue — 승리 · 패배
```
1 Victory parade in the rat capital: rat soldiers marching, confetti, civilians cheering from balconies, cheese garlands.
2 A quiet rat memorial field with small wooden crosses and helmets on rifles at sunset, a veteran rat laying flowers.
3 Defeat: cat soldiers marching through the ruined rat sewer city, dark red banners hanging over the tunnel entrances.
4 Defeat: a few surviving rat soldiers retreating through dark tunnels by lantern light, vowing to return.
```
