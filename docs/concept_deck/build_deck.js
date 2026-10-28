// 쥐들의 전쟁 — 컨셉 기획서 PPT 생성
const pptxgen = require("pptxgenjs");
const path = require("path");
const { applyTheme } = require("C:/Users/user/AppData/Local/Claude-Data/claude_pzs8sxrjxfjjc/local-agent-mode-sessions/skills-plugin/af65a7be-c802-4dd7-a623-f79dd3b45027/8d1da9b5-7196-46d4-a26e-322d0e74f8a1/skills/pptx/scripts/apply_theme.js");

const IMG = "C:/Project/RatWar/docs/concept_deck/jpg/";
const OUT = "C:/Project/RatWar/docs/concept_deck/쥐들의 전쟁_컨셉 기획서.pptx";
const img = (n) => IMG + n + ".jpg";

const HEX = {
  bg: "1E1B14", panel: "2D2920", panel2: "39342A", olive: "6E6D3C", text: "EEE7D4",
  muted: "A9A189", gold: "F2C230", red: "C0442F", blue: "6FA8D8", green: "8CC08F", purple: "B39DDB",
};
const THEME = {
  name: "RatWar",
  headFontFace: "Noto Sans KR",
  bodyFontFace: "Noto Sans KR",
  colors: {
    dk1: HEX.bg, lt1: HEX.text, dk2: HEX.panel, lt2: HEX.muted,
    accent1: HEX.gold, accent2: HEX.red, accent3: HEX.olive, accent4: HEX.blue, accent5: HEX.green, accent6: HEX.purple,
    hlink: HEX.gold, folHlink: HEX.muted,
  },
};

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10 x 5.625
pres.title = "쥐들의 전쟁 — 컨셉 기획서";
pres.author = "정승운";
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
const C = pres.SchemeColor;
// 색 이름 → 테마 색
const K = { bg: C.text1, panel: C.text2, text: C.background1, muted: C.background2, gold: C.accent1, red: C.accent2, olive: C.accent3, blue: C.accent4, green: C.accent5, purple: C.accent6 };

// ── 레이아웃 ──
pres.defineSlideMaster({
  title: "COVER",
  background: { color: K.bg },
  objects: [],
});
pres.defineSlideMaster({
  title: "CONTENT",
  background: { color: K.bg },
  objects: [
    { placeholder: { options: { name: "kicker", type: "body", x: 0.5, y: 0.22, w: 6, h: 0.3, fontSize: 11, bold: true, color: K.gold, margin: 0, valign: "middle" }, text: "" } },
    { placeholder: { options: { name: "title", type: "title", x: 0.5, y: 0.5, w: 9, h: 0.62, fontSize: 24, bold: true, color: K.text, margin: 0, valign: "middle", align: "left" }, text: "" } },
    { text: { text: "쥐들의 전쟁 · 컨셉 기획서 v2.0 · 정승운", options: { x: 0.5, y: 5.28, w: 5, h: 0.25, fontSize: 9, color: K.muted, margin: 0 } } },
  ],
  slideNumber: { x: 9.0, y: 5.28, w: 0.5, h: 0.25, fontSize: 9, color: K.muted, align: "right" },
});

let n = 0;
const sections = new Set();
function content(section, kicker, title) {
  if (!sections.has(section)) { pres.addSection({ title: section }); sections.add(section); }
  const s = pres.addSlide({ masterName: "CONTENT", sectionTitle: section });
  s.addText(kicker, { placeholder: "kicker" });
  s.addText(title, { placeholder: "title" });
  return s;
}
const T = (s, text, o) => s.addText(text, { isTextBox: true, margin: 0, color: K.text, fontSize: 14, valign: "top", ...o });
const box = (s, x, y, w, h, color = K.panel, o = {}) =>
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, fill: { color }, line: { type: "none" }, rectRadius: 0.08, ...o });
const shadow = () => ({ type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.45 });
const pic = (s, name, x, y, w, h, o = {}) => s.addImage({ path: img(name), x, y, w, h, ...o });
// 16:9 이미지를 w 폭으로
const picW = (s, name, x, y, w, o = {}) => pic(s, name, x, y, w, w * 9 / 16, o);
// 번호 원
function badge(s, x, y, label, color = K.gold, d = 0.42) {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color }, line: { type: "none" } });
  T(s, label, { x, y, w: d, h: d, fontSize: 13, bold: true, color: K.bg, align: "center", valign: "middle" });
}
function bullets(s, items, o) {
  s.addText(items.map((t, i) => {
    const runs = typeof t === "string" ? { text: t } : t;
    return { text: runs.text, options: { bullet: { indent: 14 }, breakLine: i < items.length - 1, bold: runs.bold, color: runs.color } };
  }), { isTextBox: true, margin: 0, color: K.text, fontSize: 13, paraSpaceAfter: 6, valign: "top", ...o });
}
function table(s, rows, o) {
  const head = rows[0].map((t) => ({ text: t, options: { bold: true, color: K.bg, fill: { color: K.gold } } }));
  const body = rows.slice(1).map((r, i) => r.map((t) => (typeof t === "object" ? t : { text: String(t), options: { fill: { color: i % 2 ? "36312A" : K.panel } } })));
  s.addTable([head, ...body], {
    fontSize: 11, color: K.text, fontFace: THEME.bodyFontFace, border: { type: "solid", pt: 0.75, color: HEX.bg },
    fill: { color: K.panel }, margin: [3, 6, 3, 6], valign: "middle", ...o,
  });
}

// ── 글자 규격 (모든 슬라이드 공통) ──
const FS = { lead: 12, h: 13, body: 11, small: 10, cap: 9.5 };
// 여러 줄 본문: 줄마다 문단, 간격 일정
function L(s, arr, o) {
  s.addText(arr.map((t, i) => ({ text: t, options: { breakLine: i < arr.length - 1 } })),
    { isTextBox: true, margin: 0, color: K.text, fontSize: FS.body, valign: "top", paraSpaceAfter: 3, ...o });
}
const lead = (s, t) => T(s, t, { x: 0.5, y: 1.25, w: 9, h: 0.32, fontSize: FS.lead, color: K.muted });
const cap = (s, t, x, y, w) => T(s, t, { x, y, w, h: 0.28, fontSize: FS.cap, color: K.muted });
function headBody(s, x, y, w, head, body, col = K.gold) {
  T(s, head, { x, y, w, h: 0.32, fontSize: FS.h, bold: true, color: col });
  L(s, body, { x, y: y + 0.36, w, h: 0.25 * body.length + 0.1, fontSize: FS.small });
}

// ═════════ 1. 표지 ═════════
pres.addSection({ title: "표지" });
{
  const s = pres.addSlide({ masterName: "COVER", sectionTitle: "표지" });
  pic(s, "key_art", 0, 0, 10, 5.625);
  s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 4.9, h: 5.625, fill: { color: K.bg, transparency: 25 }, line: { type: "none" } });
  T(s, "컨셉 기획서", { x: 0.6, y: 1.65, w: 4.0, h: 0.35, fontSize: 14, bold: true, color: K.gold });
  T(s, "쥐들의 전쟁", { x: 0.6, y: 2.0, w: 4.0, h: 0.9, fontSize: 44, bold: true });
  T(s, "턴제 분대 전술 · 로스터 관리", { x: 0.6, y: 3.0, w: 4.0, h: 0.4, fontSize: 15 });
  [["작성자", "정승운"], ["작성일", "2026.10.28"], ["버전", "v2.0"]].forEach(([k, v], i) => {
    T(s, k, { x: 0.6, y: 4.2 + i * 0.3, w: 0.8, h: 0.26, fontSize: FS.small, color: K.muted });
    T(s, v, { x: 1.4, y: 4.2 + i * 0.3, w: 2.5, h: 0.26, fontSize: FS.small, bold: true });
  });
}

// ═════════ 2. 목차 ═════════
{
  const s = content("개요", "CONTENTS", "목차");
  const items = [
    ["01", "기획 의도", "기획 의도 · 레퍼런스"],
    ["02", "원페이지 기획서", "개요 · 세계관 · 핵심 루프"],
    ["03", "비쥬얼 가이드", "원근감 · 시야 · 부위파괴"],
    ["04", "전투 시스템", "명중률 · 치명타 · 부위파괴 · 스트레스"],
    ["05", "최전방 교대 (PTSD)", "전선 피로 · 교대 운용"],
    ["06", "아웃게임 시스템", "성장 · 병종 · 모집 · 특성"],
  ];
  items.forEach(([no, t, d], i) => {
    const x = 0.5 + (i % 2) * 4.6, y = 1.4 + Math.floor(i / 2) * 1.2;
    box(s, x, y, 4.4, 1.0, K.panel, { shadow: shadow() });
    T(s, no, { x: x + 0.3, y, w: 0.8, h: 1.0, fontSize: 26, bold: true, color: K.gold, valign: "middle" });
    T(s, t, { x: x + 1.2, y: y + 0.2, w: 3.0, h: 0.34, fontSize: 15, bold: true });
    T(s, d, { x: x + 1.2, y: y + 0.56, w: 3.0, h: 0.26, fontSize: FS.small, color: K.muted });
  });
}

// ═════════ 3. 기획 의도 ═════════
{
  const s = content("기획 의도", "01  기획 의도", "기획 의도");
  lead(s, "동물 의인화 만화 이미지로 전쟁의 참혹함과 개인의 공포, 소중함을 표현");
  const cards = [
    ["감성", "플랫한 그림체", ["원작의 플랫한 그림체 유지", "3D 레이어로 원근감 보강", "귀여운 쥐와 참혹한 전장의 대비"], K.gold],
    ["주제", "참혹함 · 공포 · 소중함", ["리깅 기반 부위파괴", "스트레스와 정신 붕괴", "사연 있는 병사의 영구 사망"], K.red],
    ["재미", "확률 판정의 긴장감", ["명중률 항목별 공개", "측면 치명타 시네마틱 연출", "한 발로 갈리는 카타르시스"], K.blue],
  ];
  cards.forEach(([tag, head, body, col], i) => {
    const x = 0.5 + i * 3.05;
    box(s, x, 1.8, 2.85, 3.3, K.panel, { shadow: shadow() });
    s.addShape(pres.shapes.OVAL, { x: x + 0.3, y: 2.1, w: 0.7, h: 0.7, fill: { color: col }, line: { type: "none" } });
    T(s, tag, { x: x + 0.3, y: 2.1, w: 0.7, h: 0.7, fontSize: 13, bold: true, color: K.bg, align: "center", valign: "middle" });
    T(s, head, { x: x + 0.3, y: 3.05, w: 2.35, h: 0.34, fontSize: 14, bold: true });
    L(s, body, { x: x + 0.3, y: 3.5, w: 2.4, h: 1.3, fontSize: FS.small, color: K.muted, paraSpaceAfter: 5 });
  });
}

// ═════════ 4. 레퍼런스 ═════════
{
  const s = content("기획 의도", "01  기획 의도", "레퍼런스");
  table(s, [
    ["레퍼런스", "참고 요소", "적용"],
    ["엑스컴", "엄폐 · 명중률 · 측면 · 영구 사망", "전투"],
    ["다키스트 던전", "스트레스 · 정신 붕괴 · 로스터 교대", "관리"],
    ["쥐 (Maus)", "동물 의인화 · 만화 이미지", "주제"],
  ], { x: 0.5, y: 1.3, w: 5.4, colW: [1.35, 3.25, 0.8], rowH: 0.42, fontSize: 10.5 });
  T(s, "차별점", { x: 0.5, y: 3.3, w: 3, h: 0.32, fontSize: FS.h, bold: true, color: K.red });
  bullets(s, [
    "고어도 플랫한 톤으로 표현, 불쾌감보다 비극성 강조",
    "쥐 습성을 스킬로 표현 (땅굴, 철조망 갉기, 수염, 치즈)",
    "병종은 선택이 아닌 스킬 투자 결과로 결정",
  ], { x: 0.5, y: 3.7, w: 5.4, h: 1.2, fontSize: FS.body });
  picW(s, "shot_title", 6.2, 1.3, 3.3);
  cap(s, "타이틀 화면", 6.2, 3.2, 3.3);
  box(s, 6.2, 3.6, 3.3, 1.5, K.panel);
  headBody(s, 6.4, 3.75, 2.95, "포지셔닝", ["엑스컴의 전술", "+ 다키스트 던전의 정신 관리", "2차 세계대전 우화로 재구성"]);
}

// ═════════ 5. 원페이지 ═════════
{
  const s = content("원페이지", "02  원페이지 기획서", "원페이지 기획서");
  table(s, [
    ["항목", "내용"],
    ["장르", "턴제 분대 전술 + 로스터 관리"],
    ["플랫폼 / 엔진", "PC (Windows) / Unity 6 URP"],
    ["타깃", "엑스컴 · 다키스트 던전 유저"],
    ["세계관", "2차 세계대전 모티브, 고양이 총통 vs 쥐 연합"],
    ["캠페인", "4막 × (임무 3 + 막 보스) = 16임무"],
    ["분대", "최대 12마리 보유, 6마리 출전"],
    ["사망", "영구 사망, 복귀 불가"],
  ], { x: 0.5, y: 1.3, w: 5.6, colW: [1.35, 4.25], rowH: 0.42, fontSize: 10.5 });
  box(s, 6.4, 1.3, 3.1, 3.8, K.panel, { shadow: shadow() });
  T(s, "핵심 포인트", { x: 6.65, y: 1.5, w: 2.7, h: 0.32, fontSize: FS.h, bold: true, color: K.gold });
  ["플랫 그림체 + 입체 전장", "리깅 기반 부위파괴", "사연 있는 병사 개체", "쥐 습성 기반 스킬"].forEach((t, i) => {
    badge(s, 6.65, 2.05 + i * 0.72, String(i + 1), K.gold, 0.38);
    T(s, t, { x: 7.18, y: 2.05 + i * 0.72, w: 2.2, h: 0.38, fontSize: FS.body, valign: "middle" });
  });
}

// ═════════ 6. 세계관 · 캠페인 ═════════
{
  const s = content("원페이지", "02  원페이지 기획서", "세계관 / 캠페인 구성");
  lead(s, "막마다 새 병과 · 지형 · 목표를 하나씩만 추가, 단계적으로 학습");
  const acts = [
    ["1막", "점령된 마을", "마을 · 참호", "전멸 · 표적 · 진지", "장교 · 저격 · 화염"],
    ["2막", "강 건너 공장지대", "물 · 다리 · 건물", "파괴 · 구출 · 방어", "방패병"],
    ["3막", "숲과 성당", "숲 · 다층 언덕", "탈출", "군견 고양이"],
    ["4막", "총통의 벙커", "실내 · 문 · 복도", "최종 보스 3단계", "친위대 · 총통"],
  ];
  acts.forEach(([no, name, terr, goal, cat], i) => {
    const x = 0.5 + i * 2.3;
    box(s, x, 1.8, 2.1, 3.3, i === 3 ? "4A2620" : K.panel, { shadow: shadow() });
    T(s, no, { x: x + 0.22, y: 1.98, w: 1.7, h: 0.3, fontSize: 12, bold: true, color: i === 3 ? K.red : K.gold });
    T(s, name, { x: x + 0.22, y: 2.3, w: 1.75, h: 0.36, fontSize: 14, bold: true });
    [["지형", terr], ["목표", goal], ["추가 적", cat]].forEach(([k, v], j) => {
      T(s, k, { x: x + 0.22, y: 2.95 + j * 0.68, w: 1.7, h: 0.24, fontSize: FS.cap, color: K.muted });
      T(s, v, { x: x + 0.22, y: 3.19 + j * 0.68, w: 1.75, h: 0.3, fontSize: FS.body });
    });
    if (i < 3) s.addShape(pres.shapes.RIGHT_TRIANGLE, { x: x + 2.13, y: 3.3, w: 0.14, h: 0.25, fill: { color: K.olive }, line: { type: "none" } });
  });
}

// ═════════ 7. 핵심 루프 ═════════
{
  const s = content("원페이지", "02  원페이지 기획서", "핵심 루프");
  const steps = [
    ["모집", "신병 · 특성", K.green], ["육성", "스킬 -> 병종", K.gold], ["출전", "12마리 중 6", K.blue],
    ["전투", "엄폐 · 확률", K.red], ["정산", "성장 · 사망", K.purple], ["교대", "지친 병사 후방", K.olive],
  ];
  steps.forEach(([t, d, col], i) => {
    const x = 0.5 + i * 1.53;
    box(s, x, 1.35, 1.38, 1.8, K.panel, { shadow: shadow() });
    s.addShape(pres.shapes.OVAL, { x: x + 0.41, y: 1.55, w: 0.56, h: 0.56, fill: { color: col }, line: { type: "none" } });
    T(s, String(i + 1), { x: x + 0.41, y: 1.55, w: 0.56, h: 0.56, fontSize: 15, bold: true, color: K.bg, align: "center", valign: "middle" });
    T(s, t, { x, y: 2.25, w: 1.38, h: 0.34, fontSize: 14, bold: true, align: "center" });
    T(s, d, { x, y: 2.65, w: 1.38, h: 0.28, fontSize: FS.small, color: K.muted, align: "center" });
  });
  s.addShape(pres.shapes.LINE, { x: 1.19, y: 3.4, w: 7.65, h: 0, line: { color: HEX.olive, width: 2, dashType: "dash", beginArrowType: "triangle" } });
  T(s, "교대로 빈 자리는 신병으로 보충 후 재육성", { x: 2.5, y: 3.5, w: 5, h: 0.28, fontSize: FS.small, color: K.muted, align: "center" });
  picW(s, "shot_ops", 0.5, 3.95, 2.0);
  headBody(s, 2.8, 4.05, 6.7, "작전 화면", ["출전 6마리 편성, 대기 병사는 다음 판 이후 스트레스 -35"]);
}

// ═════════ 8. 비쥬얼 — 원근 · 시야 ═════════
{
  const s = content("비쥬얼 가이드", "03  비쥬얼 가이드", "비쥬얼 가이드 - 원근감 / 시야");
  picW(s, "shot_battle", 0.5, 1.3, 5.6, { shadow: shadow() });
  cap(s, "인게임 화면 (1-2 언덕 위의 박격포)", 0.5, 4.5, 5.6);
  const pts = [
    ["3D 레이어 배경", ["배경층 6겹 + 양옆 날개층", "원근 카메라 FOV 40, 줌 시 숙임"]],
    ["리깅 + 통이미지", ["기본 액션은 부위 리깅", "엄폐 · 사망 모션은 통이미지"]],
    ["안개 = 시야", ["미확인 칸 명도 · 채도 하락", "흐르는 연무 2겹으로 표현"]],
  ];
  pts.forEach(([h, d], i) => {
    const y = 1.3 + i * 1.12;
    badge(s, 6.4, y, String(i + 1), K.gold, 0.36);
    headBody(s, 6.9, y + 0.01, 2.6, h, d);
  });
}

// ═════════ 9. 비쥬얼 — 부위파괴 ═════════
{
  const s = content("비쥬얼 가이드", "03  비쥬얼 가이드", "비쥬얼 가이드 - 부위파괴");
  picW(s, "shot_gore", 0.5, 1.3, 5.6, { shadow: shadow() });
  cap(s, "고양이 소총병 절단 사망 직후, 웅덩이 확산 중", 0.5, 4.5, 5.6);
  const pts = [
    ["리깅 부위 분리", ["머리 · 팔 · 다리 분리", "날아간 부위는 바닥에 잔류"]],
    ["플랫한 톤의 출혈", ["방울 -> 바닥 자국", "-> 천천히 번지는 웅덩이"]],
    ["사망 방식별 연출", ["참수 · 절단 · 폭발 3종", "목격한 아군 스트레스 +8"]],
  ];
  pts.forEach(([h, d], i) => {
    const y = 1.3 + i * 1.12;
    badge(s, 6.4, y, String(i + 1), K.red, 0.36);
    headBody(s, 6.9, y + 0.01, 2.6, h, d);
  });
}

// ═════════ 10. 명중률 ═════════
{
  const s = content("전투 시스템", "04  전투 시스템", "명중률");
  lead(s, "확률 기반 공격 판정, 보정 항목은 전부 화면에 공개");
  const parts = [["조준", K.gold, 1.3], ["+ 무기 거리 보정", K.blue, 2.3], ["- 엄폐", K.red, 1.4], ["± 상황 보정", K.green, 2.0]];
  let x = 0.5;
  parts.forEach(([t, col, w]) => {
    box(s, x, 1.75, w, 0.52, col);
    T(s, t, { x, y: 1.75, w, h: 0.52, fontSize: FS.h, bold: true, color: K.bg, align: "center", valign: "middle" });
    x += w + 0.1;
  });
  T(s, "= 명중 % (1~99)", { x: x + 0.05, y: 1.75, w: 1.8, h: 0.52, fontSize: 14, bold: true, color: K.gold, valign: "middle" });
  table(s, [
    ["상황", "보정"],
    ["낮은 엄폐 / 높은 엄폐", "-20 / -40"],
    ["엎드리기 (엄폐 중)", "-25"],
    ["고지", "+20"],
    ["정조준 / 표적 지정 / 칼", "+20 / +15 / +30"],
    ["제압 / 팔 부상", "-30 / -30"],
    ["경계 사격", "-10"],
  ], { x: 0.5, y: 2.5, w: 4.7, colW: [2.9, 1.8], rowH: 0.37, fontSize: 10.5 });
  box(s, 5.5, 2.5, 4.0, 2.6, K.panel);
  headBody(s, 5.75, 2.68, 3.6, "무기 거리 보정", ["산탄총: 2칸 이내 +30, 8칸부터 급감", "저격소총: 3칸 이내 -25, 7칸부터 +5"]);
  headBody(s, 5.75, 3.88, 3.6, "특성 보정", ["지휘관 곁 +5 · 외톨이 +10", "형제 복수심 +15"]);
}

// ═════════ 11. 치명타 · 부위파괴 ═════════
{
  const s = content("전투 시스템", "04  전투 시스템", "치명타 / 부위파괴");
  s.addChart(pres.charts.BAR, [
    { name: "일반 명중", labels: ["머리", "몸통", "팔", "다리"], values: [10, 50, 20, 20] },
    { name: "치명타", labels: ["머리", "몸통", "팔", "다리"], values: [30, 40, 15, 15] },
  ], {
    x: 0.5, y: 1.3, w: 4.7, h: 2.5, barDir: "col", barGrouping: "clustered",
    chartColors: [HEX.olive, HEX.red], showValue: true, dataLabelPosition: "outEnd", dataLabelFontSize: 9.5, dataLabelColor: HEX.text,
    dataLabelFormatCode: "0\"%\"", dataLabelFontFace: "+mn-lt",
    showTitle: true, title: "부위별 피격 확률 (%)", titleColor: HEX.text, titleFontSize: 11, titleFontFace: "+mn-lt",
    showLegend: true, legendPos: "b", legendColor: HEX.text, legendFontSize: 9.5, legendFontFace: "+mn-lt",
    catAxisLabelColor: HEX.text, catAxisLabelFontSize: 10.5, catAxisLabelFontFace: "+mn-lt", valAxisHidden: true,
    valGridLine: { style: "none" }, catGridLine: { style: "none" }, valAxisMaxVal: 60,
  });
  table(s, [
    ["부위", "결과"],
    ["머리", "즉사"],
    ["팔", "명중 -30, 재장전 35% 실패"],
    ["다리", "이동 범위 절반"],
  ], { x: 0.5, y: 3.95, w: 4.7, colW: [1.0, 3.7], rowH: 0.29, fontSize: 10 });
  const facts = [
    ["+40", "측면 치명타 보정", ["엄폐 없는 대상 +15", "치명타 피해 +2 · 시네마틱"]],
    ["70%", "팔다리 절단 확률", ["절단 시 쓰러짐", "의수 · 의족 전 출전 불가"]],
    ["7칸", "확인사살 거리", ["쓰러진 쥐를 고양이가 추격", "구출 여부 판단 필요"]],
  ];
  facts.forEach(([big, h, d], i) => {
    const y = 1.3 + i * 1.3;
    box(s, 5.5, y, 4.0, 1.16, K.panel);
    T(s, big, { x: 5.65, y, w: 1.15, h: 1.16, fontSize: 26, bold: true, color: K.red, valign: "middle" });
    headBody(s, 6.85, y + 0.14, 2.55, h, d, K.text);
  });
}

// ═════════ 12. 스트레스 ═════════
{
  const s = content("전투 시스템", "04  전투 시스템", "스트레스");
  table(s, [
    ["증가", "값", "감소", "값"],
    ["피격 / 치명 피격", "+8 / +12", "처치", "-6"],
    ["쓰러짐", "+20", "승리", "-12"],
    ["7칸 이내 동료 사망", "+22", "치즈 배급", "-25"],
    ["형제 사망", "+45", "찍찍 함성", "-10"],
    ["고어 목격", "+8", "후방 휴식", "-35"],
  ], { x: 0.5, y: 1.3, w: 5.0, colW: [1.7, 0.9, 1.6, 0.8], rowH: 0.36, fontSize: 10.5 });
  T(s, "단계", { x: 0.5, y: 3.6, w: 2, h: 0.3, fontSize: FS.h, bold: true, color: K.gold });
  const st = [["안정", 0, 35, K.green], ["긴장", 35, 60, K.gold], ["불안 (명중 -10)", 60, 85, K.purple], ["붕괴 직전", 85, 100, K.red]];
  st.forEach(([t, a, b, col]) => {
    const x = 0.5 + 5.0 * a / 100, w = 5.0 * (b - a) / 100;
    s.addShape(pres.shapes.RECTANGLE, { x, y: 3.95, w, h: 0.4, fill: { color: col }, line: { color: HEX.bg, width: 1 } });
    T(s, t, { x, y: 3.95, w, h: 0.4, fontSize: FS.cap, bold: true, color: K.bg, align: "center", valign: "middle" });
    T(s, String(a), { x: x - 0.2, y: 4.37, w: 0.4, h: 0.2, fontSize: 8, color: K.muted, align: "center" });
  });
  T(s, "100", { x: 5.3, y: 4.37, w: 0.4, h: 0.2, fontSize: 8, color: K.muted, align: "center" });
  L(s, ["특성별 배율: 용감함 ×0.7 · 겁쟁이 ×1.3 · 비위 약함 고어 ×2", "강철 의지: 붕괴 기준 120"], { x: 0.5, y: 4.62, w: 5.0, h: 0.5, fontSize: FS.cap, color: K.muted, paraSpaceAfter: 1 });
  pic(s, "stress", 5.8, 1.3, 3.7, 2.08);
  box(s, 5.8, 3.55, 3.7, 1.55, K.panel);
  headBody(s, 6.0, 3.68, 3.35, "스트레스 100 도달 시 붕괴", ["공황 50%: 행동 불가", "광란 25%: 무차별 사격", "도주 25%: 전장 이탈", "붕괴 후 70으로 하락, 주변 아군 +6"], K.red);
}

// ═════════ 13. 최전방 교대 ═════════
{
  const s = content("최전방 교대", "05  최전방 교대 (PTSD)", "최전방 교대 (PTSD) - 전선 피로");
  pic(s, "rotation", 0.5, 1.3, 4.3, 2.42);
  headBody(s, 0.5, 3.9, 4.3, "기획 의도", ["현실 전쟁의 최전방 병력 교대를 관리 요소로 채용", "에이스 반복 출전 방지, 분대 전체에 애착 형성"]);
  T(s, "출전 +1  ·  대기 1임무 -2  ·  붕괴 1회 +1", { x: 5.1, y: 1.3, w: 4.4, h: 0.32, fontSize: 12, bold: true, color: K.gold });
  const lv = [
    ["0~1", "정상", "효과 없음", K.green],
    ["2", "지침", "붕괴 기준 -10 (100 -> 90)", K.gold],
    ["3", "탄환 충격", "붕괴 기준 -20, 출전 시 스트레스 +10", "D9824A"],
    ["4~5", "전쟁 신경증 (PTSD)", "붕괴 기준 -30, 매 턴 공황 5%, 출전 경고", K.red],
  ];
  lv.forEach(([f, name, eff, col], i) => {
    const y = 1.72 + i * 0.7;
    box(s, 5.1, y, 4.4, 0.62, K.panel);
    s.addShape(pres.shapes.OVAL, { x: 5.22, y: y + 0.09, w: 0.44, h: 0.44, fill: { color: col }, line: { type: "none" } });
    T(s, f, { x: 5.22, y: y + 0.09, w: 0.44, h: 0.44, fontSize: 9.5, bold: true, color: K.bg, align: "center", valign: "middle" });
    T(s, name, { x: 5.82, y: y + 0.05, w: 3.55, h: 0.28, fontSize: 12, bold: true });
    T(s, eff, { x: 5.82, y: y + 0.33, w: 3.6, h: 0.25, fontSize: FS.cap, color: K.muted });
  });
  box(s, 5.1, 4.6, 4.4, 0.5, "35302A");
  L(s, ["회복: 대기 1임무당 스트레스 -35, 피로 -2", "PTSD 해제: 2임무 연속 대기"], { x: 5.25, y: 4.64, w: 4.15, h: 0.44, fontSize: FS.cap, paraSpaceAfter: 0 });
}

// ═════════ 14. 교대 운용 ═════════
{
  const s = content("최전방 교대", "05  최전방 교대 (PTSD)", "교대 운용 예시");
  const roster = [[0, "출전"], [1, "출전"], [2, "출전"], [1, "출전"], [3, "출전"], [4, "출전"], [0, "대기"], [0, "대기"], [1, "대기"], [2, "대기"], [5, "대기"], [0, "신병"]];
  const fc = [HEX.green, HEX.green, HEX.gold, "D9824A", HEX.red, HEX.red];
  T(s, "임무 N 분대 현황 (숫자 = 전선 피로)", { x: 0.5, y: 1.3, w: 5, h: 0.32, fontSize: FS.h, bold: true, color: K.gold });
  roster.forEach(([f, st], i) => {
    const x = 0.5 + (i % 6) * 0.82, y = 1.75 + Math.floor(i / 6) * 1.2;
    box(s, x, y, 0.72, 0.95, K.panel);
    s.addShape(pres.shapes.OVAL, { x: x + 0.14, y: y + 0.1, w: 0.44, h: 0.44, fill: { color: fc[f] }, line: { type: "none" } });
    T(s, String(f), { x: x + 0.14, y: y + 0.1, w: 0.44, h: 0.44, fontSize: 12, bold: true, color: K.bg, align: "center", valign: "middle" });
    T(s, st, { x, y: y + 0.62, w: 0.72, h: 0.26, fontSize: FS.cap, color: K.muted, align: "center" });
  });
  cap(s, "윗줄 출전 6  ·  아랫줄 후방 대기 6", 0.5, 4.25, 5);
  const qs = [
    ["교대 대상 선택", ["피로 3~4 병사 제외 시 전력 공백", "빈 자리는 피로 0 신병으로 보충"]],
    ["강행 출전", ["PTSD 에이스의 막 보스 투입 여부 결정", "출전 시 경고창 표시"]],
    ["신병 모집", ["사망 보충 + 교대 인원 확보", "1마리당 보급품 35"]],
  ];
  qs.forEach(([h, d], i) => {
    const y = 1.3 + i * 1.28;
    box(s, 5.6, y, 3.9, 1.14, K.panel, { shadow: shadow() });
    headBody(s, 5.82, y + 0.14, 3.5, h, d);
  });
}

// ═════════ 15. 병사 성장 ═════════
{
  const s = content("아웃게임", "06  아웃게임 시스템", "병사 성장 (계급 / 스킬)");
  picW(s, "shot_squad", 4.3, 1.3, 5.2, { shadow: shadow() });
  cap(s, "분대 화면 (병사 정보 · 특성 · 스킬판)", 4.3, 4.3, 5.2);
  headBody(s, 0.5, 1.3, 3.5, "계급 8단계", ["이병 > 일병 > 상병 > 병장", "> 하사 > 중사 > 상사 > 원사", "경험치: 처치 · 출전 · 승리", "진급 보상: 명중 · 체력 + 스킬 1점"]);
  headBody(s, 0.5, 2.95, 3.5, "육각 스킬판", ["신병 칸에서 이웃 칸으로 확장", "갈래: 사격 · 화력 · 폭파 · 기동 · 지원 · 정찰", "최다 투자 갈래 (2칸 이상) = 병종", "두 갈래 균형 시 혼합 병종"]);
}

// ═════════ 16. 병종 ═════════
{
  const s = content("아웃게임", "06  아웃게임 시스템", "병종");
  table(s, [
    ["병종", "갈래", "무기", "쥐 컨셉 스킬"],
    ["소총수 (신병)", "-", "소총", "잽싼 발: 경계 사격 회피"],
    ["저격수", "사격", "저격소총", "쥐구멍 매복: 사격 전까지 은신"],
    ["기관총 사수", "화력", "경기관총", "찍찍 함성: 명중 +10, 스트레스 -10"],
    ["공병", "폭파", "소총", "철조망 갉기 · 땅굴 파기 · 폭약"],
    ["코만도", "기동", "기관단총", "벽 타기 · 소리 없는 칼"],
    ["의무병", "지원", "기관단총", "치즈 배급: 스트레스 -25, 공황 해제"],
    ["정찰병", "정찰", "소총", "수염 감지: 벽 너머 탐지 · 표적 지정"],
    ["통신병", "사격 + 정찰", "기관단총", "쥐떼 전령 · 무전 탐지"],
    ["주술사", "지원 + 정찰", "기관단총", "정신 지배 · 마법 방어막 (영매 필요)"],
  ], { x: 0.5, y: 1.3, w: 9, colW: [1.7, 1.3, 1.3, 4.7], rowH: 0.37, fontSize: 10.5 });
}

// ═════════ 17. 모집 · 특성 ═════════
{
  const s = content("아웃게임", "06  아웃게임 시스템", "신병 모집 / 특성");
  picW(s, "shot_recruit", 0.5, 1.3, 4.6, { shadow: shadow() });
  T(s, "\"딸에게 매일 편지를 쓴다. 아직 한 통도 못 부쳤다.\"", { x: 0.5, y: 4.0, w: 4.6, h: 0.3, fontSize: FS.small, italic: true, color: K.gold });
  L(s, ["후보 3마리 · 1마리당 보급품 35 · 새로고침 8", "이름 · 고향 · 사연 + 특성 2~3개 (충돌 조합 제외)"], { x: 0.5, y: 4.4, w: 4.6, h: 0.6, fontSize: FS.cap, color: K.muted, paraSpaceAfter: 2 });
  const cats = [
    ["스트레스 관리용", ["용감함 · 겁쟁이 · 낙천가 · 비관론자", "신앙심 · 술꾼 · 골초 · 향수병"], K.purple],
    ["개체 특징용", ["피에 굶주림 · 사이코패스 · 외톨이", "타고난 지휘관 · 치즈 귀신 · 덤벙댐"], K.blue],
    ["관계", ["형제: 동반 출전 시 명중 +8", "한쪽 사망 시 스트레스 +45, 명중 +15"], K.red],
    ["희귀", ["영매 (15%)", "주술사 전직 조건"], K.gold],
  ];
  cats.forEach(([h, d, col], i) => {
    const y = 1.3 + i * 0.96;
    box(s, 5.4, y, 4.1, 0.86, K.panel);
    s.addShape(pres.shapes.OVAL, { x: 5.56, y: y + 0.14, w: 0.18, h: 0.18, fill: { color: col }, line: { type: "none" } });
    T(s, h, { x: 5.86, y: y + 0.06, w: 3.5, h: 0.3, fontSize: 11.5, bold: true, color: col });
    L(s, d, { x: 5.86, y: y + 0.37, w: 3.55, h: 0.46, fontSize: FS.cap, paraSpaceAfter: 1 });
  });
}

// ═════════ 18. 시스템 연결 ═════════
{
  const s = content("시스템 연결", "06  아웃게임 시스템", "시스템 연결 구조");
  pic(s, "memorial", 5.6, 1.3, 3.9, 2.19, { shadow: shadow() });
  const flow = [
    ["사연 / 특성", "개체별 개성", K.blue], ["스킬 / 병종 육성", "직접 키운 병사", K.gold], ["애착", "", K.green],
    ["부위파괴 / 스트레스 / 영구 사망", "상실", K.red], ["최전방 교대 / 신병 모집", "빈 자리 보충", K.purple],
  ];
  flow.forEach(([h, d, col], i) => {
    const y = 1.3 + i * 0.72;
    box(s, 0.5, y, 4.8, 0.6, K.panel);
    s.addShape(pres.shapes.OVAL, { x: 0.66, y: y + 0.17, w: 0.26, h: 0.26, fill: { color: col }, line: { type: "none" } });
    T(s, h, { x: 1.08, y, w: 2.9, h: 0.6, fontSize: FS.body, bold: true, valign: "middle" });
    if (d) T(s, d, { x: 3.9, y, w: 1.25, h: 0.6, fontSize: FS.cap, color: K.muted, valign: "middle", align: "right" });
  });
  T(s, "-> 새 병사로 1번부터 반복", { x: 0.5, y: 4.95, w: 4.8, h: 0.25, fontSize: FS.cap, color: K.gold });
  bullets(s, [
    "성장으로 애착 형성, 상실 시 소중함 부각",
    "전사자는 추모비에 사망 방식과 함께 기록",
    "형제 사망 시 남은 쪽 능력치 · 스트레스 변화",
  ], { x: 5.6, y: 3.7, w: 3.9, h: 1.4, fontSize: FS.small });
}

// ═════════ 19. 정리 ═════════
pres.addSection({ title: "정리" });
{
  const s = pres.addSlide({ masterName: "COVER", sectionTitle: "정리" });
  pic(s, "memorial", 0, 0, 10, 5.625);
  s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 10, h: 5.625, fill: { color: K.bg, transparency: 22 }, line: { type: "none" } });
  T(s, "목표", { x: 0.8, y: 1.1, w: 8.4, h: 0.35, fontSize: 13, bold: true, color: K.gold, align: "center" });
  T(s, "전투가 끝난 뒤\n돌아오지 못한 병사를 확인하는 순간", { x: 0.8, y: 1.5, w: 8.4, h: 1.35, fontSize: 26, bold: true, align: "center", valign: "middle" });
  [["비쥬얼", "플랫 그림체 + 입체 전장"], ["전투", "확률 판정 + 부위파괴"], ["관리", "스트레스 + 최전방 교대"]].forEach(([h, d], i) => {
    const x = 0.8 + i * 2.87;
    box(s, x, 3.35, 2.65, 1.05, K.panel, { fill: { color: K.panel, transparency: 15 } });
    T(s, h, { x, y: 3.5, w: 2.65, h: 0.34, fontSize: 14, bold: true, color: K.gold, align: "center" });
    T(s, d, { x, y: 3.9, w: 2.65, h: 0.3, fontSize: FS.body, align: "center" });
  });
  T(s, "쥐들의 전쟁 · 컨셉 기획서", { x: 0.8, y: 4.95, w: 8.4, h: 0.28, fontSize: FS.cap, color: K.muted, align: "center" });
}

(async () => {
  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);
  console.log("wrote", OUT);
})();
