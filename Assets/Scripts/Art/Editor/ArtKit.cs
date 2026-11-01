using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

namespace IronRing.Art.Editor
{
    /// <summary>스토리 019. 팔레트 한 색의 세 명암(밝음 L·기본 B·그늘 S).</summary>
    public readonly struct Swatch
    {
        public readonly Color32 L, B, S;

        /// <summary>밝음 l, 기본 b, 그늘 s로 명암 묶음을 만든다.</summary>
        public Swatch(Color32 l, Color32 b, Color32 s) { L = l; B = b; S = s; }
    }

    /// <summary>스토리 019. Assets/Art/Palette/SteampunkPalette.json을 읽어 이름으로 명암 세 단계를 돌려준다. 생성기는 색을 코드에 두지 않는다.</summary>
    public sealed class SteampunkPalette
    {
        public const string PATH = "Assets/Art/Palette/SteampunkPalette.json";
        private readonly Dictionary<string, Swatch> _swatches = new Dictionary<string, Swatch>();

        /// <summary>name 색의 명암 묶음. 없으면 예외.</summary>
        public Swatch this[string name] => _swatches.TryGetValue(name, out Swatch s) ? s : throw new KeyNotFoundException("Palette color missing: " + name);

        /// <summary>팔레트 파일을 읽는다.</summary>
        public static SteampunkPalette Load()
        {
            var data = JsonUtility.FromJson<Data>(File.ReadAllText(PATH));
            if (data == null || data.colors == null || data.colors.Length == 0) throw new FormatException("Empty palette: " + PATH);
            var palette = new SteampunkPalette();
            foreach (Entry e in data.colors) palette._swatches[e.name] = new Swatch(Parse(e.light), Parse(e.@base), Parse(e.shade));
            return palette;
        }

        /// <summary>#rrggbb 문자열을 Color32로 바꾼다.</summary>
        public static Color32 Parse(string hex) => hex != null && ColorUtility.TryParseHtmlString(hex, out Color c) ? (Color32)c : throw new FormatException("Bad palette color: " + hex);

#pragma warning disable 0649
        [Serializable] private sealed class Data { public Entry[] colors; }
        [Serializable] private sealed class Entry { public string name; public string light; public string @base; public string shade; }
#pragma warning restore 0649
    }

    /// <summary>스토리 019. 픽셀 버퍼(y=0이 아래). 알파 합성, 외곽선 레이어, 기본 도형을 제공한다.</summary>
    public sealed class PixelCanvas
    {
        public readonly int Size;
        public Color32[] Px;
        private Color32[] _under;

        /// <summary>size×size 투명 버퍼를 만든다.</summary>
        public PixelCanvas(int size) { Size = size; Px = new Color32[size * size]; }

        /// <summary>x,y 픽셀(범위 밖은 투명).</summary>
        public Color32 Get(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size ? Px[y * Size + x] : default;

        /// <summary>x,y에 c를 알파 합성한다.</summary>
        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || c.a == 0) return;
            int i = y * Size + x;
            Px[i] = c.a == 255 ? c : Over(c, Px[i]);
        }

        /// <summary>x,y를 투명으로 지운다.</summary>
        public void Erase(int x, int y) { if (x >= 0 && y >= 0 && x < Size && y < Size) Px[y * Size + x] = default; }

        /// <summary>s를 d 위에 덮는 알파 합성.</summary>
        public static Color32 Over(Color32 s, Color32 d)
        {
            float a = s.a / 255f, da = d.a / 255f * (1 - a), oa = a + da;
            if (oa <= 0f) return default;
            return new Color32((byte)((s.r * a + d.r * da) / oa), (byte)((s.g * a + d.g * da) / oa), (byte)((s.b * a + d.b * da) / oa), (byte)Mathf.RoundToInt(oa * 255f));
        }

        /// <summary>x,y에서 w×h 사각형을 채운다.</summary>
        public void Rect(int x, int y, int w, int h, Color32 c) { for (int py = y; py < y + h; py++) for (int px = x; px < x + w; px++) Set(px, py, c); }

        /// <summary>cx,cy 중심 rx,ry 타원을 채운다.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>cx,cy 중심 r 원을 채운다.</summary>
        public void Disc(float cx, float cy, float r, Color32 c) { if (r > 0) Ellipse(cx, cy, r, r, c); }

        /// <summary>rIn 초과 rOut 이하의 고리를 채운다.</summary>
        public void Ring(float cx, float cy, float rIn, float rOut, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - rOut - 1); y <= Mathf.CeilToInt(cy + rOut + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rOut - 1); x <= Mathf.CeilToInt(cx + rOut + 1); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = dx * dx + dy * dy;
                    if (d > rIn * rIn && d <= rOut * rOut) Set(x, y, c);
                }
        }

        /// <summary>브레젠험 선(thick 두께).</summary>
        public void Line(int x0, int y0, int x1, int y1, Color32 c, int thick = 1)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                if (thick <= 1) Set(x0, y0, c); else Rect(x0 - thick / 2, y0 - thick / 2, thick, thick, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>새 투명 레이어에 그리기 시작한다. EndLayer가 외곽선을 둘러 아래에 합친다.</summary>
        public void BeginLayer() { _under = Px; Px = new Color32[Size * Size]; }

        /// <summary>레이어의 불투명 픽셀 둘레(4방향)에 outline을 두르고 아래 버퍼에 합성한다.</summary>
        public void EndLayer(Color32 outline)
        {
            Color32[] layer = Px;
            var edge = new bool[layer.Length];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                if (layer[y * Size + x].a >= 128) continue;
                edge[y * Size + x] = (x > 0 && layer[y * Size + x - 1].a >= 128) || (x < Size - 1 && layer[y * Size + x + 1].a >= 128)
                    || (y > 0 && layer[(y - 1) * Size + x].a >= 128) || (y < Size - 1 && layer[(y + 1) * Size + x].a >= 128);
            }
            Px = _under ?? new Color32[Size * Size];
            _under = null;
            for (int i = 0; i < layer.Length; i++)
            {
                if (edge[i]) Set(i % Size, i / Size, outline);
                if (layer[i].a > 0) Set(i % Size, i / Size, layer[i]);
            }
        }

        /// <summary>x와 y를 맞바꾼다(가로 타일을 세로로).</summary>
        public void Transpose()
        {
            for (int y = 0; y < Size; y++) for (int x = y + 1; x < Size; x++)
            {
                Color32 t = Px[y * Size + x]; Px[y * Size + x] = Px[x * Size + y]; Px[x * Size + y] = t;
            }
        }

        /// <summary>결정적 위치 해시(같은 입력이면 같은 무늬).</summary>
        public static int Hash(int x, int y, int salt)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        /// <summary>버퍼를 PNG로 저장한다.</summary>
        public void Save(string path)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(Px); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    /// <summary>
    /// 스토리 019. 64픽셀 탑뷰 스팀펑크 도트의 공용 도구: 팔레트 명암 묶음, 현재 캔버스, 반복 모티프(돌바닥·벽돌·슬레이트 지붕·창 불빛·굴뚝·톱니·
    /// 상자·배럴·관목·가스등·연기), 임포터 설정과 콘택트 시트. 모든 색은 SteampunkPalette.json에서 온다.
    /// </summary>
    public static class ArtKit
    {
        public const int SIZE = 64;
        public const int PPU = 64;
        public static SteampunkPalette P;
        public static PixelCanvas C;
        public static Swatch O, Mo, G, Sa, Br, W, W2, R, Cu, Bs, Sm, Gl, Wa, Sh, Ms, Wd, Fe, Em, Fa, Bg;

        /// <summary>팔레트 파일을 읽어 이름 붙은 명암 묶음을 채운다.</summary>
        public static void Load()
        {
            P = SteampunkPalette.Load();
            O = P["outline"]; Mo = P["mortar"]; G = P["ground"]; Sa = P["sandstone"]; Br = P["brick"]; W = P["wall"]; W2 = P["wall_alt"];
            R = P["roof"]; Cu = P["copper"]; Bs = P["brass"]; Sm = P["smoke"]; Gl = P["glow"]; Wa = P["water"]; Sh = P["shrub"];
            Ms = P["moss"]; Wd = P["wood"]; Fe = P["iron"]; Em = P["ember"]; Fa = P["fabric"]; Bg = P["background"];
        }

        /// <summary>새 64×64 투명 캔버스를 연다.</summary>
        public static void Begin() { C = new PixelCanvas(SIZE); }
        /// <summary>외곽선 레이어를 연다.</summary>
        public static void Layer() => C.BeginLayer();
        /// <summary>레이어에 팔레트 외곽선을 둘러 합친다.</summary>
        public static void EndLayer() => C.EndLayer(O.B);
        /// <summary>결정적 해시.</summary>
        public static int H(int x, int y, int salt) => PixelCanvas.Hash(x, y, salt);
        /// <summary>두 색 보간.</summary>
        public static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);
        /// <summary>c의 알파만 바꾼다.</summary>
        public static Color32 A(Color32 c, byte alpha) => new Color32(c.r, c.g, c.b, alpha);
        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        /// <summary>윗줄·왼줄 밝음, 아랫줄·오른줄 그늘의 상자.</summary>
        public static void Box(int x, int y, int w, int h, Swatch s)
        {
            C.Rect(x, y, w, h, s.B);
            C.Rect(x, y + h - 1, w, 1, s.L); C.Rect(x, y, 1, h - 1, s.L);
            C.Rect(x, y, w, 1, s.S); C.Rect(x + w - 1, y, 1, h - 1, s.S);
        }

        /// <summary>세로 원통(왼쪽 하이라이트 띠, 오른쪽 그늘).</summary>
        public static void CylV(int x, int y, int w, int h, Swatch s)
        {
            for (int i = 0; i < w; i++)
            {
                float t = (i + 0.5f) / w;
                C.Rect(x + i, y, 1, h, t < 0.15f ? s.B : t < 0.38f ? s.L : t < 0.7f ? s.B : s.S);
            }
        }

        /// <summary>가로 원통(위 하이라이트 띠, 아래 그늘).</summary>
        public static void CylH(int x, int y, int w, int h, Swatch s)
        {
            for (int j = 0; j < h; j++)
            {
                float t = (j + 0.5f) / h;
                C.Rect(x, y + h - 1 - j, w, 1, t < 0.15f ? s.B : t < 0.4f ? s.L : t < 0.72f ? s.B : s.S);
            }
        }

        /// <summary>왼쪽 위에서 빛을 받는 둥근 지붕/원판.</summary>
        public static void Dome(float cx, float cy, float r, Swatch s)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / r, dy = (y + 0.5f - cy) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) continue;
                    float l = -dx * 0.6f + dy * 0.8f;
                    C.Set(x, y, d > 0.86f && l < 0.2f ? s.S : l > 0.38f ? s.L : l < -0.42f ? s.S : s.B);
                }
        }

        /// <summary>둥근 테두리(cx,cy 중심 radius) 위에 count개 리벳.</summary>
        public static void RivetRing(float cx, float cy, float radius, int count, Color32 c)
        {
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2 / count;
                C.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * radius), Mathf.FloorToInt(cy + Mathf.Sin(a) * radius), c);
            }
        }

        /// <summary>x0..x1 가로줄(y)에 step 간격 리벳.</summary>
        public static void RivetsH(int x0, int x1, int y, int step, Color32 c) { for (int x = x0; x <= x1; x += step) C.Set(x, y, c); }

        /// <summary>y0..y1 세로줄(x)에 step 간격 리벳.</summary>
        public static void RivetsV(int x, int y0, int y1, int step, Color32 c) { for (int y = y0; y <= y1; y += step) C.Set(x, y, c); }

        /// <summary>돌 포장(행 높이 8, 줄눈 mortar, 돌마다 명암, 줄눈 이끼·먼지 점).</summary>
        public static void Cobble(int x0, int y0, int w, int h, Swatch stone, int salt)
        {
            C.Rect(x0, y0, w, h, Mo.B);
            for (int row = 0; row * 8 < h; row++)
            {
                int ry = y0 + row * 8;
                int cursor = -(H(row, 0, salt) % 8), n = 0;
                while (cursor < w)
                {
                    int sw = 7 + H(row, n, salt + 1) % 5;
                    int v = H(row, n, salt + 2) % 7;
                    Color32 fill = v == 0 ? Mix(stone.B, stone.L, 0.5f) : v == 1 ? Mix(stone.B, stone.S, 0.6f) : stone.B;
                    for (int yy = 0; yy < 7; yy++) for (int xx = 0; xx < sw - 1; xx++)
                    {
                        if ((xx == 0 || xx == sw - 2) && (yy == 0 || yy == 6)) continue;
                        int px = cursor + xx, py = ry + yy;
                        if (px < 0 || px >= w || py >= y0 + h) continue;
                        Color32 c = yy == 6 || xx == 0 ? Mix(fill, stone.L, 0.7f) : yy == 0 || xx == sw - 2 ? stone.S : fill;
                        C.Set(x0 + px, py, c);
                    }
                    cursor += sw; n++;
                }
            }
            for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++)
            {
                int hsh = H(x, y, salt + 9);
                if (Same(C.Get(x, y), Mo.B)) { if (hsh % 7 == 0) C.Set(x, y, hsh % 2 == 0 ? Ms.B : Ms.S); }
                else if (hsh % 61 == 0) C.Set(x, y, Sa.S);
            }
        }

        /// <summary>벽돌 벽(4줄마다 줄눈, 엇갈린 세로 줄눈, 윗줄 밝음).</summary>
        public static void Bricks(int x, int y, int w, int h, Swatch s)
        {
            C.Rect(x, y, w, h, s.B);
            for (int j = 0; j < h; j += 4)
            {
                C.Rect(x, y + j, w, 1, s.S);
                for (int i = (j / 4) % 2 == 0 ? 3 : 7; i < w; i += 8) C.Rect(x + i, y + j, 1, Math.Min(4, h - j), s.S);
            }
            C.Rect(x, y + h - 1, w, 1, s.L);
        }

        /// <summary>지붕. corrugated면 골함석 세로 줄무늬, 아니면 슬레이트 비늘 줄. 처마(아래)는 그늘, 용마루(위)는 밝음.</summary>
        public static void Roof(int x, int y, int w, int h, Swatch r, bool corrugated)
        {
            C.Rect(x, y, w, h, r.B);
            if (corrugated)
                for (int i = 0; i < w; i += 3) { C.Rect(x + i, y, 1, h, r.L); C.Rect(x + i + 2, y, 1, h, r.S); }
            else
                for (int j = 2; j < h; j += 3)
                {
                    C.Rect(x, y + j, w, 1, r.S);
                    for (int i = (j / 3) % 2 == 0 ? 1 : 4; i < w; i += 6) C.Set(x + i, y + j - 1, r.S);
                    for (int i = (j / 3) % 2 == 0 ? 3 : 0; i < w; i += 9) C.Set(x + i, y + j + 1, r.L);
                }
            C.Rect(x, y + h - 2, w, 2, r.L);
            C.Rect(x, y, w, 2, r.S);
            C.Rect(x, y, w, 1, Bs.S);
        }

        /// <summary>창. lit이면 호박색 불빛(위 밝음·아래 진함)과 창살, 아니면 꺼진 어두운 유리.</summary>
        public static void Window(int x, int y, int w, int h, bool lit)
        {
            C.Rect(x - 1, y - 1, w + 2, h + 2, O.B);
            C.Rect(x, y, w, h, lit ? Gl.B : Mo.S);
            if (lit) { C.Rect(x, y + h - 2, w, 2, Gl.L); C.Rect(x, y, w, 1, Gl.S); }
            Color32 bar = lit ? Wd.S : O.S;
            if (w >= 5) C.Rect(x + w / 2, y, 1, h, bar);
            if (h >= 6) C.Rect(x, y + h / 2, w, 1, bar);
            C.Rect(x - 1, y - 2, w + 2, 1, Bs.S);
        }

        /// <summary>굴뚝: 원통 몸통, 쇠 테 머리, 어두운 구멍, 황동 띠. 머리 윗줄은 y+h-1.</summary>
        public static void Chimney(int x, int y, int w, int h, Swatch body)
        {
            CylV(x, y, w, h - 3, body);
            C.Rect(x, y + (h - 3) / 2, w, 1, Bs.S);
            C.Rect(x - 1, y + h - 4, w + 2, 4, Fe.B); C.Rect(x - 1, y + h - 1, w + 2, 1, Fe.L); C.Rect(x - 1, y + h - 4, w + 2, 1, Fe.S);
            C.Rect(x + 1, y + h - 2, w - 2, 1, O.S);
        }

        /// <summary>톱니바퀴: 이빨, 바큇살 구멍, 축. 왼쪽 위 밝음.</summary>
        public static void Gear(float cx, float cy, float r, int teeth, Swatch s)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2) + 1f;
                    bool tooth = (ang * teeth * 2) % 2f < 1f;
                    float body = r - Mathf.Max(2f, r * 0.2f);
                    if (d > r || (d > body && !tooth)) continue;
                    float l = (-dx * 0.6f + dy * 0.8f) / Mathf.Max(d, 0.01f);
                    Color32 c = l > 0.4f ? s.L : l < -0.4f ? s.S : s.B;
                    if (r >= 7 && d > r * 0.3f && d < body * 0.72f && (ang * 5f) % 1f > 0.3f) c = Mix(s.S, O.B, 0.55f);
                    if (r >= 7 && Mathf.Abs(d - body * 0.74f) < 0.6f) c = s.S;
                    if (d < Mathf.Max(1.2f, r * 0.14f)) c = O.B;
                    C.Set(x, y, c);
                }
        }

        /// <summary>나무 상자: 판자 줄, X 버팀, 황동 모서리.</summary>
        public static void Crate(int x, int y, int w, int h)
        {
            Box(x, y, w, h, Wd);
            for (int j = 3; j < h - 1; j += 4) C.Rect(x + 1, y + j, w - 2, 1, Wd.S);
            C.Line(x + 1, y + 1, x + w - 2, y + h - 2, Wd.L); C.Line(x + 1, y + h - 2, x + w - 2, y + 1, Wd.L);
            C.Rect(x + 1, y + 1, w - 2, 1, Wd.S);
            C.Set(x + 1, y + 1, Bs.B); C.Set(x + w - 2, y + 1, Bs.B); C.Set(x + 1, y + h - 2, Bs.L); C.Set(x + w - 2, y + h - 2, Bs.L);
        }

        /// <summary>위에서 본 배럴: 쇠 테, 나무 뚜껑, 황동 마개. water면 뚜껑 대신 물.</summary>
        public static void Barrel(float cx, float cy, float r, bool water)
        {
            C.Disc(cx, cy, r, Fe.S);
            C.Disc(cx, cy, r - 1, Wd.B);
            C.Ring(cx, cy, r - 3, r - 2, Fe.B);
            if (water) { C.Disc(cx, cy, r - 3, Wa.B); C.Disc(cx - 1, cy + 1, r - 5, Wa.L); C.Disc(cx + 1, cy - 1, r - 6, Wa.B); return; }
            C.Disc(cx, cy, r - 3, Wd.L);
            for (int k = -2; k <= 2; k += 2) C.Rect(Mathf.FloorToInt(cx) + k * 2, Mathf.FloorToInt(cy - r + 4), 1, Mathf.FloorToInt(2 * r - 8), Wd.B);
            C.Disc(cx + r * 0.3f, cy - r * 0.2f, 1.2f, Bs.B);
        }

        /// <summary>관목 덩어리: 그늘 바닥, 기본 잎, 왼쪽 위 밝은 잎.</summary>
        public static void Shrub(float cx, float cy, float r, int salt)
        {
            C.Disc(cx, cy, r, Sh.S);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.05f + H(k, 1, salt) % 10 * 0.05f;
                C.Disc(cx + Mathf.Cos(a) * r * 0.45f, cy + Mathf.Sin(a) * r * 0.45f, r * 0.5f, Sh.B);
            }
            C.Disc(cx - r * 0.3f, cy + r * 0.3f, r * 0.35f, Sh.L);
            C.Disc(cx + r * 0.25f, cy + r * 0.05f, r * 0.2f, Sh.L);
            int span = Mathf.Max(1, (int)(2 * r));
            for (int k = 0; k < 6; k++) C.Set(Mathf.FloorToInt(cx - r) + H(k, 2, salt) % span, Mathf.FloorToInt(cy - r) + H(k, 3, salt) % span, Sh.S);
        }

        /// <summary>호박색 광륜(2단 반투명 원).</summary>
        public static void Glow(float cx, float cy, float r)
        {
            C.Disc(cx, cy, r, A(Gl.B, 40));
            C.Disc(cx, cy, r * 0.66f, A(Gl.L, 60));
        }

        /// <summary>가스등: 받침, 쇠기둥, 황동 등갓과 호박 불빛. halo면 광륜.</summary>
        public static void LampPost(int cx, int by, int h, bool halo)
        {
            if (halo) Glow(cx + 0.5f, by + h + 3.5f, 13);
            C.Rect(cx - 3, by, 7, 3, Fe.S); C.Rect(cx - 2, by + 2, 5, 1, Fe.B);
            C.Rect(cx - 1, by + 3, 3, h - 3, Fe.B); C.Rect(cx - 1, by + 3, 1, h - 3, Fe.L);
            Box(cx - 4, by + h, 9, 8, Bs);
            C.Rect(cx - 2, by + h + 1, 5, 6, Gl.B); C.Rect(cx - 1, by + h + 3, 3, 3, Gl.L);
            C.Rect(cx - 5, by + h + 8, 11, 2, Fe.S); C.Rect(cx - 4, by + h + 9, 9, 1, Fe.B); C.Set(cx, by + h + 10, Bs.L);
        }

        /// <summary>연기 한 덩이(반투명, 그늘→기본→밝음).</summary>
        public static void Puff(float cx, float cy, float r, byte alpha)
        {
            if (r < 1f) return;
            C.Disc(cx + 0.5f, cy - 0.5f, r, A(Sm.S, alpha));
            C.Disc(cx - 0.3f, cy + 0.3f, r * 0.82f, A(Sm.B, alpha));
            C.Disc(cx - r * 0.35f, cy + r * 0.35f, r * 0.42f, A(Sm.L, alpha));
        }

        /// <summary>가로 관(원통 + 양끝 황동 플랜지).</summary>
        public static void PipeH(int x, int y, int w, int thick, Swatch s)
        {
            CylH(x, y, w, thick, s);
            Box(x, y - 1, 3, thick + 2, Bs); Box(x + w - 3, y - 1, 3, thick + 2, Bs);
        }

        /// <summary>잔해 조각 count개(돌·나무·벽·지붕).</summary>
        public static void Rubble(int x, int y, int w, int h, int count, int salt)
        {
            for (int k = 0; k < count; k++)
            {
                int px = x + H(k, 1, salt) % Math.Max(1, w), py = y + H(k, 2, salt) % Math.Max(1, h);
                int sw = 3 + H(k, 3, salt) % 4, sh = 2 + H(k, 4, salt) % 3;
                Swatch s = k % 4 == 0 ? Wd : k % 4 == 1 ? W : k % 4 == 2 ? G : R;
                Box(px, py, sw, sh, s);
            }
        }

        /// <summary>현재 레이어를 폐허로: 어둡게, 윗선을 들쭉날쭉 깎고, 지붕에 구멍과 부러진 들보.</summary>
        public static void Ruin(int salt)
        {
            Color32[] px = C.Px;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                Color32 m = Mix(px[i], O.S, 0.4f);
                px[i] = new Color32(m.r, m.g, m.b, px[i].a);
            }
            for (int x = 0; x < SIZE; x++)
            {
                int top = -1;
                for (int y = SIZE - 1; y >= 0; y--) if (C.Get(x, y).a > 0) { top = y; break; }
                if (top < 0) continue;
                int cut = 2 + H(x / 3, 0, salt) % 9;
                for (int y = top; y > top - cut && y >= 0; y--) C.Erase(x, y);
            }
            for (int k = 0; k < 5; k++)
            {
                float cx = 12 + H(k, 1, salt) % 40, cy = 26 + H(k, 2, salt) % 22, r = 3 + H(k, 3, salt) % 4;
                for (int y = (int)(cy - r - 2); y <= cy + r + 2; y++) for (int x = (int)(cx - r - 2); x <= cx + r + 2; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) + H(x, y, salt) % 3 * 0.5f;
                    if (C.Get(x, y).a == 0 || d > r + 1) continue;
                    C.Set(x, y, d > r ? Mo.S : H(x, y, salt + 1) % 7 == 0 ? Wd.S : O.S);
                }
                C.Line((int)(cx - r), (int)(cy - 1), (int)(cx + r), (int)(cy + 2), Wd.S);
            }
        }

        /// <summary>단일 스프라이트, PPU 64, Point, 무압축으로 임포트한다.</summary>
        public static void ConfigureImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Not imported: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PPU;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        /// <summary>sprites를 perRow칸씩(칸 72px, 배경 팔레트 background) 늘어놓은 콘택트 시트 PNG 바이트.</summary>
        public static byte[] Sheet(List<Color32[]> sprites, int perRow)
        {
            const int cell = SIZE + 8;
            int rows = Math.Max(1, (sprites.Count + perRow - 1) / perRow);
            var sheet = new Texture2D(cell * perRow, cell * rows, TextureFormat.RGBA32, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = Bg.B;
            for (int i = 0; i < sprites.Count; i++)
            {
                int ox = (i % perRow) * cell + 4, oy = (rows - 1 - i / perRow) * cell + 4;
                for (int y = 0; y < SIZE; y++) for (int x = 0; x < SIZE; x++)
                {
                    int k = (oy + y) * sheet.width + ox + x;
                    fill[k] = PixelCanvas.Over(sprites[i][y * SIZE + x], fill[k]);
                }
            }
            sheet.SetPixels32(fill); sheet.Apply();
            byte[] png = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);
            return png;
        }
    }
}
