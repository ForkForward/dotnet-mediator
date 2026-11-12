# Brand assets

| File | Use |
|---|---|
| `icon.svg` | Source of the mark |
| `icon.png` | 256 x 256 render |
| `banner.svg` | Source of the README banner |
| `banner.png` | 1280 x 320 render, used in the README |
| `../../icon.png` | 128 x 128 render, the NuGet package icon |

## The mark

A path starts at a blue node and forks into two arrows that point forward.

- The **blue node** is the origin. Its colour, `#1695CA`, is the blue of the MediatR logo, as a visible credit to the project this code comes from.
- The **two arrows** are the code moving on from there.
- The background is ForkForward navy, `#14213D`. The strokes are white.

The mark is new artwork. The original MediatR logo is not used anywhere in this repository's current files, so that nobody mistakes the fork for the original; its files were in `assets/logo` and remain in the git history.

In other ForkForward projects the node takes the main colour of the project being continued; the rest of the mark stays the same.

## Rendering the PNG files

The PNG files are screenshots of the SVG files taken with a headless Chromium browser at the sizes above, with a transparent background:

```powershell
msedge --headless=new --default-background-color=00000000 --window-size=128,128 --screenshot=icon.png page-with-icon-at-128px.html
```

Change the SVG first, then render every PNG again.
