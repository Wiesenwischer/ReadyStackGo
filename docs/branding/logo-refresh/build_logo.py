"""Master source of the ReadyStackGo logo (three cubes + wordmark).

Generates every SVG logo asset from one geometry and one font, so all exports match.
Reference: referenz/ReadyStackGo_Neues_Logo.jpeg (selected by Marcus, 2026-10-05).

Requirements: Python 3, fonttools, brotli (to read the woff2 font).
Font: Figtree (SIL Open Font License 1.1, font/OFL.txt), converted to outlines; no font is
loaded at runtime.
Run from the repository root:

    python docs/branding/logo-refresh/build_logo.py

PNG exports (favicon.png, apple-touch-icon.png) are rendered from the generated SVGs by
render_png.mjs in this folder.
"""
from __future__ import annotations

import math
import os
import sys

from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "font", "figtree-latin-wght-normal.woff2")

# --- Colors (docs/branding/logo-refresh/README.md) -------------------------------------------
READY = "#00CED1"        # brand turquoise, "Ready"
GO = "#FF6B35"           # brand orange, "Go"
STACK_ON_DARK = "#FFFFFF"
STACK_ON_LIGHT = "#0E171A"  # graphite, only for "Stack" on light surfaces
TILE = "#091012"         # app icon tile (as before)

CUBES = {
    "orange": ("#FF7B43", "#FF6B35", "#D84B16"),     # top, left, right
    "turquoise": ("#0BDCDD", "#00CED1", "#00A3A5"),
    "white": ("#FFFFFF", "#E4E8E9", "#C7CCCF"),
}

# --- Mark geometry, unit = cube edge E (measured from the reference) ----------------------------
E = 1.0
W = math.sqrt(3) * E   # cube width
DY = 4 / 3 * E         # top vertex of the lower cubes below the top vertex of the orange cube
GAP = 0.071 * E        # joint between the cubes (transparent)
GAP_SMALL = 0.16 * E   # wider joint for 16-32 px


def cube_faces(x: float, y: float) -> dict[str, list[tuple[float, float]]]:
    """Faces of an isometric cube whose top vertex is at (x, y)."""
    top = [(x, y), (x + W / 2, y + E / 2), (x, y + E), (x - W / 2, y + E / 2)]
    left = [(x - W / 2, y + E / 2), (x, y + E), (x, y + 2 * E), (x - W / 2, y + 1.5 * E)]
    right = [(x, y + E), (x + W / 2, y + E / 2), (x + W / 2, y + 1.5 * E), (x, y + 2 * E)]
    return {"top": top, "left": left, "right": right}


def silhouette(x: float, y: float) -> list[tuple[float, float]]:
    return [(x, y), (x + W / 2, y + E / 2), (x + W / 2, y + 1.5 * E), (x, y + 2 * E),
            (x - W / 2, y + 1.5 * E), (x - W / 2, y + E / 2)]


def pts(points, s: float, ox: float, oy: float) -> str:
    def fmt(v: float) -> str:
        return f"{round(v, 2) + 0.0:.2f}"  # + 0.0 turns -0.0 into 0.0

    return " ".join(f"{fmt(ox + px * s)},{fmt(oy + py * s)}" for px, py in points)


def mark_svg_parts(s: float, ox: float, oy: float, gap: float, mask_id: str) -> tuple[str, str, float, float]:
    """Returns (defs, body, width, height) of the mark scaled by s with its top-left at (ox, oy)."""
    half = (W + gap) / 2
    cx = W / 2 + half                        # x of the orange top vertex; left edge of the mark at 0
    cubes = [("orange", cx, 0.0), ("turquoise", cx - half, DY), ("white", cx + half, DY)]
    width, height = (2 * half + W) * s, (DY + 2 * E) * s

    # The lower cubes sit in front of the orange one; a mask cuts the joint out of the orange cube.
    cut = "".join(
        f'<polygon points="{pts(silhouette(x, y), s, ox, oy)}" fill="#000" stroke="#000" '
        f'stroke-width="{2 * gap * s:.2f}" stroke-linejoin="miter"/>'
        for name, x, y in cubes[1:]
    )
    defs = (f'<mask id="{mask_id}" maskUnits="userSpaceOnUse" x="{ox - 1:.2f}" y="{oy - 1:.2f}" '
            f'width="{width + 2:.2f}" height="{height + 2:.2f}">'
            f'<rect x="{ox - 1:.2f}" y="{oy - 1:.2f}" width="{width + 2:.2f}" height="{height + 2:.2f}" fill="#fff"/>'
            f'{cut}</mask>')
    body = []
    for name, x, y in cubes:
        top, left, right = CUBES[name]
        faces = cube_faces(x, y)
        group = "".join(
            f'<polygon points="{pts(faces[f], s, ox, oy)}" fill="{c}"/>'
            for f, c in (("top", top), ("left", left), ("right", right))
        )
        body.append(f'<g mask="url(#{mask_id})">{group}</g>' if name == "orange" else group)
    return defs, "".join(body), width, height


# --- Wordmark -----------------------------------------------------------------------------------
_font_cache: dict[int, TTFont] = {}


def load_font(weight: int) -> TTFont:
    if weight not in _font_cache:
        font = TTFont(FONT)
        _font_cache[weight] = instantiateVariableFont(font, {"wght": weight})
    return _font_cache[weight]


def text_paths(text: str, weight: int, size: float, x: float, baseline: float, tracking: float = 0.0) -> tuple[str, float]:
    """Path data for text in Montserrat at the given size; returns (d, advance width)."""
    font = load_font(weight)
    upem = font["head"].unitsPerEm
    cmap = font.getBestCmap()
    glyphs = font.getGlyphSet()
    hmtx = font["hmtx"]
    scale = size / upem
    pen_x = x
    parts = []
    for ch in text:
        name = cmap[ord(ch)]
        svg_pen = SVGPathPen(glyphs)
        glyphs[name].draw(TransformPen(svg_pen, (scale, 0, 0, -scale, pen_x, baseline)))
        parts.append(svg_pen.getCommands())
        pen_x += hmtx[name][0] * scale + tracking * size
    return " ".join(parts), pen_x - x - tracking * size


def font_metrics(weight: int) -> tuple[int, int, int]:
    font = load_font(weight)
    os2 = font["OS/2"]
    return font["head"].unitsPerEm, os2.sCapHeight, os2.sTypoDescender


# Wordmark layout measured from the reference (in mark heights): the word starts 0.19 mark heights
# right of the mark; cap height 0.419 mark heights; baseline 0.742 mark heights below the mark top.
WEIGHT = 760
CAP_RATIO = 0.419
BASELINE_RATIO = 0.742
GAP_RATIO = 0.19
TRACKING = -0.055


def lockup_svg(stack_color: str, mark_height: float = 64.0) -> str:
    s = mark_height / (DY + 2 * E)
    defs, mark, mw, mh = mark_svg_parts(s, 0, 0, GAP, "rsgo-joint")
    upem, cap, _ = font_metrics(WEIGHT)
    size = CAP_RATIO * mark_height * upem / cap
    baseline = BASELINE_RATIO * mark_height
    x = mw + GAP_RATIO * mark_height
    words = []
    for text, color in (("Ready", READY), ("Stack", stack_color), ("Go", GO)):
        d, adv = text_paths(text, WEIGHT, size, x, baseline, TRACKING)
        words.append(f'<path d="{d}" fill="{color}"/>')
        x += adv + TRACKING * size
    # No tracking after the last letter.
    width = math.ceil((x - TRACKING * size) * 100) / 100
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:.2f} {mh:.2f}" '
            f'role="img" aria-label="ReadyStackGo"><title>ReadyStackGo</title><defs>{defs}</defs>{mark}{"".join(words)}</svg>\n')


def mark_svg(gap: float, size: float = 64.0, tile: bool = False) -> str:
    """Square mark, centered; with tile=True on the dark rounded app icon tile."""
    if tile:
        inner = size * 0.68
        s = inner / (DY + 2 * E)
        defs, body, w, h = mark_svg_parts(s, 0, 0, gap, "rsgo-joint")
        ox, oy = (size - w) / 2, (size - h) / 2
        defs, body, _, _ = mark_svg_parts(s, ox, oy, gap, "rsgo-joint")
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size:.0f} {size:.0f}" role="img" '
                f'aria-label="ReadyStackGo"><title>ReadyStackGo</title><defs>{defs}</defs>'
                f'<rect width="{size:.0f}" height="{size:.0f}" rx="{size * 0.22:.2f}" fill="{TILE}"/>{body}</svg>\n')
    s = size / ((W + gap) + W)  # width is the long side
    _, _, w, h = mark_svg_parts(s, 0, 0, gap, "rsgo-joint")
    ox, oy = (size - w) / 2, (size - h) / 2
    defs, body, _, _ = mark_svg_parts(s, ox, oy, gap, "rsgo-joint")
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size:.0f} {size:.0f}" role="img" '
            f'aria-label="ReadyStackGo"><title>ReadyStackGo</title><defs>{defs}</defs>{body}</svg>\n')


def artwork_ts() -> str:
    """TypeScript module with the same geometry for the WebUi Logo component (inline SVG, theme-aware)."""
    mark_height = 64.0
    s = mark_height / (DY + 2 * E)

    def mark(gap: float) -> str:
        half = (W + gap) / 2
        cx = W / 2 + half
        cubes = [("orange", cx, 0.0), ("turquoise", cx - half, DY), ("white", cx + half, DY)]
        lines = []
        for name, x, y in cubes:
            faces = cube_faces(x, y)
            colors = dict(zip(("top", "left", "right"), CUBES[name]))
            face_ts = ", ".join(f'{{ points: "{pts(faces[f], s, 0, 0)}", fill: "{colors[f]}" }}' for f in ("top", "left", "right"))
            lines.append(f'    {{ masked: {"true" if name == "orange" else "false"}, faces: [{face_ts}] }},')
        joints = ", ".join(f'"{pts(silhouette(x, y), s, 0, 0)}"' for _, x, y in cubes[1:])
        width = (2 * half + W) * s
        return (f"{{\n  width: {width:.2f},\n  height: {mark_height:.2f},\n  jointWidth: {2 * gap * s:.2f},\n"
                f"  joints: [{joints}],\n  cubes: [\n" + "\n".join(lines) + "\n  ],\n}")

    _, _, mw, _ = mark_svg_parts(s, 0, 0, GAP, "x")
    upem, cap, _ = font_metrics(WEIGHT)
    size = CAP_RATIO * mark_height * upem / cap
    baseline = BASELINE_RATIO * mark_height
    x = mw + GAP_RATIO * mark_height
    paths = {}
    for key, text in (("ready", "Ready"), ("stack", "Stack"), ("go", "Go")):
        d, adv = text_paths(text, WEIGHT, size, x, baseline, TRACKING)
        paths[key] = d
        x += adv + TRACKING * size
    width = math.ceil((x - TRACKING * size) * 100) / 100
    return (
        "// Generated by docs/branding/logo-refresh/build_logo.py - do not edit by hand.\n"
        "// Geometry of the ReadyStackGo mark (three cubes) and the wordmark outlines (Figtree, SIL OFL 1.1).\n\n"
        "export type LogoCube = { masked: boolean; faces: { points: string; fill: string }[] };\n"
        "export type LogoMarkArtwork = { width: number; height: number; jointWidth: number; joints: string[]; cubes: LogoCube[] };\n\n"
        f"export const LOGO_MARK: LogoMarkArtwork = {mark(GAP)};\n\n"
        f"/** Wider joints for 16-32 px. */\nexport const LOGO_MARK_SMALL: LogoMarkArtwork = {mark(GAP_SMALL)};\n\n"
        "/** Wordmark in the coordinate system of LOGO_MARK (mark at x 0, height 64). */\n"
        f"export const LOGO_WORDMARK = {{\n  width: {width:.2f},\n  height: {mark_height:.2f},\n"
        f'  colors: {{ ready: "{READY}", go: "{GO}" }},\n'
        f'  ready: "{paths["ready"]}",\n  stack: "{paths["stack"]}",\n  go: "{paths["go"]}",\n}};\n'
    )


TARGETS = {
    "webui": os.path.join(ROOT, "src", "ReadyStackGo.WebUi", "apps", "rsgo-generic", "public"),
    "web": os.path.join(ROOT, "src", "ReadyStackGo.PublicWeb", "public"),
}


def write(path: str, content: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)
    print("wrote", os.path.relpath(path, ROOT))


def main() -> None:
    if not os.path.exists(FONT):
        sys.exit(f"Font not found: {FONT}")
    files = {
        "images/logo/readystackgo-mark.svg": mark_svg(GAP),
        "images/logo/readystackgo-mark-small.svg": mark_svg(GAP_SMALL),
        "images/logo/readystackgo-lockup-dark.svg": lockup_svg(STACK_ON_DARK),
        "images/logo/readystackgo-lockup-light.svg": lockup_svg(STACK_ON_LIGHT),
        "images/logo/readystackgo-app-icon.svg": mark_svg(GAP, tile=True),
        "favicon.svg": mark_svg(GAP_SMALL),
    }
    for target in TARGETS.values():
        for rel, content in files.items():
            write(os.path.join(target, *rel.split("/")), content)
    # Starlight (docs) logo
    write(os.path.join(ROOT, "src", "ReadyStackGo.PublicWeb", "src", "assets", "readystackgo-mark.svg"), files["images/logo/readystackgo-mark.svg"])
    # WebUi Logo component
    write(os.path.join(ROOT, "src", "ReadyStackGo.WebUi", "packages", "ui-generic", "src", "components", "brand",
                       "logoArtwork.ts"), artwork_ts())
    # Reference copies next to this script, for review
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets")
    for rel, content in files.items():
        write(os.path.join(out, os.path.basename(rel)), content)


if __name__ == "__main__":
    main()
