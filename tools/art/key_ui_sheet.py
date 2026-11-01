"""스토리 024. 사용자 스팀펑크 UI 시트의 갈색 배경을 투명으로 바꾸고 조각 rect·9분할 border를 JSON으로 기록한다.

PIL만 사용(numpy 없음). 원본은 건드리지 않는다.
사용: python tools/art/key_ui_sheet.py [--root <repo>] [--out-dir <dir>]

키잉 방식(외곽선 장벽 + 테두리 플러드):
  1. 조각은 거의 검은 외곽선(max(R,G,B) < OUTLINE_MAX)으로 둘러싸여 있다. 배경의 가장 어두운 곳도 max ≈ 31이라 구분된다.
  2. 외곽선 마스크를 1px 팽창(MaxFilter 3)해 안티앨리어싱 틈을 막는다.
  3. 이미지 테두리에서 외곽선이 아닌 픽셀로 플러드 → 닿은 곳 = 배경. 닿지 않은 곳 = 전경(조각 내부 구멍 자동 채움).
  4. 전경을 1px 침식(MinFilter 3)해 팽창분과 갈색 테를 없앤다.
"""
import argparse, json, os
from collections import deque
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUTLINE_MAX = 20        # 이 값보다 어두운(max 채널) 픽셀 = 외곽선 장벽
MIN_PIECE_AREA = 400    # 이보다 작은 연결 성분은 티끌로 버린다

# 9분할 border(시트 픽셀, l/b/r/t)와 내용 여백(pad: 프레임 두께, 글자가 파이프와 겹치지 않는 안쪽 거리).
# border는 모서리 장식(톱니·이음쇠)이 늘어나지 않게 pad보다 클 수 있다.
SPEC = {
    "frame_plain":       {"border": (176, 56, 136, 84),  "pad": (64, 48, 48, 76)},
    "frame_titled":      {"border": (180, 64, 180, 118), "pad": (44, 60, 44, 116)},
    "title_plate":       {"border": (44, 16, 44, 20),    "pad": (40, 12, 40, 18)},
    "progress_bar":      {"border": (112, 24, 112, 24),  "pad": (112, 16, 112, 20)},
    "btn_dark_arrows":   {"border": (44, 16, 44, 16),    "pad": (42, 14, 42, 12)},
    "btn_dark":          {"border": (32, 18, 32, 18),    "pad": (28, 13, 26, 13)},
    "btn_copper":        {"border": (24, 24, 24, 24),    "pad": (28, 24, 28, 24)},
    "btn_gold":          {"border": (24, 24, 24, 24),    "pad": (28, 24, 28, 24)},
    "slot_dark":         {"border": (24, 24, 24, 24),    "pad": (13, 12, 12, 12)},
    "slot_copper":       {"border": (24, 24, 24, 24),    "pad": (13, 12, 12, 12)},
    "slot_gold":         {"border": (24, 24, 24, 24),    "pad": (13, 12, 12, 12)},
}

def key(im):
    W, H = im.size
    r, g, b = im.split()
    mx = ImageChops.lighter(ImageChops.lighter(r, g), b)
    m = mx.point(lambda v: 255 if v < OUTLINE_MAX else 0).filter(ImageFilter.MaxFilter(3))
    seeds = [(x, y) for x in range(W) for y in (0, H - 1)] + [(x, y) for y in range(H) for x in (0, W - 1)]
    for p in seeds:
        if m.getpixel(p) == 0:
            ImageDraw.floodfill(m, p, 128)
    fg = m.point(lambda v: 0 if v == 128 else 255).filter(ImageFilter.MinFilter(3))
    out = im.convert("RGBA"); out.putalpha(fg)
    return out

def components(alpha, W, H):
    a = alpha.tobytes(); seen = bytearray(W * H); comps = []
    for i in range(W * H):
        if not a[i] or seen[i]:
            continue
        q = deque([i]); seen[i] = 1; n = 0; x0 = y0 = 1 << 30; x1 = y1 = -1
        while q:
            j = q.popleft(); n += 1; x, y = j % W, j // W
            x0 = min(x0, x); x1 = max(x1, x); y0 = min(y0, y); y1 = max(y1, y)
            for k in ((j - 1) if x > 0 else -1, (j + 1) if x < W - 1 else -1, j - W, j + W):
                if 0 <= k < W * H and a[k] and not seen[k]:
                    seen[k] = 1; q.append(k)
        if n >= MIN_PIECE_AREA:
            comps.append({"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1, "area": n})
    return comps

def name_pieces(comps, H):
    """위치·크기 규칙(시트 위쪽 기준 y). 애매하면 JSON을 손으로 고친다."""
    named = {}
    row1 = sorted([c for c in comps if c["y"] < 520], key=lambda c: c["x"])
    row3 = sorted([c for c in comps if c["y"] >= 860], key=lambda c: c["x"])
    row2 = [c for c in comps if 520 <= c["y"] < 860]
    for n, c in zip(["frame_plain", "frame_titled"], row1): named[n] = c
    for n, c in zip(["btn_dark_arrows", "btn_dark", "btn_copper", "btn_gold", "slot_dark", "slot_copper", "slot_gold"], row3): named[n] = c
    rounds = sorted([c for c in row2 if abs(c["w"] - c["h"]) <= 4 and c["w"] < 130], key=lambda c: (c["y"] > 700, c["x"]))
    for n, c in zip(["btn_round_close_top", "btn_round_gear", "btn_round_alert", "btn_round_help", "btn_round_close"], rounds): named[n] = c
    for c in row2:
        if c in rounds: continue
        cx = c["x"] + c["w"] / 2
        if c["w"] > 400: named["title_plate" if c["h"] > 80 else "progress_bar"] = c
        elif cx < 300: named["corner_a" if c["y"] < 700 else "pipe_corner_small"] = c
        elif cx < 560: named["corner_b"] = c
        else: named["pipe_corner_gear"] = c
    if len(named) != len(comps):
        raise SystemExit(f"이름 규칙 불일치: 성분 {len(comps)}개, 이름 {len(named)}개")
    out = []
    for n, c in named.items():
        spec = SPEC.get(n, {"border": (0, 0, 0, 0), "pad": (0, 0, 0, 0)})
        l, b, r, t = spec["border"]; pl, pb, pr, pt = spec["pad"]
        out.append({"name": n, "x": c["x"], "y": H - c["y"] - c["h"], "w": c["w"], "h": c["h"],
                    "border": {"l": l, "b": b, "r": r, "t": t}, "pad": {"l": pl, "b": pb, "r": pr, "t": pt}})
    return sorted(out, key=lambda e: (-e["y"], e["x"]))

def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=os.path.normpath(os.path.join(here, "..", "..")))
    ap.add_argument("--out-dir", default=None, help="keyed png·json·콘택트 시트 출력 폴더(기본: 저장소 경로)")
    a = ap.parse_args()
    src = os.path.join(a.root, "Assets/Art/UI/SteampunkUiSheet.png")
    keyed_path = os.path.join(a.out_dir or os.path.join(a.root, "Assets/Art/UI"), "SteampunkUiSheet_keyed.png")
    json_path = os.path.join(a.out_dir or os.path.join(a.root, "tools/art"), "ui_sheet_rects.json")
    ev_dir = a.out_dir or os.path.join(a.root, "production/qa/evidence/story-024-ui-skin-sprite-sheet")
    os.makedirs(ev_dir, exist_ok=True)
    im = Image.open(src).convert("RGB"); W, H = im.size
    keyed = key(im); keyed.save(keyed_path)
    mag = Image.new("RGBA", (W, H), (255, 0, 255, 255)); mag.alpha_composite(keyed)
    mag.convert("RGB").save(os.path.join(ev_dir, "keyed_contact.png"))
    pieces = name_pieces(components(keyed.getchannel("A"), W, H), H)
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(pieces, f, ensure_ascii=False, indent=2)
    print(f"{'name':22}{'x':>6}{'y':>6}{'w':>6}{'h':>6}   border l/b/r/t     pad l/b/r/t")
    for e in pieces:
        bd, pd = e["border"], e["pad"]
        print(f"{e['name']:22}{e['x']:>6}{e['y']:>6}{e['w']:>6}{e['h']:>6}   {bd['l']}/{bd['b']}/{bd['r']}/{bd['t']:<10} {pd['l']}/{pd['b']}/{pd['r']}/{pd['t']}")
    print(f"keyed={keyed_path}\njson={json_path}")

if __name__ == "__main__":
    main()
