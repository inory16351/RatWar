# 양옆 날개 배경층 — ChatGPT 요청

## 첨부
- `ref_style_backdrop.jpg` (그림체 기준: 지금 쓰는 후경 띠들). **이것만 첨부**
- `where_it_goes_ingame.png` 는 참고용 (어디에 쓰이는지). 첨부 안 해도 됨

## 프롬프트 (그대로 붙여넣기)

```
Attached image = STYLE REFERENCE: horizontal parallax backdrop strips from my 2D WW2 "rats vs cats" war game (flat 2-3 tone shapes, no outlines, muted olive / khaki / brown palette, soft haze). Please create a NEW image in exactly this style: a sprite sheet of THREE separate horizontal "side wing" strips that will stand at the LEFT and RIGHT flanks of the battlefield, receding into the distance from a low camera. Each strip is a wide panorama of a ruined WW2 battlefield edge: dead splintered trees, broken wooden fences, ruined brick wall chunks, leaning telegraph poles, sandbag mounds, a wrecked cart, scrub bushes and earth mounds, with a ragged organic top silhouette and a flat ground band along the bottom so it blends into the ground. Strip 1 (top) = far: light hazy desaturated tones, low profile. Strip 2 (middle) = mid distance: medium tones. Strip 3 (bottom) = near: darker richer tones, taller objects. No sky, no characters, no text. Each strip spans the full width and the strips are separated by clear gaps. Solid pure magenta #FF00FF background everywhere outside the strips. Same flat style, no outlines, no texture. Wide landscape canvas.
```

## 받은 뒤
- 이 폴더에 `sheet.png` 로 저장 (또는 아무 이름으로 두고 알려 주기)
- 띠 셋이 서로 떨어져 있고 배경이 마젠타면 OK. 띠가 붙어 나오면 "make the gaps between strips wider" 로 다시 요청
- 처리: `python -I tools/cut_strips.py art_requests/side_wings/sheet.png RatWarUnity/Assets/Resources/Art/out/backdrop wing_far wing_mid wing_near`
