# Computer Graphics Virtual Lab (Unity)
Refrence source - https://en.wikipedia.org/wiki/Line_drawing_algorithm
Results pixel is set to Texture2D displayed on a flat Quad GameObject

## Modules
- Line Drawing: DDA, Bresenham, Bresenham (All Slopes)
- Circle Drawing: Midpoint Circle
- Line Clipping: Cohen–Sutherland, Liang–Barsky
- Polygon Fill: Scanline Fill

## TODO
- Xiaolin-Wu
- Gupta-Sproull

## How to Run
Open the project in Unity `2022.3.21f1` and press Play. The app starts in:
- `Assets/LineDrawingAlgorithm/Examples/Opening/Opening.unity`

Flow:
- Opening (Welcome) → MainMenu → Interactive module

In each module you select points directly on the canvas and the algorithm is drawn step-by-step.

## Audio (optional)
For audio to work in Windows builds, put narration clips under:
- `Assets/Resources/LineDrawingAlgorithm/`

Expected clip names (examples):
- `Welcome Intro`
- `DDA Algorithm`
- `Bresenham's Line Algorithm`
- `Midpoint Circle Algorithm`
- `Cohen-Sutherland Line Clipping`
- `Liang-Barsky Line Clipping`
- `Scanline Polygon Fill`

## Related
Circle Drawing Algorithm
- Mid-Point
- Bresenham
