"""Regenerate the package icons in src/SapLogon/Assets.

Usage: python scripts/make-icons.py   (needs Pillow)

The mark is a SAP-blue rounded square with a white "SAP" wordmark.
Everything is drawn at 1024 px and downsampled, so small sizes stay smooth.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ASSETS = Path(__file__).resolve().parent.parent / "src" / "SapLogon" / "Assets"
BASE = 1024
TILE = (0, 112, 242, 255)  # SAP blue
FONT = "C:/Windows/Fonts/segoeuib.ttf"
INK = (255, 255, 255, 255)


def icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (BASE, BASE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, BASE - 1, BASE - 1), radius=int(BASE * 0.22), fill=TILE)

    font = ImageFont.truetype(FONT, int(BASE * 0.36))
    d.text((BASE / 2, BASE / 2), "SAP", font=font, fill=INK, anchor="mm")

    return img.resize((size, size), Image.LANCZOS)


def centered(width: int, height: int, mark: int) -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    img.alpha_composite(icon(mark), ((width - mark) // 2, (height - mark) // 2))
    return img


def main() -> None:
    squares = {
        "StoreLogo.png": 50,
        "LockScreenLogo.scale-200.png": 48,
        "Square44x44Logo.scale-200.png": 88,
        "Square44x44Logo.targetsize-24_altform-unplated.png": 24,
        "Square150x150Logo.scale-200.png": 300,
    }
    for name, size in squares.items():
        icon(size).save(ASSETS / name)

    centered(620, 300, 200).save(ASSETS / "Wide310x150Logo.scale-200.png")
    centered(1240, 600, 360).save(ASSETS / "SplashScreen.scale-200.png")
    print(f"Wrote {len(squares) + 2} icons to {ASSETS}")


if __name__ == "__main__":
    main()
