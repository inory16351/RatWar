// 맵 · 임무 만들기 + 검사 → RatWarUnity/Assets/Resources/Data/maps.json · missions.json
// 사용: node tools/maps/build_maps.js
// 맵은 "도장 찍기"로 짠 뒤 기호 줄로 뽑음. 기호는 GameData.PROPS · World.Create 와 같음
//   . 풀  , 진흙  = 길  o 구덩이 | S 모래주머니 C 상자 (낮은 엄폐) | W 벽 T 나무 R 트럭 H 돌집 (높은 엄폐)
//   X 철조망 (못 지나감) | M 박격포 진지  G 기관총 진지
// z = 0 이 맨 뒤(고양이 쪽), 쥐는 아래(z 큰 쪽)에서 출발. 자세한 설계 기준은 docs/LEVEL_DESIGN.md
const fs = require('fs');
const path = require('path');

// 소품 시트 A (2026-10-27): 낮은 엄폐 D 드럼통 B 건초 묶음 U 잔해 K 수레 F 돌담 L 통나무 Q 치즈 상자 J 모래주머니 모서리
//                           높은 엄폐 Y 벽 모서리 V 불탄 차 P 헛간 A 건초 더미 E 우물 Z 그루터기 N 성당 벽 I 광고 기둥
const BLOCK = new Set([...'SCWTRHX', ...'DBUKFLQJ', ...'YVPAEZNI']);
const COVER = { S: 1, C: 1, W: 2, T: 2, R: 2, H: 2, G: 1 };
for (const c of 'DBUKFLQJ') COVER[c] = 1;
for (const c of 'YVPAEZNI') COVER[c] = 2;

// ── 도장 ──
class Map {
  constructor(id, W, D, fill = '.') { this.id = id; this.W = W; this.D = D; this.g = Array.from({ length: D }, () => Array(W).fill(fill)); this.hills = []; this.spawn = []; }
  put(x, z, c) { if (x >= 0 && z >= 0 && x < this.W && z < this.D) this.g[z][x] = c; return this; }
  at(x, z) { return x >= 0 && z >= 0 && x < this.W && z < this.D ? this.g[z][x] : null; }
  h(x0, x1, z, c) { for (let x = x0; x <= x1; x++) this.put(x, z, c); return this; }   // 가로 줄
  v(x, z0, z1, c) { for (let z = z0; z <= z1; z++) this.put(x, z, c); return this; }   // 세로 줄
  pts(list, c) { for (const [x, z] of list) this.put(x, z, c); return this; }          // 낱칸들
  // 빈 땅(. , =)에만 놓기 — 다른 소품 · 출발 칸은 건드리지 않음 (꾸밈용)
  add(list, c) { for (const [x, z] of list) if ('.,'.includes(this.at(x, z)) && !this.spawn.some(([a, b]) => a === x && b === z)) this.put(x, z, c); return this; }
  // 건물 외곽(돌집 벽) — door = [[x,z],...] 는 비워 둠
  house(x0, z0, x1, z1, door = [], c = 'H') {
    this.h(x0, x1, z0, c).h(x0, x1, z1, c).v(x0, z0, z1, c).v(x1, z0, z1, c);
    for (const [x, z] of door) this.put(x, z, '.');
    return this;
  }
  // 참호선: 모래주머니 가로 줄 + 틈(gaps: x 목록)
  trench(z, x0, x1, gaps = []) { this.h(x0, x1, z, 'S'); for (const x of gaps) this.put(x, z, '.'); return this; }
  // 기존 맵 붙이기 (rows 를 (ox, oz) 에)
  paste(rows, ox, oz) { rows.forEach((r, z) => [...r].forEach((c, x) => this.put(ox + x, oz + z, c))); return this; }
  hill(cx, cz, rx, rz) { this.hills.push({ cx, cz, rx, rz }); return this; }
  rows() { return this.g.map((r) => r.join('')); }
}

// ── 기존 18 × 22 맵 (data.js 원본) ──
const HILL18 = [
  'T.HH....T....HH..T', '..H..C.....,...H..', '.......WW.....C...', '..==============..', '.S..C.......S...T.', '....,..o.......,..',
  'XXX..SSSS..SSSS.XX', '..........o.......', 'T...HH......RRR..T', '....H.....C.......', '.WW......S...SGS..', '......C.....,.....',
  '.T.......XXX...T..', '....S.......,..C..', '..C...S..M..S.....', '.......,..........', '.T....S...,.S.....', '....C......XX..W..',
  '..S.....T.....WW..', '........=====.....', '=======.....======', '..T......C........',
];
const STATION18 = [
  'HHH..T.....HHHH..T', 'H.C......C....H...', 'H.....S.G...S.....', '..T.......,....C..', '==================', '..o....RRR....o...',
  '.S...C......S..C..', '....WW....XX....WW', '.T..W..........W..', '......S..o..S.....', '..C.......,....T..', '.....HH....HH.....',
  '.S...H......H..S..', '..,.....o.....,...', '....S.....C.......', 'T.......RRR.....T.', '..XX..........XX..', '.....C....S.......',
  '..T........,....C.', '.....=====.....T..', '=====.....=====...', '..C.....T.........',
];

const maps = {};

// 1-1 하수구 출구 (S 20 × 26)
{
  const m = new Map('sewer', 20, 26);
  // 위: 마을 외곽 집 두 채 + 마당 상자
  m.house(1, 0, 5, 2, [[3, 2]]).house(14, 0, 18, 2, [[16, 2]]);
  m.pts([[3, 1], [16, 1]], 'C').pts([[9, 0], [10, 1]], 'T').pts([[7, 3], [8, 3], [12, 3]], 'W');
  m.h(0, 19, 5, '=').v(9, 6, 14, '=').v(10, 6, 14, '=');                 // 가로 길 + 가운데 세로 길
  m.pts([[2, 4], [17, 4]], 'C').pts([[6, 6], [14, 6]], 'S');
  // 모래주머니 줄 (B 무리) — 가운데는 길로 열림
  m.trench(9, 2, 7, [4]).trench(9, 12, 17, [15]);
  m.pts([[0, 11], [1, 11], [2, 11], [17, 11], [18, 11], [19, 11]], 'X'); // 양끝 철조망 → 가운데로 모음
  m.pts([[5, 7], [14, 8]], 'o').pts([[4, 10], [15, 10]], ',');
  // 가운데 밭: 생울타리(모래주머니) · 나무 · 상자
  m.pts([[1, 7], [18, 7], [6, 13], [14, 14], [2, 17], [17, 18], [8, 20]], 'T');
  m.pts([[4, 13], [5, 13], [13, 16], [12, 16], [7, 17], [8, 17]], 'S');
  m.pts([[11, 12], [3, 15], [16, 15], [11, 19], [5, 20]], 'C');
  m.pts([[9, 16], [10, 22], [15, 21]], 'o').pts([[6, 19], [13, 20], [4, 22]], ',');
  // 아래: 하수구 둑 (출발) — 둑 벽 사이로 나옴
  m.h(0, 6, 25, 'W').h(13, 19, 25, 'W').pts([[3, 23], [16, 23]], 'C').pts([[6, 23], [13, 23]], 'S');
  m.spawn = [[8, 24], [11, 24], [9, 25], [10, 25], [7, 24], [12, 24]];
  // 소품 시트 A 섞기
  m.add([[11, 2]], 'E').add([[13, 4]], 'I').add([[8, 8], [16, 8]], 'D').add([[2, 14], [15, 19]], 'B').add([[12, 20]], 'L').add([[17, 13]], 'K')
    .add([[4, 18], [5, 18]], 'F').add([[14, 23]], 'Q').add([[6, 11]], 'Z').add([[7, 4]], 'U');
  maps.sewer = m;
}

// 1-2 언덕 위의 박격포 (M 24 × 34) = 기존 언덕 맵 + 앞 농장 접근로 12줄 + 양옆 3칸
{
  const m = new Map('hill', 24, 34);
  m.paste(HILL18, 3, 0);
  m.hill(9 + 3, 14.6, 4.3, 2.4);
  // 양옆 띠: 나무 · 상자 드문드문
  m.pts([[0, 2], [1, 9], [0, 15], [22, 4], [23, 12], [22, 18], [1, 20]], 'T').pts([[1, 5], [22, 8], [0, 12], [23, 16]], 'C');
  m.pts([[0, 6], [1, 6], [2, 6], [21, 6], [22, 6], [23, 6]], 'X');     // 참호선 철조망을 양끝까지
  m.h(0, 2, 20, '=').h(21, 23, 20, '=');                                 // 길 이어짐
  // 앞 농장 (z 22 ~ 33)
  m.house(2, 24, 7, 28, [[7, 26], [4, 24]]).pts([[4, 26]], 'C');         // 농가
  m.pts([[10, 23], [11, 23], [12, 23], [14, 25], [15, 25]], 'S');          // 생울타리
  m.pts([[18, 23], [20, 25], [9, 27], [16, 29], [21, 30], [1, 31]], 'T');
  m.pts([[12, 27], [13, 27], [18, 27], [10, 30], [3, 30]], 'C');
  m.pts([[15, 31], [16, 31], [7, 31], [8, 31]], 'S');
  m.pts([[11, 25], [19, 28], [6, 29]], 'o').pts([[13, 29], [2, 22], [20, 32]], ',');
  m.v(11, 24, 33, '=').v(12, 31, 33, '=');                               // 농장 길
  m.spawn = [[10, 32], [13, 32], [11, 33], [12, 33], [9, 33], [14, 33]];
  // 소품 시트 A 섞기
  m.add([[20, 27]], 'P').add([[16, 23], [5, 32]], 'A').add([[8, 22], [9, 22]], 'B').add([[9, 25]], 'E').add([[14, 28]], 'K').add([[17, 31], [18, 31]], 'F')
    .add([[22, 24]], 'L').add([[20, 10]], 'D').add([[9, 1]], 'N').add([[13, 2]], 'Y').add([[2, 17]], 'U').add([[6, 10]], 'J').add([[21, 15]], 'V').add([[3, 3]], 'I').add([[1, 26]], 'Z');
  maps.hill = m;
}

// 1-3 철도역의 장교 (M 24 × 34) = 기존 철도역 + 앞 화물 적치장 12줄 + 양옆 3칸
{
  const m = new Map('station', 24, 34);
  m.paste(STATION18, 3, 0);
  m.hill(9 + 3, 9.2, 2.6, 1.5);
  m.h(0, 2, 4, '=').h(21, 23, 4, '=');                                   // 철길 이어짐
  m.house(0, 0, 2, 3, [[2, 2]]).house(21, 0, 23, 3, [[21, 2]]);          // 양옆 역사 건물
  m.pts([[1, 8], [22, 9], [0, 14], [23, 15], [1, 19], [22, 20]], 'T').pts([[1, 11], [22, 12], [0, 17]], 'C');
  // 앞 화물 적치장 (z 22 ~ 33): 트럭 · 상자 더미 · 화차 사이 골목
  m.h(3, 5, 23, 'R').h(17, 19, 24, 'R').h(9, 11, 26, 'R');
  m.pts([[13, 23], [14, 23], [7, 25], [7, 26], [15, 27], [16, 27], [2, 28], [20, 28]], 'C');
  m.pts([[5, 29], [6, 29], [18, 30], [19, 30], [11, 30], [12, 30]], 'S');
  m.pts([[0, 25], [23, 26], [9, 31], [14, 32]], 'T');
  m.pts([[12, 24], [4, 31], [17, 32]], 'o').pts([[8, 28], [21, 31]], ',');
  m.h(0, 23, 33, '.');
  m.spawn = [[10, 32], [13, 32], [11, 33], [12, 33], [9, 33], [14, 33]];
  // 소품 시트 A 섞기
  m.add([[6, 23], [21, 29]], 'D').add([[9, 28]], 'Q').add([[3, 9]], 'V').add([[7, 3], [16, 3]], 'I').add([[20, 11]], 'Y').add([[10, 13]], 'E').add([[1, 22], [16, 13]], 'U')
    .add([[20, 22]], 'K').add([[14, 29]], 'J').add([[2, 31]], 'L').add([[21, 17]], 'P').add([[10, 0]], 'N');
  maps.station = m;
}

// 1-B 참호선 돌파 (L 30 × 44)
{
  const m = new Map('trench', 30, 44);
  // 맨 뒤 마을 · 지휘 벙커 (z 0 ~ 7)
  m.house(1, 0, 6, 3, [[6, 2]]).house(23, 0, 28, 3, [[23, 2]]);
  m.house(10, 2, 19, 7, [[14, 7], [15, 7]]);                             // 벙커: 문은 앞쪽 두 칸
  m.pts([[12, 4], [17, 4]], 'C').pts([[8, 1], [21, 1], [0, 6], [29, 6]], 'T');
  // 언덕 박격포 (z 9 ~ 13)
  m.hill(22, 11, 4.5, 2.6).put(22, 11, 'M').pts([[20, 10], [24, 10]], 'S');
  m.hill(6, 10, 3.5, 2.2).pts([[6, 9]], 'S');
  // 2차 참호 (z 15) + 앞 철조망 토막 (z 17)
  m.trench(15, 0, 29, [4, 13, 14, 24]).put(9, 14, 'G').pts([[8, 14], [10, 14]], 'S');
  m.pts([[2, 17], [3, 17], [7, 17], [8, 17], [17, 17], [18, 17], [19, 17], [26, 17], [27, 17]], 'X');
  // 폐농가 (z 19 ~ 24)
  m.house(3, 19, 8, 23, [[5, 23], [8, 21]]).house(20, 20, 25, 24, [[20, 22], [23, 20]]);
  m.pts([[13, 20], [14, 21], [16, 22], [11, 23]], 'C').pts([[12, 19], [17, 24], [1, 22], [28, 21]], 'T');
  // 1차 참호 (z 27) + 기관총 진지 2
  m.trench(27, 0, 29, [5, 6, 15, 24, 25]).put(10, 26, 'G').pts([[9, 26], [11, 26]], 'S').put(20, 26, 'G').pts([[19, 26], [21, 26]], 'S');
  // 무인지대 (z 29 ~ 38): 구덩이 · 철조망 토막 · 트럭 · 나무 그루터기
  m.pts([[3, 30], [4, 30], [12, 31], [13, 31], [22, 30], [23, 30], [7, 35], [8, 35], [18, 34], [19, 34], [27, 33], [28, 33]], 'X');
  m.pts([[6, 32], [16, 30], [25, 35], [10, 37], [2, 36], [20, 37], [14, 33], [27, 30]], 'o');
  m.h(13, 15, 35, 'R').pts([[1, 33], [9, 31], [24, 32], [16, 37], [5, 38]], 'T');
  m.pts([[11, 33], [21, 32], [4, 34], [26, 37], [15, 39]], 'C').pts([[9, 29], [17, 33], [23, 36]], ',');
  // 아군 참호 (z 40) + 출발 (z 41 ~ 43)
  m.trench(40, 0, 29, [6, 7, 14, 15, 22, 23]).pts([[3, 42], [26, 42]], 'C');
  m.spawn = [[13, 42], [16, 42], [14, 43], [15, 43], [12, 43], [17, 43]];
  // 소품 시트 A 섞기
  m.add([[5, 26], [24, 26], [13, 14]], 'J').add([[8, 32], [19, 30], [25, 33]], 'U').add([[12, 9], [16, 9], [8, 5]], 'D').add([[4, 36]], 'V').add([[22, 34], [11, 38]], 'Z')
    .add([[26, 13]], 'N').add([[2, 12]], 'Y').add([[9, 42], [20, 42]], 'Q').add([[14, 19]], 'K').add([[26, 21]], 'L').add([[11, 21]], 'A').add([[16, 19]], 'E');
  maps.trench = m;
}

// ── 임무 ──
const C = (cls, head, x, z, pod, target) => ({ cls, head, x, z, pod, ...(target ? { target: true } : {}) });
const missions = [
  {
    id: 'm1', act: 1, name: '1-1 · 하수구 출구', map: 'sewer', obj: 'elim', reward: 50, shells: 0,
    brief: '하수구를 빠져나가 마을 외곽의 고양이 초소를 쓸어버려라. 첫 출격이다 — 엄폐물에서 엄폐물로.',
    cats: [
      C('trooper', 'mackerel', 6, 14, 'A'), C('trooper', 'tuxedo', 13, 13, 'A'),
      C('mgcat', 'boss_turret', 4, 8, 'B'), C('trooper', 'chonk', 14, 8, 'B'),
      C('officer', 'boss_dictator', 10, 3, 'C'), C('trooper', 'loaf', 5, 4, 'C'), C('trooper', 'boss_ryan', 15, 4, 'C'),
    ],
  },
  {
    id: 'm2', act: 1, name: '1-2 · 언덕 위의 박격포', map: 'hill', obj: 'elim', reward: 60, shells: 3,
    brief: '농장을 지나 언덕 위 버려진 박격포 진지를 차지하고, 참호선까지 고양이 분대를 몰아내라.',
    cats: [
      C('trooper', 'loaf', 8, 21, 'F'), C('trooper', 'chonk', 17, 22, 'F'),
      C('trooper', 'mackerel', 5, 11, 'A'), C('trooper', 'tuxedo', 7, 10, 'A'),
      C('mgcat', 'boss_turret', 17, 10, 'B'), C('trooper', 'chonk', 19, 9, 'B'),
      C('officer', 'boss_dictator', 14, 9, 'C'), C('trooper', 'boss_ryan', 16, 9, 'C'),
      C('trooper', 'loaf', 11, 11, 'D'), C('mgcat', 'mackerel', 15, 12, 'D'),
      C('trooper', 'tuxedo', 9, 5, 'E'), C('trooper', 'chonk', 15, 5, 'E'),
    ],
  },
  {
    id: 'm3', act: 1, name: '1-3 · 철도역의 장교', map: 'station', obj: 'officer', reward: 80, shells: 0,
    brief: '화물 적치장을 뚫고 철도역 뒤 마을에 숨은 고양이 장교를 사살하라. 장교만 쓰러뜨리면 나머지는 흩어진다.',
    cats: [
      C('trooper', 'mackerel', 8, 21, 'G'), C('trooper', 'tuxedo', 16, 22, 'G'),
      C('trooper', 'mackerel', 9, 13, 'A'), C('trooper', 'loaf', 16, 12, 'A'),
      C('mgcat', 'boss_turret', 11, 9, 'B'), C('catflame', 'tuxedo', 14, 8, 'B'),
      C('trooper', 'chonk', 6, 6, 'C'), C('catsniper', 'mackerel', 16, 5, 'C'),
      C('officer', 'boss_dictator', 11, 1, 'D', true), C('trooper', 'boss_ryan', 13, 2, 'D'), C('mgcat', 'boss_turret', 11, 2, 'D'),
    ],
  },
  {
    id: 'mb1', act: 1, name: '1막 보스 · 참호선 돌파', map: 'trench', obj: 'officer', reward: 120, shells: 2,
    brief: '무인지대를 건너 참호 세 줄을 뚫고, 지휘 벙커의 독재 고양이 장교를 쓰러뜨려라. 기관총 진지를 뺏으면 2차 참호를 쓸어버릴 수 있다.',
    cats: [
      C('trooper', 'mackerel', 3, 28, 'A'), C('mgcat', 'boss_turret', 10, 25, 'A'), C('trooper', 'tuxedo', 13, 28, 'A'),
      C('mgcat', 'boss_turret', 20, 25, 'B'), C('trooper', 'chonk', 26, 28, 'B'),
      C('trooper', 'loaf', 5, 21, 'C'), C('catflame', 'boss_ryan', 15, 21, 'C'),
      C('catsniper', 'mackerel', 22, 22, 'D'), C('trooper', 'tuxedo', 27, 23, 'D'),
      C('mgcat', 'boss_turret', 9, 13, 'E'), C('trooper', 'chonk', 18, 14, 'E'),
      C('trooper', 'loaf', 21, 11, 'F'), C('catsniper', 'mackerel', 6, 10, 'F'),
      C('officer', 'boss_dictator', 15, 4, 'G', true), C('trooper', 'boss_ryan', 13, 5, 'G'), C('catflame', 'tuxedo', 16, 6, 'G'),
    ],
  },
  // 2막 자리 (임시: 1막 맵 좌우 뒤집기) — 2막 맵이 나오면 바꿈
  {
    id: 'm21', act: 2, name: '2-1 · 참호 돌파 (임시)', map: 'hill', mirror: true, obj: 'reach', reward: 90, shells: 2, goal: { x: 11, z: 1, r: 1 },
    brief: '참호선을 뚫고 폐허 마을의 깃발까지 쥐 한 마리라도 닿게 하라. 다 잡을 필요는 없다.',
    cats: [
      C('trooper', 'loaf', 8, 21, 'F2'), C('trooper', 'chonk', 17, 22, 'F2'),
      C('trooper', 'mackerel', 5, 11, 'A'), C('mgcat', 'boss_turret', 17, 10, 'B'), C('trooper', 'chonk', 19, 9, 'B'),
      C('trooper', 'loaf', 11, 11, 'D'), C('trooper', 'tuxedo', 9, 5, 'E'), C('mgcat', 'mackerel', 15, 5, 'E'), C('catflame', 'boss_ryan', 12, 5, 'E'),
      C('catsniper', 'loaf', 17, 1, 'F'), C('officer', 'boss_dictator', 12, 1, 'F'), C('trooper', 'tuxedo', 8, 2, 'F'),
    ],
  },
  {
    id: 'm22', act: 2, name: '2-2 · 총통의 근위대 (임시)', map: 'station', mirror: true, obj: 'elim', reward: 120, shells: 0,
    brief: '총통 직속 근위대가 역을 틀어쥐었다. 남김없이 쓸어버려라.',
    cats: [
      C('trooper', 'mackerel', 8, 21, 'G2'), C('trooper', 'tuxedo', 16, 22, 'G2'),
      C('trooper', 'mackerel', 9, 13, 'A'), C('trooper', 'loaf', 16, 12, 'A'),
      C('mgcat', 'boss_turret', 11, 9, 'B'), C('trooper', 'tuxedo', 14, 8, 'B'), C('officer', 'boss_dictator', 13, 9, 'B'),
      C('trooper', 'chonk', 6, 6, 'C'), C('mgcat', 'mackerel', 16, 5, 'C'), C('catflame', 'boss_ryan', 12, 3, 'C'),
      C('catsniper', 'loaf', 6, 1, 'D'), C('officer', 'boss_dictator', 11, 1, 'D'), C('trooper', 'boss_ryan', 13, 2, 'D'),
      C('mgcat', 'boss_turret', 11, 2, 'D'), C('trooper', 'chonk', 7, 1, 'D'),
    ],
  },
];

// ── 검사 ──
let bad = 0;
const err = (s) => { console.log('  ✖ ' + s); bad++; };
const mirrorRows = (rows) => rows.map((r) => [...r].reverse().join(''));
function walk(rows, x, z) { const c = rows[z] && rows[z][x]; return c != null && !BLOCK.has(c); }
function reach(rows, sx, sz) {
  const D = rows.length, W = rows[0].length, seen = new Set([sz * W + sx]), q = [[sx, sz]];
  while (q.length) {
    const [x, z] = q.shift();
    for (const [dx, dz] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]]) {
      const nx = x + dx, nz = z + dz, k = nz * W + nx;
      if (nx < 0 || nz < 0 || nx >= W || nz >= D || seen.has(k) || !walk(rows, nx, nz)) continue;
      if (dx && dz && !walk(rows, x + dx, z) && !walk(rows, x, z + dz)) continue;
      seen.add(k); q.push([nx, nz]);
    }
  }
  return seen;
}
for (const m of Object.values(maps)) {
  const rows = m.rows();
  console.log(`맵 ${m.id} ${m.W}×${m.D}`);
  rows.forEach((r, z) => { if (r.length !== m.W) err(`줄 ${z} 길이 ${r.length}`); });
  for (const [x, z] of m.spawn) if (!walk(rows, x, z)) err(`출발 칸 막힘 ${x},${z}`);
  // 출발점 6칸 안 엄폐 칸 수
  const [sx, sz] = m.spawn[0];
  let cover = 0;
  rows.forEach((r, z) => [...r].forEach((c, x) => { if (COVER[c] && Math.hypot(x - sx, z - sz) <= 6) cover++; }));
  if (cover < 3) err(`출발점 근처 엄폐 ${cover}개 (3개 이상 권장)`);
  else console.log(`  출발점 6칸 안 엄폐 ${cover}`);
}
for (const mi of missions) {
  const m = maps[mi.map];
  const W = m.W, fx = (x) => (mi.mirror ? W - 1 - x : x);
  const rows = mi.mirror ? mirrorRows(m.rows()) : m.rows();
  const [sx, sz] = m.spawn[0];
  const R = reach(rows, fx(sx), sz);
  console.log(`임무 ${mi.id} (${mi.map}${mi.mirror ? ' 뒤집기' : ''}) 고양이 ${mi.cats.length}`);
  const pods = {};
  for (const c of mi.cats) {
    const x = fx(c.x);
    if (!walk(rows, x, c.z)) err(`${c.cls} 칸 막힘 ${c.x},${c.z} (${rows[c.z][x]})`);
    else if (!R.has(c.z * W + x)) err(`${c.cls} ${c.x},${c.z} 에 출발점에서 갈 길 없음`);
    (pods[c.pod] = pods[c.pod] || []).push([x, c.z]);
  }
  if (mi.goal && !R.has(mi.goal.z * W + fx(mi.goal.x))) err(`깃발 ${mi.goal.x},${mi.goal.z} 에 갈 길 없음`);
  // 첫 접촉 거리 · 무리 간 거리
  const centers = Object.entries(pods).map(([p, l]) => [p, l.reduce((a, b) => [a[0] + b[0] / l.length, a[1] + b[1] / l.length], [0, 0])]);
  const first = Math.min(...centers.map(([, c]) => Math.hypot(c[0] - fx(sx), c[1] - sz)));
  console.log(`  첫 무리까지 ${first.toFixed(1)}칸 (10~14 권장) · 무리 ${centers.length}`);
}
if (bad) { console.log(`\n문제 ${bad}개 — 저장 안 함`); process.exit(1); }

// ── 저장 ──
const out = path.join(__dirname, '../../RatWarUnity/Assets/Resources/Data');
fs.mkdirSync(out, { recursive: true });
const mapsJson = {};
for (const m of Object.values(maps)) mapsJson[m.id] = { rows: m.rows(), hills: m.hills, spawn: m.spawn };
fs.writeFileSync(path.join(out, 'maps.json'), JSON.stringify(mapsJson, null, 1));
fs.writeFileSync(path.join(out, 'missions.json'), JSON.stringify(missions, null, 1));
console.log('\n저장: ' + out);
for (const m of Object.values(maps)) { console.log(`\n[${m.id}]`); console.log(m.rows().map((r, z) => String(z).padStart(2) + ' ' + r).join('\n')); }
