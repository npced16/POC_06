using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityEditor;

using static IronRing.Art.Editor.ArtKit;

namespace IronRing.Art.Editor
{
    /// <summary>
    /// 온보딩 팝업용 임시 마스코트(상반신, 화면 오른쪽을 봄). 128×128 픽셀아트로 그려 최근접 4배 확대한 512×512 투명 PNG로 저장한다(Assets/Art/Generated/mascot/).
    /// 후보 3안(황동 증기 로봇·역무원 모자 기계 부엉이·랜턴 머리 로봇) × 표정 3개(기본·칭찬·주의)의 콘택트 시트와, 고른 안(<see cref="CHOSEN"/>)의 표정 3장을 만든다.
    /// 화풍은 SpriteGenPortraits와 같다(외곽선 레이어, 왼쪽 위 빛 2~3단 명암). 색은 모두 SteampunkPalette.json.
    /// </summary>
    public static class SpriteGenMascot
    {
        /// <summary>출력 폴더.</summary>
        public const string OUTPUT = "Assets/Art/Generated/mascot/";
        /// <summary>그리는 캔버스 크기와 저장 배율(128×4 = 512).</summary>
        public const int CANVAS = 128, SCALE = 4;
        /// <summary>기본으로 고른 후보 번호(<see cref="CANDIDATES"/>).</summary>
        public const int CHOSEN = 1;

        /// <summary>후보 안 이름.</summary>
        public static readonly string[] CANDIDATES = { "boiler_bot", "station_owl", "lantern_bot" };
        /// <summary>표정 이름(파일 이름 끝).</summary>
        public static readonly string[] EXPRESSIONS = { "default", "happy", "alert" };

        private static int _e;

        /// <summary>
        /// 후보 3안 × 표정 3개의 콘택트 시트와 고른 안의 표정 3장(512×512)을 다시 그리고 임포트 설정을 맞춘다(멱등).
        /// 팔레트 파일을 읽고, OUTPUT 폴더의 PNG와 임포트 설정을 바꾼다.
        /// </summary>
        [MenuItem("IronRing/Art/Generate Mascot")]
        public static void Generate()
        {
            ArtKit.Load();
            Directory.CreateDirectory(OUTPUT);
            var sheet = new List<Color32[]>();
            for (int e = 0; e < EXPRESSIONS.Length; e++)
                for (int c = 0; c < CANDIDATES.Length; c++) sheet.Add(Draw(c, e));
            File.WriteAllBytes(OUTPUT + "mascot_contact_sheet.png", SheetPng(sheet, CANDIDATES.Length, 2));
            for (int e = 0; e < EXPRESSIONS.Length; e++) SaveScaled(OUTPUT + "mascot_" + EXPRESSIONS[e] + ".png", Draw(CHOSEN, e));
            AssetDatabase.Refresh();
            for (int e = 0; e < EXPRESSIONS.Length; e++) Configure(OUTPUT + "mascot_" + EXPRESSIONS[e] + ".png", PPU * SCALE);
            Configure(OUTPUT + "mascot_contact_sheet.png", PPU);
            Debug.Log($"MASCOT: 후보 {CANDIDATES.Length}안 × 표정 {EXPRESSIONS.Length}, 기본 안 {CANDIDATES[CHOSEN]} → {OUTPUT}");
        }

        /// <summary>
        /// candidate 후보를 expression 표정으로 새 128×128 캔버스에 그린다.
        /// _e와 ArtKit.C를 바꾸고, 그린 픽셀 배열의 복사본을 돌려준다.
        /// </summary>
        private static Color32[] Draw(int candidate, int expression)
        {
            _e = expression;
            ArtKit.C = new PixelCanvas(CANVAS);
            switch (candidate)
            {
                case 0: BoilerBot(); break;
                case 1: StationOwl(); break;
                default: LanternBot(); break;
            }
            Effects();
            return (Color32[])C.Px.Clone();
        }

        // ---------------------------------------------------------------- 후보 0: 황동 증기 로봇

        /// <summary>
        /// 황동 보일러 몸통, 구리 배관, 굴뚝 머리, 렌즈 눈의 증기 로봇을 그린다.
        /// 현재 표정 _e를 쓰고, 현재 캔버스 C에 그린다.
        /// </summary>
        private static void BoilerBot()
        {
            Layer();
            Pipe(36, 28, 27, 44, 33, 62);
            Pipe(90, 28, 97, 42, 92, 58);
            EndLayer();

            Layer();
            Shade(62, 4, 42, 30, Bs, 2.6f, 0.3f);
            RivetsH(30, 94, 21, 5, Bs.S);
            C.Rect(61, 0, 2, 20, Bs.S);
            Gauge(76, 12, 7f);
            EndLayer();

            Layer(); Box(50, 30, 26, 9, Fe); RivetsH(52, 74, 34, 4, Bs.B); EndLayer();

            Layer(); CylV(66, 96, 10, 12, Fe); Box(64, 106, 14, 3, Bs); EndLayer();

            Layer();
            Shade(64, 72, 31, 26, Bs, 3.2f, 0.4f);
            RivetsH(42, 86, 94, 6, Bs.S);
            Gear(37, 72, 6.5f, 8, Cu);
            Shade(69, 70, 23, 17, Fe, 3f, 0.5f);
            EndLayer();

            Lens(59, 74, 8); Lens(81, 74, 7);
            Brow(59, 74, 8, true, Bs); Brow(81, 74, 7, false, Bs);
            GrilleMouth(70, 58);

            // 주의 표정에서는 굴뚝 연기를 더 크고 길게 뿜는다.
            if (_e == 2) { Puff(76, 114, 5, 190); Puff(84, 120, 4, 160); Puff(92, 124, 3, 130); }
            else { Puff(75, 115, 4, 150); Puff(81, 121, 3, 110); }
        }

        /// <summary>
        /// 세 점(x0,y0)→(x1,y1)→(x2,y2)을 잇는 두께 5의 구리 배관과 가운데 황동 이음쇠를 그린다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Pipe(int x0, int y0, int x1, int y1, int x2, int y2)
        {
            C.Line(x0, y0, x1, y1, Cu.B, 5); C.Line(x1, y1, x2, y2, Cu.B, 5);
            C.Line(x0 - 1, y0, x1 - 1, y1, Cu.L); C.Line(x1 - 1, y1, x2 - 1, y2, Cu.L);
            C.Line(x0 + 2, y0, x1 + 2, y1, Cu.S); C.Line(x1 + 2, y1, x2 + 2, y2, Cu.S);
            Box(x1 - 3, y1 - 2, 7, 4, Bs);
        }

        /// <summary>
        /// (cx,cy) 중심 반지름 r의 가슴 압력계를 그린다. 바늘 각도는 표정 _e에 따라 기본·칭찬·주의(빨간 눈금 쪽)로 바뀐다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Gauge(float cx, float cy, float r)
        {
            LitRing(cx, cy, r - 1.5f, r, Bs);
            C.Disc(cx, cy, r - 1.5f, Sm.B);
            C.Disc(cx - 1, cy + 1, r - 3.5f, Sm.L);
            for (int k = 0; k < 5; k++)
            {
                float a = Mathf.Deg2Rad * (160 - k * 35);
                C.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * (r - 2.5f)), Mathf.FloorToInt(cy + Mathf.Sin(a) * (r - 2.5f)), k == 4 ? Em.B : O.L);
            }
            float n = Mathf.Deg2Rad * (_e == 0 ? 120 : _e == 1 ? 90 : 25);
            C.Line(Mathf.FloorToInt(cx), Mathf.FloorToInt(cy), Mathf.FloorToInt(cx + Mathf.Cos(n) * (r - 3)), Mathf.FloorToInt(cy + Mathf.Sin(n) * (r - 3)), _e == 2 ? Em.S : O.B);
            C.Set(Mathf.FloorToInt(cx), Mathf.FloorToInt(cy), O.B);
        }

        /// <summary>
        /// (cx,cy) 중심의 격자 입을 그린다. 칭찬은 양끝이 올라간 웃는 격자, 주의는 안쪽이 주황으로 달아오른 벌린 입이다.
        /// 표정 _e를 쓰고, 현재 캔버스 C에 그린다.
        /// </summary>
        private static void GrilleMouth(int cx, int cy)
        {
            if (_e == 2)
            {
                C.Rect(cx - 7, cy - 5, 14, 8, O.S);
                C.Rect(cx - 5, cy - 4, 10, 3, A(Em.B, 210));
                C.Rect(cx - 7, cy + 2, 14, 1, Fe.L);
                return;
            }
            for (int i = -8; i <= 8; i++)
            {
                float t = i / 8f;
                int lift = _e == 1 ? Mathf.RoundToInt(t * t * 4f) : 0;
                C.Rect(cx + i, cy - 2 + lift, 1, 4, O.S);
                if ((i + 8) % 3 == 1) C.Rect(cx + i, cy - 1 + lift, 1, 2, Fe.L);
            }
        }

        // ---------------------------------------------------------------- 후보 1: 역무원 모자 기계 부엉이

        /// <summary>
        /// 쇠 몸통에 구리 깃털 판, 사암색 얼굴 원반, 렌즈 눈, 구리 부리, 역무원 모자를 쓴 기계 부엉이를 그린다.
        /// 표정 _e에 따라 칭찬은 왼쪽 날개를 들고, 주의는 귀깃을 세운다. 현재 캔버스 C에 그린다.
        /// </summary>
        private static void StationOwl()
        {
            Layer();
            if (_e == 1) { Shade(22, 52, 9, 19, Fe, 2.2f, 0.3f); WingLines(14, 42, 26, 64); }
            else { Shade(26, 30, 11, 22, Fe, 2.2f, 0.3f); WingLines(18, 14, 32, 46); }
            Shade(98, 28, 9, 20, Fe, 2.2f, 0.3f);
            EndLayer();

            Layer();
            Shade(62, 14, 38, 34, Fe, 2.2f, 0.3f);
            for (int row = 0; row < 6; row++)
            {
                int y = 40 - row * 8;
                for (int x = 38 + (row % 2) * 5; x <= 88; x += 10)
                {
                    // 몸통 밖으로 삐져나오는 깃털 판은 건너뛴다.
                    if (!Inside(x, y - 4, 62, 14, 35, 31, 2.2f)) continue;
                    Shade(x, y, 5.5f, 4.5f, Cu, 2f, 0.3f);
                    C.Set(x, y + 3, Bs.L);
                }
            }
            EndLayer();

            Layer();
            int tuft = _e == 2 ? 7 : 0;
            Tri(32, 86, 46, 94, 30, 106 + tuft, Fe.B);
            Tri(31, 90, 34, 101 + tuft, 37, 92, Fe.L);
            Tri(80, 94, 94, 86, 92, 104 + tuft, Fe.S);
            Shade(62, 74, 34, 25, Fe, 2.6f, 0.4f);
            Shade(51, 74, 15, 14, Sa, 2f, 0.45f);
            Shade(77, 74, 13, 13, Sa, 2f, 0.45f);
            EndLayer();

            Lens(51, 74, 9); Lens(77, 74, 8);
            Brow(51, 74, 9, true, Bs); Brow(77, 74, 8, false, Bs);

            Layer();
            Tri(60, 66, 70, 66, 67, 56, Cu.B);
            Tri(60, 66, 64, 66, 66, 58, Cu.L);
            if (_e == 2) C.Rect(63, 61, 5, 2, O.S);
            EndLayer();

            Layer();
            Shade(62, 103, 22, 6, R, 2.6f, 0.4f);
            C.Rect(41, 93, 42, 10, R.B);
            C.Rect(41, 93, 1, 10, R.L); C.Rect(82, 93, 1, 10, R.S);
            C.Rect(41, 93, 42, 3, Bs.B); C.Rect(41, 95, 42, 1, Bs.L);
            Gear(66, 100, 3.5f, 6, Bs);
            Shade(76, 92, 19, 3f, Fe, 2f, 0.3f);
            C.Rect(60, 92, 34, 1, Fe.L);
            EndLayer();
        }

        /// <summary>
        /// (x0,y0)~(x1,y1) 범위의 날개에 깃 줄과 황동 날개 끝선을 긋는다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void WingLines(int x0, int y0, int x1, int y1)
        {
            for (int y = y0 + 4; y < y1; y += 6) C.Line(x0, y, x1, y - 3, Fe.S);
            C.Line(x1, y0, x1, y1 - 2, Bs.S);
        }

        // ---------------------------------------------------------------- 후보 2: 랜턴 머리 로봇

        /// <summary>
        /// 쇠 몸통과 천 목도리, 등불 유리 속에 얼굴이 비치는 랜턴 머리 로봇을 그린다.
        /// 표정 _e에 따라 눈·입 모양과 유리 빛(주의는 주황)이 바뀐다. 현재 캔버스 C에 그린다.
        /// </summary>
        private static void LanternBot()
        {
            Layer();
            Shade(62, 4, 38, 28, Fe, 2.4f, 0.3f);
            RivetsV(62, 4, 22, 6, Bs.L);
            EndLayer();

            Layer(); Box(54, 30, 16, 20, Fe); RivetsH(55, 68, 40, 3, Bs.S); EndLayer();

            Layer();
            Shade(62, 32, 22, 6, Fa, 2f, 0.2f);
            Box(70, 12, 8, 20, Fa);
            for (int y = 15; y < 30; y += 5) C.Rect(71, y, 6, 1, Fa.S);
            EndLayer();

            Layer();
            for (int y = 96; y < 104; y++) { int inset = (y - 96) * 2; C.Rect(40 + inset, y, 44 - inset * 2, 1, y > 100 ? Fe.L : Fe.B); }
            for (int y = 104; y < 113; y++) for (int x = 54; x < 70; x++)
            {
                float dx = x + 0.5f - 62, dy = y + 0.5f - 104, d = dx * dx + dy * dy;
                if (d > 25 && d <= 56) C.Set(x, y, dx < 0 ? Bs.L : Bs.S);
            }
            Swatch glass = _e == 2 ? Em : Gl;
            for (int y = 52; y < 92; y++) for (int x = 44; x < 80; x++)
            {
                float t = (x - 44) / 36f;
                C.Set(x, y, t < 0.3f ? glass.L : t < 0.75f ? glass.B : glass.S);
            }
            C.Rect(40, 48, 4, 48, Fe.B); C.Rect(40, 48, 1, 48, Fe.L);
            C.Rect(80, 48, 4, 48, Fe.S);
            C.Rect(40, 92, 44, 4, Fe.B); C.Rect(40, 95, 44, 1, Fe.L);
            C.Rect(40, 48, 44, 4, Fe.S);
            RivetsH(44, 80, 93, 6, Bs.B);
            EndLayer();

            Color32 ink = O.S;
            int[] eyes = { 61, 74 };
            foreach (int ex in eyes)
            {
                if (_e == 1)
                    for (int i = -3; i <= 3; i++) C.Rect(ex + i, 74 + (3 - Mathf.Abs(i)) / 2, 1, 2, ink);
                else if (_e == 2) C.Disc(ex, 74, 2f, ink);
                else C.Ellipse(ex, 74, 2.5f, 4f, ink);
            }
            if (_e == 2)
            {
                C.Line(57, 83, 64, 80, ink, 2); C.Line(71, 80, 78, 83, ink, 2);
                C.Ring(68, 63, 1.5f, 3f, ink);
            }
            else if (_e == 1)
                for (int i = -5; i <= 5; i++) C.Set(68 + i, 64 - (25 - i * i) / 8, ink);
            else C.Rect(64, 63, 8, 1, ink);
            C.Rect(46, 84, 3, 5, A(Sm.L, 170));
        }

        // ---------------------------------------------------------------- 공용 부품

        /// <summary>
        /// (cx,cy) 중심 반지름 r의 렌즈 눈(황동 테, 어두운 유리, 오른쪽을 보는 눈동자)을 그린다.
        /// 표정 _e가 칭찬이면 웃는 호, 주의면 작아진 주황 눈동자로 그린다. 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Lens(float cx, float cy, float r)
        {
            LitRing(cx, cy, r, r + 2f, Bs);
            C.Disc(cx, cy, r, Mix(O.S, Wa.S, 0.45f));
            if (_e == 1)
            {
                int ir = Mathf.FloorToInt(r) - 2;
                for (int i = -ir; i <= ir; i++)
                {
                    float t = i / (float)ir;
                    int y = Mathf.RoundToInt(cy - r * 0.3f + (1f - t * t) * r * 0.6f);
                    C.Rect(Mathf.RoundToInt(cx) + i, y - 1, 1, 2, Gl.L);
                }
                return;
            }
            Swatch g = _e == 2 ? Em : Gl;
            float pr = _e == 2 ? r * 0.32f : r * 0.5f, px = cx + r * 0.28f;
            C.Disc(px, cy, pr + 1f, A(g.S, 200));
            C.Disc(px, cy, pr, g.B);
            C.Disc(px - pr * 0.3f, cy + pr * 0.3f, pr * 0.45f, g.L);
            C.Rect(Mathf.RoundToInt(cx - r * 0.55f), Mathf.RoundToInt(cy + r * 0.3f), 2, 2, A(Sm.L, 210));
        }

        /// <summary>
        /// (ex,ey) 반지름 r 눈 위에 s 색 눈썹 판을 긋는다. left는 왼쪽 눈 여부다.
        /// 표정 _e가 주의면 안쪽이 내려간 사선, 칭찬이면 2칸 올린다. 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Brow(float ex, float ey, float r, bool left, Swatch s)
        {
            int x0 = Mathf.RoundToInt(ex - r + 1), x1 = Mathf.RoundToInt(ex + r - 1), y = Mathf.RoundToInt(ey + r + 4);
            int a = 0, b = 0;
            if (_e == 2) { a = left ? 3 : -1; b = left ? -1 : 3; }
            if (_e == 1) y += 2;
            C.Line(x0, y + a, x1, y + b, s.S, 2);
            C.Line(x0, y + a + 1, x1, y + b + 1, s.L);
        }

        /// <summary>
        /// 표정 효과를 오른쪽 위에 그린다. 칭찬은 반짝임 셋, 주의는 주황 놀람 선 셋이다.
        /// 표정 _e를 쓰고, 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Effects()
        {
            if (_e == 1) { Sparkle(104, 98, 4); Sparkle(112, 84, 3); Sparkle(98, 114, 2); }
            if (_e == 2)
            {
                C.Line(98, 100, 106, 108, Em.B, 2);
                C.Line(102, 90, 112, 92, Em.B, 2);
                C.Line(92, 108, 95, 118, Em.B, 2);
            }
        }

        /// <summary>
        /// (x,y) 중심에 팔 길이 arm의 십자 반짝임을 그린다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Sparkle(int x, int y, int arm)
        {
            C.Rect(x - arm, y, arm * 2 + 1, 1, Gl.B);
            C.Rect(x, y - arm, 1, arm * 2 + 1, Gl.B);
            C.Rect(x - 1, y - 1, 3, 3, Gl.L);
        }

        /// <summary>
        /// (cx,cy) 중심 rx·ry 초버타원(지수 p)을 왼쪽 위 빛의 2~3단 명암 s로 채운다(SpriteGenPortraits와 같은 규칙). rimLight는 밝은 테 문턱값이다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Shade(float cx, float cy, float rx, float ry, Swatch s, float p = 2f, float rimLight = 0.42f)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), p) + Mathf.Pow(Mathf.Abs(dy), p), 1f / p);
                    if (d > 1f) continue;
                    float l = -dx * 0.7f + dy * 0.45f;
                    C.Set(x, y, l > rimLight && d > 0.55f ? s.L : (l < -0.3f || (d > 0.86f && l < 0.05f)) ? s.S : s.B);
                }
        }

        /// <summary>
        /// 픽셀 (x,y)가 (cx,cy) 중심 rx·ry 초버타원(지수 p) 안인지 돌려준다.
        /// </summary>
        private static bool Inside(float x, float y, float cx, float cy, float rx, float ry, float p)
        {
            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
            return Mathf.Pow(Mathf.Abs(dx), p) + Mathf.Pow(Mathf.Abs(dy), p) <= 1f;
        }

        /// <summary>
        /// (cx,cy) 중심 rIn 초과 rOut 이하의 고리를 왼쪽 위 밝음, 오른쪽 아래 그늘의 s 명암으로 채운다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void LitRing(float cx, float cy, float rIn, float rOut, Swatch s)
        {
            for (int y = Mathf.FloorToInt(cy - rOut - 1); y <= Mathf.CeilToInt(cy + rOut + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rOut - 1); x <= Mathf.CeilToInt(cx + rOut + 1); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= rIn || d > rOut) continue;
                    float l = (-dx + dy) / d;
                    C.Set(x, y, l > 0.5f ? s.L : l < -0.5f ? s.S : s.B);
                }
        }

        /// <summary>
        /// 세 꼭짓점 (ax,ay)·(bx,by)·(cx,cy) 삼각형을 c 색으로 채운다.
        /// 현재 캔버스 C에 그린다.
        /// </summary>
        private static void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color32 c)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx))), x1 = Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx)));
            int y0 = Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy))), y1 = Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy)));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
                float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
                float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) C.Set(x, y, c);
            }
        }

        // ---------------------------------------------------------------- 저장

        /// <summary>
        /// 128×128 픽셀 배열 src를 최근접 SCALE배로 늘려 path에 PNG로 저장한다.
        /// </summary>
        private static void SaveScaled(string path, Color32[] src)
        {
            int size = CANVAS * SCALE;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = src[(y / SCALE) * CANVAS + x / SCALE];
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(px); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>
        /// sprites를 perRow칸씩 배경색 위에 늘어놓고 scale배로 키운 콘택트 시트 PNG 바이트를 돌려준다.
        /// </summary>
        private static byte[] SheetPng(List<Color32[]> sprites, int perRow, int scale)
        {
            int cell = CANVAS + 8;
            int rows = Math.Max(1, (sprites.Count + perRow - 1) / perRow);
            int w = cell * perRow * scale, h = cell * rows * scale;
            var fill = new Color32[w * h];
            for (int i = 0; i < fill.Length; i++) fill[i] = Bg.B;
            for (int i = 0; i < sprites.Count; i++)
            {
                int ox = ((i % perRow) * cell + 4) * scale, oy = ((rows - 1 - i / perRow) * cell + 4) * scale;
                for (int y = 0; y < CANVAS * scale; y++) for (int x = 0; x < CANVAS * scale; x++)
                {
                    int k = (oy + y) * w + ox + x;
                    fill[k] = PixelCanvas.Over(sprites[i][(y / scale) * CANVAS + x / scale], fill[k]);
                }
            }
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false);
            sheet.SetPixels32(fill); sheet.Apply();
            byte[] png = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);
            return png;
        }

        /// <summary>
        /// path 텍스처를 초상과 같은 설정(단일 Sprite, Point, 무압축, 밉맵 없음)으로 임포트한다. ppu만 배율에 맞춰 받는다.
        /// 임포터 설정을 바꾸고 다시 임포트한다.
        /// </summary>
        private static void Configure(string path, int ppu)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Not imported: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }
}
