#!/usr/bin/env python3
"""Build a viewer-compatible PDF (and optional HTML) from slide SVGs.

Uses raster pages so Preview / Chrome / Acrobat all open the file.
"""
import io
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PREV = ROOT / "artifacts" / "scenario-previews"
OUT_PDF = ROOT / "artifacts" / "Level-Maps-1-39.pdf"
OUT_HTML = ROOT / "artifacts" / "Level-Maps-1-39.html"


def ensure_deps():
    try:
        import cairosvg  # noqa: F401
        from PIL import Image  # noqa: F401
        import img2pdf  # noqa: F401
    except ImportError:
        import subprocess
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install", "-q", "cairosvg", "pillow", "img2pdf"]
        )


def main():
    ensure_deps()
    import cairosvg
    import img2pdf
    from PIL import Image

    svgs = sorted(PREV.glob("level-[0-9][0-9].svg"))
    if len(svgs) != 39:
        print(f"expected 39 svgs, found {len(svgs)}", file=sys.stderr)
        return 1

    jpeg_bytes = []
    for i, svg in enumerate(svgs, 1):
        png = cairosvg.svg2png(url=str(svg), output_width=1440, output_height=1080)
        im = Image.open(io.BytesIO(png)).convert("RGB")
        buf = io.BytesIO()
        im.save(buf, format="JPEG", quality=82, optimize=True)
        jpeg_bytes.append(buf.getvalue())
        print(f"  raster {i:02d}/39 {svg.name} jpeg={len(buf.getvalue())}")

    # 10in x 7.5in landscape = PowerPoint 4:3, 720x540 pt
    layout = img2pdf.get_fixed_dpi_layout_fun(144)
    # Force exact page size regardless of pixel dpi math:
    layout = img2pdf.get_layout_fun((img2pdf.in_to_pt(10), img2pdf.in_to_pt(7.5)))
    pdf_bytes = img2pdf.convert(jpeg_bytes, layout_fun=layout, pdfa=False)
    OUT_PDF.write_bytes(pdf_bytes)
    print(f"wrote {OUT_PDF} ({OUT_PDF.stat().st_size} bytes)")

    # Self-contained HTML fallback (opens in any browser)
    parts = [
        "<!doctype html><meta charset=utf-8>",
        "<title>Level Maps 1-39 + OpenSky</title>",
        "<style>body{margin:0;background:#333;color:#eee;font:14px sans-serif}",
        "h1{margin:12px 16px} img{display:block;width:min(100%,1100px);margin:12px auto;",
        "background:#fff;box-shadow:0 2px 8px #0008}</style>",
        "<h1>Level Maps 1–39 + OpenSky senaryolari</h1>",
    ]
    import base64
    for i, data in enumerate(jpeg_bytes, 1):
        b64 = base64.b64encode(data).decode("ascii")
        parts.append(f"<p style='text-align:center'>Sayfa {i}/39</p>")
        parts.append(f"<img src='data:image/jpeg;base64,{b64}' alt='Level {i}'>")
    OUT_HTML.write_text("".join(parts), encoding="utf-8")
    print(f"wrote {OUT_HTML} ({OUT_HTML.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
