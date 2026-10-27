// 쥐 전쟁 시뮬 — 맵 규칙: 칸 · 높이 · 시야(LOS) · 엄폐 · 길찾기 (웹 world.js)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Tile
{
    public int x, z, h;
    public char prop;      // '\0' = 없음
    public string ground;  // grass · mud · road · crater
    public bool HasProp => prop != '\0';
}

public struct Pt { public int x, z; public Pt(int x, int z) { this.x = x; this.z = z; } }

public class WorldMap
{
    public int W, D;
    public Tile[] tiles;
    public List<Unit> units = new List<Unit>();
    public GoalDef goal;
    public List<(Pt a, Pt b)> tunnels = new List<(Pt, Pt)>();
}

public static class World
{
    public static readonly int[][] DIRS4 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
    public static readonly int[][] DIRS8 = DIRS4.Concat(new[] { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } }).ToArray();

    public static WorldMap Create(BattleMapDef def)
    {
        int D = def.rows.Length, W = def.rows[0].Length;
        var w = new WorldMap { W = W, D = D, tiles = new Tile[W * D], goal = def.goal };
        for (int z = 0; z < D; z++)
            for (int x = 0; x < W; x++)
            {
                char ch = def.rows[z][x];
                bool onHill = def.hills.Any(H => Mathf.Pow((x - H.cx) / H.rx, 2) + Mathf.Pow((z - H.cz) / H.rz, 2) <= 1);
                w.tiles[z * W + x] = new Tile
                {
                    x = x, z = z, h = onHill ? 1 : 0,
                    prop = GameData.PROPS.ContainsKey(ch) ? ch : '\0',
                    ground = ch == ',' ? "mud" : ch == '=' ? "road" : ch == 'o' ? "crater" : "grass",
                };
            }
        return w;
    }

    public static Tile TileAt(WorldMap w, int x, int z) => x < 0 || z < 0 || x >= w.W || z >= w.D ? null : w.tiles[z * w.W + x];
    public static PropDef PropOf(Tile t) => t != null && t.HasProp ? GameData.PROPS[t.prop] : null;
    public static Unit UnitAt(WorldMap w, int x, int z) => w.units.FirstOrDefault(u => (u.alive || u.downed) && u.x == x && u.z == z);
    public static bool Walkable(WorldMap w, int x, int z) { var t = TileAt(w, x, z); return t != null && !(PropOf(t)?.block ?? false); }
    public static bool SightBlocker(Tile t) => PropOf(t)?.sightBlock ?? false;
    public static float Dist(int ax, int az, int bx, int bz) => Mathf.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    public static float Dist(Unit a, Unit b) => Dist(a.x, a.z, b.x, b.z);
    public static float Dist(Unit a, Pt b) => Dist(a.x, a.z, b.x, b.z);
    public static float Dist(Unit a, Tile b) => Dist(a.x, a.z, b.x, b.z);

    // 칸 중심끼리 선을 따라 지나는 칸들 (시작 · 끝 제외)
    static List<Tile> LineTiles(WorldMap w, int ax, int az, int bx, int bz)
    {
        var o = new List<Tile>(); var seen = new HashSet<int> { az * w.W + ax, bz * w.W + bx };
        int n = Mathf.CeilToInt(Dist(ax, az, bx, bz) * 5);
        for (int i = 1; i < n; i++)
        {
            float t = (float)i / n;
            int x = JsRound(ax + (bx - ax) * t), z = JsRound(az + (bz - az) * t);
            int k = z * w.W + x;
            if (seen.Add(k)) o.Add(TileAt(w, x, z));
        }
        return o;
    }
    // 자바스크립트 Math.round (0.5 는 위로)
    public static int JsRound(float v) => Mathf.FloorToInt(v + 0.5f);

    static bool ClearLine(WorldMap w, int ax, int az, int bx, int bz)
    {
        int top = Mathf.Max(TileAt(w, ax, az).h, TileAt(w, bx, bz).h);
        foreach (var t in LineTiles(w, ax, az, bx, bz)) if (t == null || SightBlocker(t) || t.h > top) return false;
        return true;
    }

    static bool NextToHigh(WorldMap w, int x, int z) => DIRS4.Any(d => SightBlocker(TileAt(w, x + d[0], z + d[1])));

    // 시야: 직선이 막혀도 높은 엄폐물 옆이면 옆 칸으로 몸을 내밀어 봄 (엑스컴식)
    public static bool Los(WorldMap w, int ax, int az, int bx, int bz)
    {
        if (ClearLine(w, ax, az, bx, bz)) return true;
        List<Pt> Peeks(int px, int pz)
        {
            var l = new List<Pt> { new Pt(px, pz) };
            if (NextToHigh(w, px, pz)) foreach (var d in DIRS4) if (Walkable(w, px + d[0], pz + d[1])) l.Add(new Pt(px + d[0], pz + d[1]));
            return l;
        }
        var pb = Peeks(bx, bz);
        foreach (var a in Peeks(ax, az))
            foreach (var b in pb)
                if ((a.x != ax || a.z != az || b.x != bx || b.z != bz) && ClearLine(w, a.x, a.z, b.x, b.z)) return true;
        return false;
    }

    public static float SightOf(WorldMap w, Unit u) => RULES.sight + u.sight + (TileAt(w, u.x, u.z).h > 0 ? 1 : 0);
    public static bool CanSee(WorldMap w, Unit u, int x, int z) => Dist(u.x, u.z, x, z) <= SightOf(w, u) + 0.01f && Los(w, u.x, u.z, x, z);
    public static bool CanSee(WorldMap w, Unit u, Unit p) => CanSee(w, u, p.x, p.z);

    // 대상 (px, pz) 가 from 쪽에서 오는 사격에 대해 받는 엄폐 (0 없음, 1 낮음, 2 높음) + 측면 노출 여부
    public static (int cover, bool flanked) CoverAgainst(WorldMap w, int px, int pz, int fx, int fz)
    {
        float vx = fx - px, vz = fz - pz, L = Mathf.Sqrt(vx * vx + vz * vz); if (L == 0) L = 1;
        int best = 0, any = 0;
        foreach (var d in DIRS4)
        {
            var pr = PropOf(TileAt(w, px + d[0], pz + d[1]));
            if (pr == null || pr.cover == 0) continue;
            any = Mathf.Max(any, pr.cover);
            if ((vx * d[0] + vz * d[1]) / L > 0.35f) best = Mathf.Max(best, pr.cover);
        }
        var self = PropOf(TileAt(w, px, pz));
        if (self != null && self.selfCover > 0) { best = Mathf.Max(best, self.selfCover); any = Mathf.Max(any, self.selfCover); }
        return (best, any > 0 && best == 0);
    }

    // 한 칸의 네 방향 엄폐 (이동 미리보기 방패 표시용)
    public static List<(int dx, int dz, int cover)> CoverSides(WorldMap w, int x, int z)
    {
        var l = new List<(int, int, int)>();
        foreach (var d in DIRS4) { var pr = PropOf(TileAt(w, x + d[0], z + d[1])); if (pr != null && pr.cover > 0) l.Add((d[0], d[1], pr.cover)); }
        return l;
    }

    public class Reach
    {
        public WorldMap w; public int start;
        public Dictionary<int, float> cost = new Dictionary<int, float>();
        public Dictionary<int, int> prev = new Dictionary<int, int>();
        public HashSet<int> pass = new HashSet<int>();
        public float? Cost(int x, int z) { int k = z * w.W + x; if (x < 0 || z < 0 || x >= w.W || z >= w.D || pass.Contains(k)) return null; return cost.TryGetValue(k, out var c) ? c : (float?)null; }
        public List<Pt> Path(int x, int z)
        {
            int k = z * w.W + x;
            if (!cost.ContainsKey(k)) return null;
            var o = new List<Pt>();
            while (k != start) { o.Insert(0, new Pt(k % w.W, k / w.W)); k = prev[k]; }
            return o;
        }
        // 멈출 수 있는 칸 (벽 위는 지나가기만)
        public IEnumerable<KeyValuePair<int, float>> Stops => cost.Where(kv => !pass.Contains(kv.Key));
    }

    // 길찾기 (다익스트라, 8방향, 대각선은 모서리 못 자름, 높이 바뀌면 +1)
    // 땅굴(w.tunnels, 쥐만) 은 입구 ↔ 출구 1걸음. 벽 타기(u.climb) 는 높은 엄폐물 위를 지나가되 멈추지는 못함
    public static Reach ReachOf(WorldMap w, Unit u, float maxCost)
    {
        var R = new Reach { w = w };
        int Key(int x, int z) => z * w.W + x;
        R.start = Key(u.x, u.z);
        R.cost[R.start] = 0;
        var open = new List<(float c, int x, int z)> { (0, u.x, u.z) };
        while (open.Count > 0)
        {
            int bi = 0; for (int i = 1; i < open.Count; i++) if (open[i].c < open[bi].c) bi = i;
            var (c, x, z) = open[bi]; open.RemoveAt(bi);
            if (c > R.cost[Key(x, z)]) continue;
            int h0 = TileAt(w, x, z).h;
            var steps = DIRS8.Select(d => (d[0], d[1], false)).ToList();
            if (u.side == "rat" && w.tunnels != null)
                foreach (var (a, b) in w.tunnels)
                {
                    if (a.x == x && a.z == z) steps.Add((b.x - x, b.z - z, true));
                    if (b.x == x && b.z == z) steps.Add((a.x - x, a.z - z, true));
                }
            foreach (var (dx, dz, tun) in steps)
            {
                int nx = x + dx, nz = z + dz;
                var ntile = TileAt(w, nx, nz);
                bool climbOver = u.climb && !tun && ntile != null && PropOf(ntile) != null && PropOf(ntile).cover == 2 && !(dx != 0 && dz != 0);
                if (!Walkable(w, nx, nz) && !climbOver) continue;
                if (tun)
                {
                    float tc = c + 1; int tk = Key(nx, nz);
                    if (tc <= maxCost + 1e-6f && UnitAt(w, nx, nz) == null && (!R.cost.ContainsKey(tk) || tc < R.cost[tk])) { R.cost[tk] = tc; R.prev[tk] = Key(x, z); open.Add((tc, nx, nz)); }
                    continue;
                }
                bool w1 = Walkable(w, x + dx, z), w2 = Walkable(w, x, z + dz);
                if (dx != 0 && dz != 0 && !w1 && !w2) continue;
                float corner = dx != 0 && dz != 0 && (!w1 || !w2) ? 0.35f : 0;
                var o = UnitAt(w, nx, nz);
                if (o != null && o != u) continue;
                float nc = c + (dx != 0 && dz != 0 ? RULES.diagCost : 1) + corner + (ntile.h != h0 ? RULES.climbCost : 0) + (climbOver ? 1 : 0);
                if (nc > maxCost + 1e-6f) continue;
                int k = Key(nx, nz);
                if (climbOver) R.pass.Add(k);
                if (!R.cost.ContainsKey(k) || nc < R.cost[k]) { R.cost[k] = nc; R.prev[k] = Key(x, z); open.Add((nc, nx, nz)); }
            }
        }
        return R;
    }
}
