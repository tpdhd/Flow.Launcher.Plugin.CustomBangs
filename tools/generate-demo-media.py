"""Generate the three README demo GIFs as stylized mockups of Flow Launcher.

These are illustrations, not screen recordings: this repository has no Windows
environment to capture the real app from. The layout intentionally mirrors
Flow Launcher's actual look (a single floating search box, plain dark
surface, one result row) so the mockups read as plausible screenshots rather
than a marketing graphic.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "media"
LOGO_PATH = ROOT / "src" / "Flow.Launcher.Plugin.CustomBangs" / "Images" / "app.png"
SIZE = (760, 380)
OUTPUT_SIZE = (640, 320)
FPS_MS = 90

DESKTOP = (30, 30, 32)
PANEL = (43, 43, 47)
PANEL_BORDER = (60, 60, 65)
ROW = (53, 53, 58)
LINE = (70, 70, 76)
TEXT = (235, 235, 238)
MUTED = (150, 150, 158)
ACCENT = (0, 120, 215)

FONT_CANDIDATES = [
    "C:/Windows/Fonts/segoeui.ttf",
    "C:/Windows/Fonts/seguisb.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
]
BOLD_CANDIDATES = [
    "C:/Windows/Fonts/seguisb.ttf",
    "C:/Windows/Fonts/segoeuib.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
]


def _first_existing(paths: list[str]) -> str:
    for path in paths:
        if Path(path).exists():
            return path
    raise FileNotFoundError(f"none of the candidate fonts exist: {paths}")


REGULAR_FONT = _first_existing(FONT_CANDIDATES)
BOLD_FONT = _first_existing(BOLD_CANDIDATES)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(BOLD_FONT if bold else REGULAR_FONT, size)


F14 = font(14)
F17 = font(17)
F20 = font(20)

YOUTUBE = (235, 87, 87)
AMAZON = (242, 158, 76)
EBAY = (86, 179, 214)
ALIEXPRESS = (214, 93, 160)

TAB_BAR_BG = (24, 24, 27)


def rounded(draw: ImageDraw.ImageDraw, box, radius=10, fill=None, outline=None, width=1, corners=None):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width, corners=corners)


def logo(size=28):
    icon = Image.open(LOGO_PATH).convert("RGBA")
    icon.thumbnail((size, size), Image.Resampling.LANCZOS)
    return icon


def desktop() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGB", SIZE, DESKTOP)
    draw = ImageDraw.Draw(img)
    return img, draw


def launcher_frame(query: str, result: tuple[str, str] | None, hint: str = "Enter"):
    """A single floating search box, matching Flow Launcher's own layout."""
    img, draw = desktop()
    box = (110, 60, 650, 118) if result is None else (110, 60, 650, 160)
    rounded(draw, box, 12, PANEL, PANEL_BORDER, 1)
    icon = logo(24)
    img.paste(icon, (128, 74), icon)
    draw.text((166, 78), query, font=F20, fill=TEXT if query else MUTED)
    if result:
        draw.line((126, 118, 634, 118), fill=LINE, width=1)
        title, subtitle = result
        rounded(draw, (122, 126, 638, 154), 8, ROW)
        row_icon = logo(20)
        img.paste(row_icon, (134, 131), row_icon)
        draw.text((164, 128), title, font=F17, fill=TEXT)
        draw.text((164, 145), subtitle, font=F14, fill=MUTED)
        draw.text((586, 133), hint, font=F14, fill=ACCENT)
    return img


def type_sequence(full_text: str, result_title: str, result_subtitle: str, hold=10):
    frames = []
    for i in range(len(full_text) + 1):
        query = full_text[:i]
        show_result = i >= 2
        frame = launcher_frame(
            query,
            (result_title, result_subtitle) if show_result else None,
        )
        frames.append(frame)
    frames.extend([frames[-1].copy() for _ in range(hold)])
    return frames


def opened_tabs(sites: list[tuple[str, tuple[int, int, int]]]):
    """A peek at a browser's tab strip, one tab per site the search opened in.

    Anchored at the same top position as the launcher's query line, so
    there's no vertical jump when a demo cuts from typing to this frame.
    The bar's background runs past the right edge of the canvas and only
    the top-left corner is rounded, so it reads as a cropped corner of a
    much wider browser window rather than a standalone floating widget.
    """
    img, draw = desktop()
    tab_w, tab_h, pad = 172, 40, 6
    bar_h = tab_h + 2 * pad
    x0 = 70
    top = 60
    bar_right = SIZE[0] + 60

    draw.text((x0, top - 24), "Opened in your default browser", font=F14, fill=MUTED)
    rounded(draw, (x0, top, bar_right, top + bar_h), 14, TAB_BAR_BG, PANEL_BORDER, 1, corners=(True, False, False, False))

    for i, (label, color) in enumerate(sites):
        tx = x0 + pad + i * (tab_w + pad)
        rounded(draw, (tx, top + pad, tx + tab_w, top + bar_h), 10, PANEL, corners=(True, True, False, False))
        cy = top + pad + tab_h // 2
        draw.ellipse((tx + 14, cy - 5, tx + 24, cy + 5), fill=color)
        draw.text((tx + 32, cy - 9), label, font=F14, fill=TEXT)
    return img


def settings_frame(command="", members="", saved=False):
    """A plain settings panel, matching Flow's own settings-page chrome."""
    img, draw = desktop()
    rounded(draw, (90, 40, 670, 340), 12, PANEL, PANEL_BORDER, 1)
    draw.text((114, 58), "Custom Bangs \u2014 Settings", font=F17, fill=TEXT)
    draw.line((114, 92, 646, 92), fill=LINE, width=1)

    draw.text((114, 112), "Group command", font=F14, fill=MUTED)
    rounded(draw, (114, 134, 646, 172), 8, ROW, LINE, 1)
    draw.text((130, 144), command, font=F17, fill=TEXT if command else MUTED)

    draw.text((114, 190), "Member shortcuts", font=F14, fill=MUTED)
    rounded(draw, (114, 212, 646, 250), 8, ROW, LINE, 1)
    draw.text((130, 222), members, font=F17, fill=TEXT if members else MUTED)
    draw.text((114, 258), "Comma-separated names of existing shortcuts.", font=F14, fill=MUTED)

    rounded(draw, (556, 294, 646, 326), 7, (60, 130, 90) if saved else ACCENT)
    draw.text((582 if saved else 588, 302), "Saved" if saved else "Save", font=F14, fill=(255, 255, 255))
    return img


def import_export_frame(mode="Import", progress=0.0):
    img, draw = desktop()
    rounded(draw, (90, 40, 670, 300), 12, PANEL, PANEL_BORDER, 1)
    draw.text((114, 58), "Custom Bangs \u2014 Settings", font=F17, fill=TEXT)
    draw.line((114, 92, 646, 92), fill=LINE, width=1)

    left_active = mode == "Import"
    rounded(draw, (114, 112, 370, 150), 8, PANEL if left_active else DESKTOP, ACCENT if left_active else LINE, 1)
    rounded(draw, (390, 112, 646, 150), 8, PANEL if not left_active else DESKTOP, ACCENT if not left_active else LINE, 1)
    draw.text((176, 122), "Import", font=F17, fill=TEXT)
    draw.text((466, 122), "Export", font=F17, fill=TEXT)

    draw.text((114, 172), "Custom Bang Search format \u00b7 version 6", font=F14, fill=MUTED)
    rounded(draw, (114, 200, 646, 216), 8, ROW)
    if progress:
        rounded(draw, (114, 200, 114 + int(532 * progress), 216), 8, (60, 130, 90))
    if progress >= 1:
        status = "234 bangs imported" if mode == "Import" else "234 bangs exported"
    else:
        status = "Choose a version 6 JSON file" if mode == "Import" else "Choose where to save the file"
    draw.text((114, 228), status, font=F14, fill=(150, 210, 175) if progress >= 1 else MUTED)
    return img


def save(frames, filename):
    OUT.mkdir(parents=True, exist_ok=True)
    resized = [frame.resize(OUTPUT_SIZE, Image.Resampling.LANCZOS) for frame in frames]
    paletted = [
        frame.convert("P", palette=Image.Palette.ADAPTIVE, colors=64, dither=Image.Dither.NONE)
        for frame in resized
    ]
    paletted[0].save(
        OUT / filename,
        save_all=True,
        append_images=paletted[1:],
        duration=FPS_MS,
        loop=0,
        optimize=True,
        disposal=2,
    )


def single_bang_demo():
    frames = type_sequence("yt guitar tutorial", "Search YouTube for \u201cguitar tutorial\u201d", "Custom bang \u00b7 no activator required", hold=14)
    opened = opened_tabs([("YouTube", YOUTUBE)])
    frames.extend([opened.copy() for _ in range(16)])
    save(frames, "demo-single-bang.gif")


def group_demo():
    command = "shop1"
    members = "asd, ede, ali"
    frames = [settings_frame("", "") for _ in range(6)]
    for i in range(1, len(command) + 1):
        frames.append(settings_frame(command[:i], ""))
    for i in range(1, len(members) + 1):
        frames.append(settings_frame(command, members[:i]))
    frames.extend([settings_frame(command, members, saved=True) for _ in range(10)])
    frames.extend(type_sequence("shop1 headphones", "Search 3 shortcuts for \u201cheadphones\u201d", "Search group \u00b7 asd, ede, ali", hold=12))
    opened = opened_tabs([("Amazon", AMAZON), ("eBay", EBAY), ("AliExpress", ALIEXPRESS)])
    frames.extend([opened.copy() for _ in range(18)])
    save(frames, "demo-search-groups.gif")


def portable_demo():
    frames = [import_export_frame("Import", 0.0) for _ in range(10)]
    for step in range(1, 11):
        frames.append(import_export_frame("Import", step / 10))
    frames.extend([import_export_frame("Import", 1.0) for _ in range(14)])
    for step in range(0, 11):
        frames.append(import_export_frame("Export", step / 10))
    frames.extend([import_export_frame("Export", 1.0) for _ in range(14)])
    save(frames, "demo-import-export.gif")


if __name__ == "__main__":
    single_bang_demo()
    group_demo()
    portable_demo()
    for path in sorted(OUT.glob("*.gif")):
        print(f"{path.name}: {path.stat().st_size:,} bytes")
