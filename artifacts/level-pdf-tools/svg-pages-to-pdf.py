#!/usr/bin/env python3
"""Convert a list of slide SVGs into a single landscape PDF."""
import sys
from pathlib import Path

def main():
    if len(sys.argv) < 3:
        print("usage: svg-pages-to-pdf.py svg-list.txt out.pdf", file=sys.stderr)
        return 2
    list_path = Path(sys.argv[1])
    out_path = Path(sys.argv[2])
    svgs = [Path(line.strip()) for line in list_path.read_text().splitlines() if line.strip()]
    if not svgs:
        print("no svg pages", file=sys.stderr)
        return 1

    try:
        import cairosvg
    except ImportError:
        import subprocess
        subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", "cairosvg", "pypdf"])
        import cairosvg

    try:
        from pypdf import PdfWriter, PdfReader
    except ImportError:
        import subprocess
        subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", "pypdf"])
        from pypdf import PdfWriter, PdfReader

    writer = PdfWriter()
    tmp_dir = out_path.parent / "scenario-previews" / "_pdf-pages"
    tmp_dir.mkdir(parents=True, exist_ok=True)
    for i, svg in enumerate(svgs, 1):
        page_pdf = tmp_dir / f"page-{i:02d}.pdf"
        cairosvg.svg2pdf(url=str(svg), write_to=str(page_pdf))
        writer.add_page(PdfReader(str(page_pdf)).pages[0])
        print(f"  pdf page {i}/{len(svgs)} {svg.name}")
    writer.write(str(out_path))
    print(f"merged {len(svgs)} pages -> {out_path} ({out_path.stat().st_size} bytes)")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
