# Band of Mice (옛 이름 쥐들의 전쟁) — 유니티판 핸드오프 (2026-10-27)

웹판(`C:\Project\nkk_another\war`, 원본 핸드오프 `war/HANDOFF.md`)을 Unity 6 URP 로 옮긴 저장소.
규칙 · 수치 · 고양이 AI 는 웹판 `web/*.js` 를 그대로 C# 로 옮김 → **규칙을 바꿀 땐 웹판과 어긋나는지 먼저 확인**.
브랜치는 `main` 하나. 설명 · 코드 주석은 한국어. 결정할 게 생기면 선택지를 주고 물어봄.

## 실행
- 유니티 `6000.3.19f1`, 프로젝트 `RatWarUnity/`, 씬 `Assets/Scenes/Main.unity` (빌드 목록에 이 씬 하나)
- 빌드: `RatWarUnity/Builds/Windows/RatWar.exe` (git 제외, 약 250MB). MCP `manage_build build windows64` 로 다시 뽑음 (10초 안팎)
- 저장: `%USERPROFILE%\AppData\LocalLow\NKK\쥐들의 전쟁\` — `war_camp_v2.json`(사령부) · `war_battle_v1.json`(전투 중간 저장) · `Player.log`
- 웹판 비교용 미리보기: `.claude/launch.json` 의 `war-web-reference` (localhost:5322)

## 사용자가 정한 것
| 항목 | 결정 |
|---|---|
| 오브젝트 | **모든 객체는 MCP 로 하이라키에 직접 만듦.** 코드로 만드는 건 하이라키에 있는 틀(`Templates` · `Canvas/UITemplates`, 꺼 둠)을 `Tpl.Spawn` 으로 복사할 때만 |
| 그림 | 코드로 그리지 않음 (자르기 · 배경 빼기 · 크기 · 색 입히기만). 새 그림은 사용자가 ChatGPT 로 뽑아 줌 → 요청서는 `art_requests/<주제>/PROMPT.md` + 참조 그림 |
| 시점 | 진짜 원근 카메라(FOV 40), 줌 = 돌리, 가까울수록 더 숙임(26°→12°). 회전 없음, 늘 쥐(앞, −Z)에서 고양이(뒤, +Z)를 봄 |
| 원근감 | 웹처럼 배경 층(하늘 · 먼 마을 · 언덕 · 들판 · 구름 · 폭격기) + 양옆 날개 층(`WingFar/Mid/Near`) + 맵 바깥 경치 |
| 안개 | 웹 방식: 안 보이는 칸 = 바닥 어둡게 + 채도 50% 빼기 + 흐르는 연무 두 겹이 **바닥 · 물체 모두에**. 맵 바깥은 늘 안개. 회색 덮개 아님 |
| 맵 | 임무마다 크기 다름(S · M · L). 전투 맵은 전략 지도 칸의 지형을 따라감 → `docs/LEVEL_DESIGN.md` |
| 캠페인 | **전략 지도 땅따먹기** (2026-11-02): 육각 40칸, 라테리아 공화국(쥐) vs 펠리니아 제국(고양이). 매 턴 고양이 침공 → 한 곳만 직접 싸움(방어 또는 붙은 땅 공격), 나머지 침공은 자동 판정. 땅 = 턴 수입 + 특수 효과. 2차 대전 모티브 주요 전투 7개는 컷씬(그림 슬라이드 + 자막). 고양이 수도 점령 = 승리, 하수구 사령부 함락 = 패배 |
| 큰 그림체 | 컷씬 · 지도 · 배경은 키 아트(`docs/concept_deck/img/key_art.png`)처럼 플랫 매트. 세밀한 지브리풍은 퇴짜. 생성은 코덱스 CLI (`tools/gen_art.ps1`) |
| 하단 HUD | 분대 카드는 작게, 행동 버튼은 그 옆 넓은 패널 |
| 흔들림 | 짧고 약하게 (지수 감쇠 ~0.35초, 진폭 0.015) |
| 유혈 | 웹처럼 "유혈낭자": 방울 · 바닥 자국 · 번지는 웅덩이 · 날아가 바닥에 남는 부위 · 잘린 자리 뿜기 · 단면 |

## 구조 (`RatWarUnity/Assets/`)
| 위치 | 내용 |
|---|---|
| `Scripts/Data/` | `GameData`(무기 · 능력 · 병종 · 소품 기호 · 규칙, 맵 · 임무는 `Resources/Data/*.json`) · `SkillsData`(육각 스킬판) · `Camp`(사령부 기록 · 정산) · `War`(전략 지도: 칸 주인 · 침공 · 자동 판정 · 칸→임무 생성, 데이터 `Resources/Data/campaign.json`, 상태는 `CampData.war`) |
| `Scripts/Battle/` | `Battle`(턴 · 명중 · 능력 · 스트레스 · AI · 목표, async `Awaitable`) · `World`(칸 · 시야 · 엄폐 · 길찾기) · `Unit` · `BattleSave` |
| `Scripts/View/` | `BoardView`(바닥 굽기 · 소품 · 배경 · 효과 · 유혈) · `UnitView`(리그 조립 · 포즈 · 정렬) · `RigPose`(웹 `rig.js pose`) · `CameraRig` · `GroundBaker` · `FogCloud` · `ArtLib` · `View`(전투 → 화면 창구) |
| `Scripts/UI/` | `Hud` · `Lobby`(작전 탭 = 전략 지도 `Ops/WarMap` · `Ops/WarInfo` + 오른쪽 브리핑) · `Cutscene`(`Canvas/Cutscene`, 그림 = `Resources/Art/out/cut/<시트>_<1~4>.jpg`) · `TitleScreen` · `EndScreen` · `Tooltip` · `UIKit` |
| `Scripts/Core/` | `GameRoot`(화면 흐름 · 글꼴 · 이어하기) · `Tpl`(틀 복사) · `Audio` · `Loc`(글자 테이블) |
| `Shaders/SpriteFog.shader` | 모든 판때기 셰이더: 시야 안개(`_FowTex` · `_MistTex` · `_FowRect` · `_FowOn`) · `_FowDark` |
| `Resources/Art/` | `rigs/<종>[_병종]/rig.json + parts/<시점>/` · `out/`(소품 · 경치 · fx · 배경 · ui, 목록 `manifest.json` · `assets.json`) · `audio/` |
| `Editor/ArtImporter.cs` | `Resources/Art` 그림 가져오기 규칙 (스프라이트 · 9분할 · 지형 반복) |
| `tools/maps/build_maps.js` | 맵 · 임무 JSON 생성 + 검사 → `Resources/Data/` |

## 글자 테이블 · 번역 (2026-11-03)
- 화면 글자는 전부 키: `Resources/Tables/<언어>/<묶음>.json` (`ko en ja zh ru fr de es`, 묶음 = battle · camp · campaign · epilogue · game · hud · scene · ui). 코드는 `Loc.T("키")` · `Loc.F("키", 값…)`
- 자리 `{0}` · 숫자 모양 `{0:0.#}` · 한국어 조사 `{0:은}` `{0:으로}` `{0:이라}` 등 (받침 보고 고름, 다른 언어에선 빠짐)
- 정의 값(특성 · 계급 · 병종 · 능력 · 무기 · 임무 · 지도 이름 등)은 키로 저장 → 보여 줄 때 `Loc.T(...)`. 쥐 기록의 이름 · 고향 · 사연도 키(`r.Name` · `r.Home` · `r.Story`), 예전 저장은 한국어 그대로 보임
- 장면에 박힌 고정 글자는 `LocText`(키 + 붙일 때의 부모 이름). 코드가 덮어쓰는 글자엔 붙이지 않음
- 없는 키 → 한국어 → 키 그대로. 처음 켤 땐 운영체제 언어. 중국어 · 일본어 · 러시아어 글리프는 OS 글꼴로 대체(글꼴 메타의 fontNames)
- 다듬기 · 번역 · 검수: `python -I tools/loc_pipe.py polish | translate <언어…> | review <언어…>` (agy CLI: 번역 클로드, 다듬기 · 검수 제미나이). 키 · 자리 · 태그가 원문과 다르면 다시 시키고 그래도 틀리면 그 묶음은 건너뜀
- 새 글자를 넣으면 ko 테이블에 키 추가 → `translate` 로 다른 언어 채움 (이미 있는 키도 다시 번역하므로 필요하면 손으로)
- 숨은 병종 조합(통신병 · 주술사 등) 조건은 화면에 쓰지 않음

## 에필로그 · 타이틀
- 전쟁이 끝나면 승리/패배 컷씬 뒤 `Epilogue.Build` → 살아남은 쥐마다 '그 뒤의 이야기' (사연 · 고향 · 잃은 팔다리 · 형제 · 전우 · 앙숙 · 스트레스 · 계급 · 처치 수로 문장을 고름) → 전사자 이름 → 마지막 한마디. 쥐 장면은 검은 바탕 + 왼쪽 초상(`Cutscene/Portrait`)
- 타이틀: `Resources/Art/out/ui/title_bg.png`(키 아트) + `logo.png`(마젠타 빼서 씀). 요청서 `art_requests/title/`

## 그리는 순서 (중요)
- 전장 판때기는 바닥 깊이 검사를 끔(`ZTest Always`, 병사가 땅에 박히던 것 때문) → **앞뒤는 `sortingOrder` 로만**
- `BoardView.DepthOrder(z) = 3000 − round(z·20)`: 맵 줄 깊이 순(웹 painter's order). 카메라 거리 정렬 금지(넓은 트럭이 앞줄 소품을 덮었음)
- 같은 줄 안: 시체 −1 · 소품 0 · 병사 +3/+4(칸 홀짝) · 날아가는 부위 +4 · 효과 +6. 멈춘 부위 −2. 바닥 자국 −500
- 시체: 웹처럼 누르지 않음(세로 45% 누름은 납작해 보여 사용자 요청으로 뺌). 관절 기준으로 땅 위에 맞춤만 (`UnitView.Update`)

## 다리 리그
- 쥐 옆모습 다리는 원본에서 겹친 다리 한 짝을 세로로 갈라 F/N 반쪽으로 잘려 있었음(무릎을 꺾으면 반쪽 군화로 벌어짐) → `tools/merge_legs.py` 로 반쪽을 합쳐 앞 · 뒤 다리 모두 온전한 한 짝, 관절 축은 가운데. 고양이는 원래 온전한 두 다리라 그대로
- 고양이(다리 짧은 리그, `legK < 0.8`)는 `RigPose` 에서 옆모습 웅크림 = 쪼그려 앉기, 쓰러짐 = 다리 거의 편 채 −90° (쥐 각도면 외투 밑으로 군화만 튀어나오거나 하늘로 들렸음)

## 그림 처리
- 파이썬: `C:\Project\underworld-proto\.venv\Scripts\python.exe -I` (Pillow · numpy · scipy)
- `tools/cut_grid.py <시트> <폴더> <열> <행> <이름…>` (마젠타 배경 격자), `tools/cut_strips.py`
- **빨간 그림은 `cut_grid.py` 로 자르면 안 됨**: 마젠타 판정식이 빨강을 반투명으로 만듦 → `min(r,b) − g` 로 판정. 그리고 투명 픽셀 RGB 를 그림 평균색으로 채워야 함(안 그러면 필터링 때 번짐)
- 요청서 · 원본 시트: `art_requests/side_wings` · `props` · `gore`. 유혈 요청은 "피 · 상처"라 쓰면 심의에 걸림 → "붉은 물감 · 소시지 단면"으로 받음

## 테스트 요령 (MCP)
- 플레이 중 유니티 창이 포커스가 없으면 안 돌아감 → PowerShell `WScript.Shell.AppActivate(<Unity PID>)` 후 `Application.runInBackground = true`
- 플레이 진입 직후 도메인 리로드가 한 번 더 일어나면 `GameRoot.I` 같은 static 이 null → 멈췄다 다시 플레이
- 전투 바로: `execute_code` 로 `Camp.Fresh()` 분대 6 → `TitleScreen` 끄고 `GameRoot.I.Deploy(GameData.MISSIONS[i], ids)`
- 시야 안개 끄기 `Shader.SetGlobalFloat("_FowOn", 0)`, 카메라 줌 `CameraRig` 의 private `distT` (리플렉션)
- 스크린샷: `ScreenCapture.CaptureScreenshot("Assets/Screenshots/x.png")` (MCP 스크린샷은 UI 가 빠짐). `Assets/Screenshots/` 는 git 제외
- `execute_code` 는 codedom(C# 6): 로컬 함수 · 패턴 없이

## 지금 상태
- 1막 맵 4장(m1 하수구 20×26 · m2 언덕 24×34 · m3 역 24×34 · mb1 참호 30×44). m21 · m22 는 2막 자리만 잡은 임시 복사본. 2~4막은 `docs/LEVEL_DESIGN.md` 계획만
- 커밋: `a400832` 이식 → `155e231` 흔들림 · 시체 순서 → `f783dba` 출혈 · 부위 파괴 · 고양이 다리 · 시체 높이 → 유혈 그림 시트 → 번지는 웅덩이

### 유혈 (웹판 느낌에 맞춤)
- 그림: `art_requests/gore/sheet.webp` → `out/fx/` 의 `pool0~3 · splat0~3 · spray0(burst) · spray1(jet) · drip0~3 · mist · drop0~5` 사용. `stump_*` · `gib0~3` 은 남아 있지만 안 씀(단면이 빨간 공처럼 보였음 → 사용자: 웹판 부위 파괴가 좋았음)
- 웅덩이: 그림을 키우지 않고 `SpriteFog.shader` `_Spread`(0 → 1.3, MPB) 로 가운데부터 연무 무늬 따라 울퉁불퉁 드러냄. 쓰러지는 동작 60% 에서 시작, 시체 4.5~5.5초 · 떨어진 부위 밑 2.2초, 처음 빠르고 점점 느리게 (`BoardView.AddPool · SetSpread`)
- 바닥 자국은 적게: 자국이 땅을 덮으면 웅덩이가 안 보임. 방울 = 웹과 같은 수, 땅에 닿으면 작은 `drop` 만. 맞을 때 큰 튐 자국 없음
- 파란 이동 칸 위 자국이 보라색으로 보이는 건 반투명 칸 표시가 위에 덮여서 (웹도 같은 순서)

## 남은 일 / 알려진 것
- 기관총 고양이(`cat_mgcat`) 앞모습 몸통에 칠 안 된 회색 부분 (원본 그림 문제)
- 고양이 앞 · 뒤 리그엔 팔 · 정강이가 없음 → 쓰러질 땐 늘 옆모습으로 돌려 절단 (`BoardView.Die`). 고양이 던지기 앞/뒤 그림 없음(옆으로 던짐)
- 저장한 전투를 이어할 때 잘린 부위 단면 · 바닥 자국은 복원 안 됨
- 우클릭 끌기 카메라는 실제 마우스로는 아직 사용자 확인 전
- 2~4막 맵 · 보스전 (계획: `docs/LEVEL_DESIGN.md`)
