MASSIVE Abiogenic Background — Perspective V2

Why V1 looked flat
- It used arena/world-space plane positions as the field coordinates, so a large
  arena showed dozens of equally sized XY cells at once: visually, a dot wallpaper.
- The raymarch began on the background plane. No sphere could get closer than
  that plane, so none could grow to extreme near-camera scale.
- MASSIVE's gameplay camera is top-down/possibly orthographic, while the visual
  reference uses a synthetic pinhole camera and perspective rays.
- V1 had no per-sphere form shading, removing another strong depth cue.

What changed
- The shader now creates a virtual perspective camera inside the shader.
- Rays begin at (camera offset X/Y, -Virtual Camera Distance).
- Screen UV drives ray direction, independent of the gameplay camera projection.
- The repeated Z layers move toward that virtual camera, so beads grow, pass the
  viewer, and reveal smaller layers behind them.
- Negative-depth repeat now uses frac rather than fmod.
- Approximate sphere-normal shading gives each bead visible volume.

Install
Replace the four V1 files with these four files. Do not keep both versions with
identical class/shader names in the same Unity project.

Important tuning note
If a MassiveAbiogenicFieldProfile is assigned to the controller, the controller
writes its values through a MaterialPropertyBlock every LateUpdate. Tune the
PROFILE asset, not the Material inspector.

Starting preset
Virtual Camera Distance   3.0
Perspective Spread        0.125
Vanishing Point           (0, 0)
Virtual Camera Offset     (0, 0)
Camera Drift Amplitude    0
Max Steps                 80
Max Distance              14
Step Safety               0.78
XY Cell Scale             3
Layer Depth               0.5
Sphere Radius             0.10
Radius Amplitude          0.50
Depth Travel Speed        0.15
Swirl Speed               0.20
Bend Amplitude            0.10
Warp Strength             0.14
Base Luma                 0.02
Glow Strength             0.90
Sphere Form Shading       0.75
Near Bead Boost           0.65

Tuning hierarchy
1. Perspective Spread: lower = tighter/deeper tunnel; higher = wider field.
2. Virtual Camera Distance: lower = more aggressive near fly-throughs.
3. Sphere Radius: larger = chunkier beads and more near-screen occlusion.
4. Depth Travel Speed: controls outward/inward motion. Positive should move the
   field toward camera; invert the sign if the scene's visual motion reads reversed.
5. Vanishing Point: offsets the apparent deep origin without moving the quad.

Performance
80 ray steps plus sphere shading is intentionally a quality-first test setting.
After the look is approved, test 56–64 steps on the cabinet GPU.
