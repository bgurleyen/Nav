#!/usr/bin/env python3
"""Rasterize a list of SVGs into a viewer-compatible landscape PDF + HTML."""
import base64
import io
import subprocess
import sys
from pathlib import Path


def ensure_deps():
    try:
        import cairosvg  # noqa: F401
        from PIL import Image  # noqa: F401
        import img2pdf  # noqa: F401
    except ImportError:
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install", "-q", "cairosvg", "pillow", "img2pdf"]
        )


def main():
    if len(sys.argv) < 4:
        print(
            "usage: raster-svg-pages.py svg-list.txt out.pdf out.html [title]",
            file=sys.stderr,
        )
        return 2

    list_path = Path(sys.argv[1])
    out_pdf = Path(sys.argv[2])
    out_html = Path(sys.argv[3])
    title = sys.argv[4] if len(sys.argv) > 4 else "Level maps"

    svgs = [Path(line.strip()) for line in list_path.read_text().splitlines() if line.strip()]
    if not svgs:
        print("no svg pages", file=sys.stderr)
        return 1

    ensure_deps()
    import cairosvg
    import img2pdf
    from PIL import Image

    jpeg_bytes = []
    for i, svg in enumerate(svgs, 1):
        png = cairosvg.svg2png(url=str(svg), output_width=1440, output_height=1080)
        im = Image.open(io.BytesIO(png)).convert("RGB")
        buf = io.BytesIO()
        im.save(buf, format="JPEG", quality=84, optimize=True)
        jpeg_bytes.append(buf.getvalue())
        print(f"  raster {i:02d}/{len(svgs)} {svg.name} jpeg={len(buf.getvalue())}")

    layout = img2pdf.get_layout_fun((img2pdf.in_to_pt(10), img2pdf.in_to_pt(7.5)))
    pdf_bytes = img2pdf.convert(jpeg_bytes, layout_fun=layout, pdfa=False)
    out_pdf.parent.mkdir(parents=True, exist_ok=True)
    out_pdf.write_bytes(pdf_bytes)
    print(f"wrote {out_pdf} ({out_pdf.stat().st_size} bytes, {len(svgs)} pages)")

    parts = [
        "<!doctype html><meta charset=utf-8>",
        f"<title>{title}</title>",
        "<style>body{margin:0;background:#333;color:#eee;font:14px sans-serif}",
        "h1{margin:12px 16px} img{display:block;width:min(100%,1100px);margin:12px auto;",
        "background:#fff;box-shadow:0 2px 8px #0008}</style>",
        f"<h1>{title}</h1>",
    ]
    for i, data in enumerate(jpeg_bytes, 1):
        b64 = base64.b64encode(data).decode("ascii")
        parts.append(f"<p style='text-align:center'>Sayfa {i}/{len(jpeg_bytes)}</p>")
        parts.append(f"<img src='data:image/jpeg;base64,{b64}' alt='Level {i}'>")
    out_html.write_text("".join(parts), encoding="utf-8")
    print(f"wrote {out_html} ({out_html.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
