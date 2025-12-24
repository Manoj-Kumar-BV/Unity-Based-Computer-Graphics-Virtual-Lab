# Computer Graphics Virtual Lab (Unity)

An interactive virtual lab to learn core Computer Graphics raster algorithms by doing.
You choose a module, click input points on a pixel canvas, and watch the algorithm draw step-by-step with a calculations panel.

Unity version: `2022.3.21f1`

## Modules
- Line Drawing: DDA, Bresenham, Bresenham (All Slopes)
- Circle Drawing: Midpoint Circle
- Line Clipping: Cohen–Sutherland, Liang–Barsky
- Polygon Fill: Scanline Fill

## Scenes (Build Order)
These are included in Build Settings and are used by the app flow:
1. `Assets/LineDrawingAlgorithm/Examples/Opening/Opening.unity`
2. `Assets/LineDrawingAlgorithm/Examples/MainMenu/MainMenu.unity`
3. `Assets/LineDrawingAlgorithm/Examples/Interactive/Interactive.unity`

Legacy example scenes are also kept in Build Settings:
- `Assets/LineDrawingAlgorithm/Examples/DDA/DDA.unity`
- `Assets/LineDrawingAlgorithm/Examples/Bresenham/Bresenham.unity`
- `Assets/LineDrawingAlgorithm/Examples/Bresenham/BresenhamFull.unity`

## TODO
- Xiaolin-Wu
- Gupta-Sproull

## How to Run
Open the project in Unity and press Play.

App Flow:
- Opening (Welcome) → MainMenu → Interactive module

Controls:
- `Esc` returns to MainMenu from the Interactive scene.
- Use `Clear` to reset the current module state.

### Module Input Instructions

Line Drawing
- Click point 1 → click point 2

Circle Drawing (Midpoint Circle)
- Click center → click a point on the radius

Line Clipping (Cohen–Sutherland / Liang–Barsky)
- Click window corner 1 → click window corner 2
- Click line point 1 → click line point 2
- The window stays; draw more lines without re-creating the window (use Clear to reset)

Polygon Fill (Scanline Fill)
- Click vertices to create a polygon
- Press `Finish` to close the polygon
- Press `Fill` to run scanline fill

## Audio (optional)
The project auto-loads narration clips using `Resources.Load`, so to make audio work in Windows builds, place clips here:

`Assets/Resources/LineDrawingAlgorithm/`

Expected clip names (exact):
- `Welcome Intro`
- `DDA Algorithm`
- `Bresenham's Line Algorithm`
- `Midpoint Circle Algorithm`
- `Cohen-Sutherland Line Clipping`
- `Liang-Barsky Line Clipping`
- `Scanline Polygon Fill`

Opening scene:
- `Play` replays the welcome narration
- `Continue` goes to MainMenu

## Notes
- Audio files are tracked via Git LFS (`*.mp3`), so ensure Git LFS is installed if you clone the repo.
