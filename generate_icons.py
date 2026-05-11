#!/usr/bin/env python3
"""
Generate QR-style application icons for QrApp.
Creates Assets/icon.ico (256x256, 64x64, 32x32, 16x16)
and Assets/tray-icon.ico (32x32, 16x16).
"""

from PIL import Image, ImageDraw
import os

ASSETS_DIR = "/home/user/QrApp/src/QrApp/Assets"
os.makedirs(ASSETS_DIR, exist_ok=True)


def draw_qr_glyph(size: int, bg=(255, 255, 255, 255), fg=(0, 0, 0, 255)) -> Image.Image:
    """
    Draw a simplified QR-code style glyph at the given size.
    Features:
    - Three corner finder patterns (squares within squares)
    - A few data dots in the middle area
    - Clean, recognizable at small sizes
    """
    img = Image.new("RGBA", (size, size), bg)
    draw = ImageDraw.Draw(img)

    # Helper: draw a finder pattern (outer square + inner filled square)
    # x, y = top-left corner; sz = outer size
    def draw_finder(x, y, sz):
        border = max(1, sz // 7)
        # Outer square border
        draw.rectangle([x, y, x + sz - 1, y + sz - 1], fill=fg)
        # White ring
        inner_x = x + border
        inner_y = y + border
        inner_sz = sz - 2 * border
        draw.rectangle([inner_x, inner_y, inner_x + inner_sz - 1, inner_y + inner_sz - 1], fill=bg)
        # Inner filled square
        inner2_x = x + 2 * border
        inner2_y = y + 2 * border
        inner2_sz = sz - 4 * border
        if inner2_sz > 0:
            draw.rectangle([inner2_x, inner2_y, inner2_x + inner2_sz - 1, inner2_y + inner2_sz - 1], fill=fg)

    # Scale the finder patterns and dots relative to image size
    # Finder patterns occupy roughly 7/25 of the image each
    finder_sz = max(3, round(size * 7 / 25))
    margin = max(1, round(size * 1.5 / 25))

    # Top-left finder
    draw_finder(margin, margin, finder_sz)
    # Top-right finder
    draw_finder(size - margin - finder_sz, margin, finder_sz)
    # Bottom-left finder
    draw_finder(margin, size - margin - finder_sz, finder_sz)

    # Alignment pattern (bottom-right area) — only at sizes >= 32
    if size >= 32:
        align_sz = max(2, round(size * 3 / 25))
        align_x = size - margin - finder_sz // 2 - align_sz // 2
        align_y = size - margin - finder_sz // 2 - align_sz // 2
        draw.rectangle([align_x, align_y, align_x + align_sz - 1, align_y + align_sz - 1], fill=fg)
        if align_sz >= 4:
            inner_a = align_sz // 4
            draw.rectangle([align_x + inner_a, align_y + inner_a,
                            align_x + align_sz - inner_a - 1,
                            align_y + align_sz - inner_a - 1], fill=bg)
            if align_sz >= 8:
                mid_a = align_sz // 8
                draw.rectangle([align_x + align_sz // 2 - mid_a, align_y + align_sz // 2 - mid_a,
                                align_x + align_sz // 2 + mid_a, align_y + align_sz // 2 + mid_a], fill=fg)

    # Timing patterns (dotted lines between finder patterns) — only at >= 32
    if size >= 32:
        timing_start = margin + finder_sz + max(1, size // 25)
        timing_end = size - margin - finder_sz - max(1, size // 25)
        dot_sz = max(1, round(size / 25))
        timing_y = margin + finder_sz // 2 - dot_sz // 2
        timing_x = margin + finder_sz // 2 - dot_sz // 2
        pos = timing_start
        on = True
        while pos < timing_end:
            if on:
                draw.rectangle([pos, timing_y, pos + dot_sz - 1, timing_y + dot_sz - 1], fill=fg)
                draw.rectangle([timing_x, pos, timing_x + dot_sz - 1, pos + dot_sz - 1], fill=fg)
            pos += dot_sz
            on = not on

    # Data dots in the middle region — simulate data area
    if size >= 16:
        data_margin = margin + finder_sz + max(2, size // 15)
        data_end = size - data_margin
        dot = max(1, round(size / 25))
        gap = dot + max(1, dot // 2)

        # A simple pattern of dots that looks like QR data
        pattern = [
            [1, 0, 1, 1, 0, 1, 0, 1],
            [0, 1, 1, 0, 1, 0, 1, 0],
            [1, 1, 0, 1, 0, 1, 1, 0],
            [0, 0, 1, 0, 1, 1, 0, 1],
            [1, 0, 0, 1, 1, 0, 1, 1],
            [0, 1, 1, 1, 0, 1, 0, 0],
            [1, 0, 1, 0, 1, 0, 1, 0],
            [0, 1, 0, 1, 0, 1, 0, 1],
        ]

        data_w = data_end - data_margin
        col_count = min(len(pattern[0]), max(1, data_w // gap))
        row_count = min(len(pattern), max(1, data_w // gap))

        for row in range(row_count):
            for col in range(col_count):
                if pattern[row % len(pattern)][col % len(pattern[0])]:
                    px = data_margin + col * gap
                    py = data_margin + row * gap
                    if px + dot <= data_end and py + dot <= data_end:
                        draw.rectangle([px, py, px + dot - 1, py + dot - 1], fill=fg)

    return img


def create_icon_ico():
    """Create main application icon with multiple sizes."""
    sizes = [256, 64, 32, 16]
    images = []
    for sz in sizes:
        img = draw_qr_glyph(sz)
        images.append(img)

    out_path = os.path.join(ASSETS_DIR, "icon.ico")
    # Save as ICO with all sizes embedded
    images[0].save(
        out_path,
        format="ICO",
        sizes=[(sz, sz) for sz in sizes],
        append_images=images[1:]
    )
    print(f"Created {out_path} ({os.path.getsize(out_path)} bytes)")


def draw_tray_glyph(size: int) -> Image.Image:
    """
    Draw a minimal QR glyph optimized for small sizes.
    Uses RGBA so it can adapt to dark/light backgrounds via OS compositing.
    Black on white (standard — Windows tray handles inversion for dark mode).
    """
    img = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    draw = ImageDraw.Draw(img)

    fg = (0, 0, 0, 255)
    bg = (255, 255, 255, 255)

    if size <= 16:
        # At 16px: draw just the three corner squares cleanly
        # Each finder occupies 5px out of 16px
        finder_sz = 5
        margin = 0

        def draw_finder_small(x, y, sz):
            draw.rectangle([x, y, x + sz - 1, y + sz - 1], fill=fg)
            draw.rectangle([x + 1, y + 1, x + sz - 2, y + sz - 2], fill=bg)
            draw.rectangle([x + 2, y + 2, x + sz - 3, y + sz - 3], fill=fg)

        draw_finder_small(margin, margin, finder_sz)
        draw_finder_small(size - margin - finder_sz, margin, finder_sz)
        draw_finder_small(margin, size - margin - finder_sz, finder_sz)

        # A few dots in the center right area
        # Bottom-right 3x3 block
        br_x = size - finder_sz - 1
        br_y = size - finder_sz - 1
        # Single dot cluster
        draw.rectangle([br_x, br_y, br_x + 2, br_y + 2], fill=fg)

        # A couple of center dots
        cx = size // 2
        cy = size // 2
        draw.point([(cx, cy), (cx + 2, cy), (cx, cy + 2)], fill=fg)

    else:
        # 32px: use the full glyph
        img = draw_qr_glyph(size)

    return img


def create_tray_ico():
    """Create tray icon optimized for 16x16 and 32x32."""
    img16 = draw_tray_glyph(16)
    img32 = draw_tray_glyph(32)

    out_path = os.path.join(ASSETS_DIR, "tray-icon.ico")
    img32.save(
        out_path,
        format="ICO",
        sizes=[(32, 32), (16, 16)],
        append_images=[img16]
    )
    print(f"Created {out_path} ({os.path.getsize(out_path)} bytes)")


if __name__ == "__main__":
    create_icon_ico()
    create_tray_ico()
    print("Done.")
