# Melee visual treatments

The alternate treatment now defaults to **Asset Slashes** on all four players:
**Prick 5** for the thrust and a **black and white volumetric crescent** for the
second arcing attack. **Sweep treatment** switches that arc back to **Sword Slash 5**.
Open **MASSIVE → Player → Melee Visual Preview**, choose the player, and use
**Play stage**, **Loop stage**, or **Scrub attack**. The old Melee Plasma Preview
menu remains an alias. **Original particles**, **Plasma**, and **Off** remain
available for comparison; only one treatment renders at a time.

Each prefab stage has its own prefab, tint, intensity, reach/width scales,
position/rotation offsets, and optional smoke, accent particles, ground impact,
and distortion. Smoke, ground impact and distortion start off. Under **Advanced —
source timing and calibration**, source timestamps determine which frames are
fitted to the existing windup, active window and recovery. These settings change
presentation only. The finisher retains its existing optional plasma ring study.

The thrust's **World-space Emission** toggle lets its emitter follow the player's
current position and aim while emitted particles retain their world-space
position and heading. **Emission Refreshes** sets one to five newly emitted main
streaks during the lunge. Each starts at the player's current pose while older
streaks continue along their world-space trajectories. **Thrust Linger (s)** sets
their extra visual lifetime after the thrust stage ends; **Thrust Fade Curve**
shapes their disappearance. In
**1 — Thrust**, **Scrub lingering trail** runs from the stage's end to the end of
that linger. The remaining particles do not extend the hitbox or deal damage.

Prick 5's authored 0.32-second delay is skipped, since the complete thrust lasts
only 0.30 seconds. Sword Slash 5 is turned from a vertical chop onto the XZ
playing plane; its visible arc is retimed to the swipe's damage window. Temporary
emitters follow the live player and sword direction, mirror with swipe handedness,
scale from the existing melee capsule, and disable automatic looping and particle
collision. The thrust's emitted particles persist through their configured fade;
the source sweep prefab clears when its stage ends. Original vendor assets are
not modified. Intensity above one uses material property blocks for emission gain.

**Organic Glow** is enabled independently for the thrust and volumetric sweep.
The thrust replaces the source's broad soft glow with a bounded three-dimensional
energy volume attached to emitted streaks. The sweep breaks its enabled light
layers into curled pockets, dark gaps and hot internal channels. **Glow Breakup**
controls their contrast, **Glow Depth** controls their depth and curl, and **Glow
Flow** controls their internal motion. The thrust also has **Glow Intensity**;
zero hides its energy volume without restoring the original glow. Switching
Organic Glow off restores the smoother treatment for comparison. Its optional
**Energy Material Override** uses the bundled Resources material when empty.

The sweep's **Extra Fine Tendrils** adds three strands by default, independently
of the existing **Tendril Count**. The combined count is capped at twelve.
**Energetic Crackle** adds local kinks and transient hot channels; **Crackle
Amount**, **Crackle Detail** and **Crackle Rate** control strength, spatial detail
and renewal speed. Changes are local to the strands rather than a synchronized
whole-effect flash. Crackle off or Amount zero removes that extra motion; Rate
zero holds its crackle pattern. Attack timing and world-space trail histories
are unchanged.

Organic-energy validation: both shaders compiled without errors, and the revised
effects were inspected from above and obliquely, during movement and fade.
Thrust Organic Glow off restored the authored glow; organic intensity zero hid
both glow treatments; aftermath and Stop cleared the new volume. All sweep layers
off still hid the sweep with organic/crackle enabled. Both Light and Dark completed
two normal input-driven thrust-to-sweep combos, retained thrust trails during the
sweep and trails after completion, then cleared every visual. No Console errors
were reported, and the user's existing tuning was unchanged. No standalone build
or target-device performance profile was run. The added thrust volume is bounded
to 18 energy segments per emission and 72 ray samples per covered pixel; extra
sweep strands and procedural glow increase GPU work while visible.

Validation (September 14): Unity compiled with no Console or volume-shader errors.
Normal scripted input through PlayerControllerScript triggered thrust → sweep
for both Light and Dark players while moving and turning. Thrust particles
remained visible during the sweep, trails continued while the attack controller
was idle, and both effects cleared after their fade. Input modes were restored,
Play Mode and previews were stopped, and the scene remained clean. No standalone
build was run.

## Volumetric second arc

Choose **Style: Asset Slashes** and **Sweep treatment: Volumetric — black & white**.
Set **Attack stage** to **2 — Sweep**, then press **Loop stage**. Its settings are
under **Sweep — Volumetric black & white**. Switch **Sweep treatment** to
**Asset prefab — Sword Slash 5** to compare the source effect.

The fixed transport includes **Stop preview**, **Scrub attack** and, for the
volumetric sweep, **Scrub lingering trail**. The latter inspects the visual tail
after the collider's active window ends: 0 is that window's end, and 1 is the end
of the configured linger. **Play stage** and **Loop stage** include this aftermath.
**Preview playback speed** is separate from the volume's internal **Flow Speed**;
**Loop rest (seconds)** sets the pause between repeated attacks.

- **Reach Scale**, **Arc Angle (degrees)**, **Radial Width (m)**,
  **Thickness (m)** and **End Taper** shape the crescent. Thickness sets its
  vertical depth in world units.
- **Tendril Count** selects two to ten main tendrils. **Arc Travel Time (s)** sets their
  traversal time, **Emission Spacing (s)** separates their launches, and
  **Tail Length** sets the trailing section behind each moving head.
- **Split / Merge Amount** controls how strongly the tendrils separate and rejoin;
  **Junction Frequency** controls how often they meet.
- **World-space Linger (s)** is the extra visual lifetime after the collider's
  active window ends, independent of the attack's duration. **Fade Curve** shapes
  its disappearance. Lingering trails deal no damage.
- **Density** controls opacity; **White Edge Intensity** and
  **Filament Intensity** control brightness.
- **Turbulence (m)**, **Noise Scale** and **Flow Speed** control the moving 3D folds.
- **Black Body**, **White Edge**, **Filaments** and **Satellite Wisp** are separate toggles.
- **Plasma Sheath**, **Inner Tributary** and **Fine Wake** add surrounding mass,
  a secondary inner flow and finer trailing detail. Each has its own toggle and
  matching **Intensity** control.
- **Volume Material** references the shared volume shader material; the dials
  above adjust this player's effect without modifying that material asset.

The shader integrates a procedural density field through a closed 3D volume,
with black absorption, white emission, curled internal strands and tapered tips.
It uses no slash texture and remains a volume when viewed obliquely. Only the
second attack uses it; the thrust retains Prick 5 and the finisher retains its
existing treatment. Combat rules are unchanged. Each new point uses the player's current position, aim and
swipe handedness. Emitted pieces retain their position and heading in world space;
the arc continues emitting around the moving player rather than around its first
origin. Tendrils split and rejoin as they travel. After the damage window closes,
the remaining trail stays behind and fades without extending the hitbox or
dealing further damage.

This is a stylized emission/absorption effect, not a fluid or light-scattering
simulation. It uses 128 bounded ray samples per covered pixel while visible;
profile on target hardware before increasing thickness, reach or simultaneous
effects. Up to four emissions per player are retained in a reusable pool;
overlapping repeats preserve previous trails within that bound. Occupied depth is tested against the scene; partial intersections
with opaque geometry are not clipped partway through the volume.

Earlier volume validation covered comparison switching, all volume layers off,
preview cleanup on Play Mode entry, top and oblique rendering, and monochrome
output. Those checks established the volume rendering and transport baseline.
The September 14 emission-history revision passed focused moving-emitter checks:
previously emitted thrust particles retained their world positions within
0.000001 m and their headings exactly when the emitter moved and turned 55
degrees, including the scene's nonuniform thrust width setting. Refreshes created
additional particles. Committed sweep centres and headings had zero drift while
new centres followed 1.29 m of player movement. Both tails remained visible beyond
the stage, then cleared at the end of the fade; Stop cleared both immediately.
These checks used temporary components and left the scene clean. Movement-preview
renders were also inspected. The sweep uses 32 recorded points; sharp reversals or
self-crossing paths can expose the nearest-segment approximation and should be
reviewed if movement speeds or attack duration change substantially.

## Original procedural plasma study

Open **MASSIVE → Player → Melee Plasma Preview**. Choose a scene player, select
**Plasma**, then **Loop stage**. The transport stays visible while the controls
scroll. **Stop preview**, closing the window, switching player, disabling the
component, and entering Play Mode remove the edit preview. Preview state is not
saved. In Play Mode the component follows normal attack input.

The shared **Players.prefab** configures all four player roots with this alternate
treatment. **Original particles / Plasma / Off** switches only melee presentation.
Existing colliders, damage, movement, combo windows, power-up routing and the
Time Dilation ghost trail retain their existing behavior. Scene-specific extra
melee particles under Sword Arc Driver are discovered automatically; explicit
`Legacy Melee Particles` references override that discovery.

## Controls

- **Solid Body:** connected opaque energy silhouette; black and white balance
  reverses between Light and Dark.
- **Bright Rim:** the fine perimeter, with independent intensity and width.
- **Flowing Filaments:** drifting internal contrast/veins. Disable to remove the
  internal texture; intensity sets its contrast.
- **Soft Halo:** restrained light outside the solid edge.
- **Edge Warble / Noise Scale / Flow Speed:** distortion, texture scale and drift.
- **Reach Scale / Width Scale / Sweep Bend:** silhouette proportions. Reach starts
  from the existing sword capsule. These are visual controls, not attack-range
  changes; extreme values can make the telegraph differ from the hitbox.
- **Windup Opacity / Recovery Opacity:** quiet forming/receding energy around the
  existing activation window. The active phase renders at full effect opacity.
- **Preview Playback Speed / Loop Rest:** edit-preview timing only.

The thrust forms a tapered ribbon; the swipe bends that ribbon behind the live
sword direction. Edit preview uses a smooth approximation of the authored swipe
curve; gameplay reads the controller's actual visual direction and swipe sign.

The third **Finisher** preview is an optional expanding-ring study. The current
players have no Repulsor AOE component, and the current profile does not queue
from the second stage into the third. This feature does not enable that gameplay.
If a Repulsor AOE is later configured, its actual collider center and radius drive
the runtime ring, gated to that collider's enabled state.

## Rendering

The original shader and procedural ribbon borrow the reference beam's principles
of domain-warped caustics and contrasting surfaces, using the pack's existing
NoiseCausticSoft02 and NoiseForOffset02 textures. The laser prefab, raycast,
segment spawning, lights and impact effects are not used. Standard alpha blending
allows black energy to cover the grid. Per-player controls use a material property
block. Temporary meshes and renderer objects are released on disable.

Validated in the Built-in pipeline on Unity 6000.0.28f1: shader and C# compilation,
Light/Dark input-driven thrust→swipe, idle cleanup, style switching, renderer
restoration, external Time Dilation emission, and edit-preview handoff to Play Mode.
No standalone player build was run.
