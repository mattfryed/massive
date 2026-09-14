# Scientific Dynamo prototype

This prototype displays **SWMF/BATS-R-US 2023 magnetic and plasma fields driven by observed solar-wind inputs**. Unity samples prepared model output; it does not run the MHD solver. The first included encounter is a pressure-pulse excerpt on **2026-01-03, 02:50â€“03:30 UTC**. It is not a complete geomagnetic storm or a representative statistical catalogue.

## Open and use

- `Assets/Scenes/Dynamo Scientific Demo.unity`: isolated 3D demonstration. Enter Play Mode. Space pauses playback, R restarts, right-drag orbits, and the wheel zooms. The timeline supports scrubbing. Pink lines follow B; cyan traces follow local plasma flow.
- `Assets/Scenes/S-8_DYNAMO-SCIENTIFIC.unity`: a separate copy of the existing gameplay prototype, with the scientific field and local-flow player drift wired in. Its legacy Dynamo storm scheduler and authored flow visuals are disabled. Other gameplay content comes from the source prototype.
- `Assets/Scripts/Anomalies/Magnetosphere/Scientific Dynamo.prefab`: reusable field and rendering components.
- `MASSIVE > Dynamo > Build Scientific Prototype`: imports the prepared episode and creates missing scenes. Existing demo/gameplay scenes are deliberately not overwritten on subsequent runs.
- `MASSIVE > Dynamo > Validate Scientific Data and Sampling`: dependency-free Editor validation.

`ScientificMagnetosphere` owns the episode, scientific clock, coordinate transform and two neighboring snapshots. `ScientificFieldLineRenderer` uses that source for both magnetic and plasma traces. `PlayerStormPush` accepts an optional explicit reference to the same source; its existing behavior remains available when no scientific source is assigned.

## Scientific provenance

- Model: [SWMF/BATS-R-US 2023](https://ccmc.gsfc.nasa.gov/models/SWMF~2023/), University of Michigan, hosted by NASA CCMC.
- Run: [Wei_Liu_090426_GM_1](https://ccmc.gsfc.nasa.gov/ror/results/viewrun.php?runnumber=Wei_Liu_090426_GM_1), requested by Wei Liu and published by CCMC on 2026-09-06. The full simulation covers January 3, 2026. It uses OMNI inputs, a time-updated dipole, corotation, a 2.5 Earth-radius inner boundary, and the Rusanov solver. It does not include a coupled ring-current model.
- Inputs: [High Resolution OMNI](https://omniweb.gsfc.nasa.gov/html/omni_min_data.html). `source-IMF.txt` retains the run's actual input sequence; `source-run.json` retains the model configuration. These are model boundary inputs, not local plasma measurements.
- Coordinates: [NASA's coordinate definitions](https://sscweb.gsfc.nasa.gov/users_guide/Appendix_C.html); offline conversions use [GEOPACK](https://github.com/tsssss/geopack).
- Each prepared frame has a UTC timestamp, source filename, source SHA-256, and prepared-file SHA-256 in `manifest.json`.

Credit the University of Michigan SWMF team, NASA CCMC, the run provider and OMNI when presenting these results. Consult [CCMC's publication policy](https://ccmc.gsfc.nasa.gov/publication-policy/) before the talk; the project has not contacted the model providers or CCMC.

## What the equations control

The source solver evolves three-dimensional magnetohydrodynamics. Its stored total magnetic field already contains the internal planetary contribution; the renderer does not add a second dipole or apply the old authored compression/warble.

Field lines integrate `dx/ds = Â±B/|B|` with midpoint stepping in three dimensions. The camera projects the resulting curves. Thin camera-facing strips provide a pixel-width core and antialiased edges with additive emission. No scene lighting is needed to make the lines bright.

The input pressure shown in the demo is `rho * |v|Â²`, with the proton-mass conversion from cm^-3 and km/s to nPa. It is approximately 1.1 nPa before this pulse and 2.4 nPa at its sampled peak. Stronger visual changes are determined by the model output, not by multiplying the geometry by this number.

The default Earth-fixed view rotates the full field from GSM into GEO using the event timestamp. Scientific Z becomes Unity Y. The optional Sun-oriented view retains GSM axes. Camera orbit is a separate presentation choice. In Earth-fixed mode, local flow uses `u_relative = u_GSM - omega_GSM Ã— r_km` before rotation, including Earth's rotation when interpreting plasma velocity relative to the arena.

Player drift samples local model velocity, projects it onto world XZ, maps 700 km/s to the existing maximum drift speed, and clamps that target. The existing bounded acceleration then follows the target. The speed conversion, response and cap are gameplay choices. Magnetic fields do not directly exert this drag on the player. Missing data, disabled source and the inner excluded region produce no scientific drift; they do not fall back to an unrelated wind direction.

## Approximation boundaries

- The archive's native adaptive grid is resampled to 65 Ã— 49 Ã— 49 nodes at 0.75 Re spacing, covering GSM X = -30â€¦18 Re and Y/Z = Â±18 Re. The exporter uses inverse-distance-squared weighting of the nearest eight native cell centres. This is a display approximation, not a divergence-preserving numerical method.
- The source inner boundary is 2.5 Re. Display and gameplay sampling exclude radii below 3.25 Re to reduce coarse-grid boundary artifacts. The central sphere depicts Earth's true relative radius; the empty inner region is an excluded model domain, not an unmagnetized cavity.
- Nine snapshots at five-minute cadence are interpolated in space and time. Lines are retraced through the interpolated field; their endpoints are not morphed between different field-line topologies. This does not resolve rapid reconnection or sub-minute fluctuations.
- The simulation's prehistory is contained in its snapshots. The excerpt starts after almost three simulated hours and stops at its last recorded snapshot. There is no automatic jump to the beginning or blend into an unrelated event.
- Flow traces show instantaneous streamlines. Their moving highlights are direction cues with artistic timing, not charged-particle trajectories. Line density and color are artistic; spacing is not a calibrated measure of magnetic flux.
- The first excerpt provides modest timestamp-derived angular variation. Broad directional and difficulty coverage requires additional observed episodes. `SelectObservedEpisode` selects a complete configured episode; no random strength multiplier or invented upstream direction is used.
- This prototype supplies local-flow drift. It does not infer a damaging magnetopause boundary from a field-strength threshold, and it does not implement storm damage or a radiation-dose model.
- Snapshot loading/decompression and texture uploads occur when crossing frame boundaries. The prototype keeps two decoded snapshots on the CPU and GPU, but the episode's compressed assets are also resident. Profile on the intended show hardware before choosing higher resolution or more episodes.

## Reproduce and expand

`Assets/Editor/DynamoScientificTools~/prepare_episode.py` prepares data outside Unity. It needs Python, NumPy, SciPy and GEOPACK; these are not Unity runtime dependencies. Pass `--cache` pointing outside Assets and `--output` pointing to an episode data directory. The defaults select this run and excerpt. `--run`, `--start`, `--end` and `--cadence-minutes` select another suitable run. Use a separate cache per run. The exporter requires observed OMNI input, updated dipole orientation, downloadable files, recognized units, finite samples and orthogonal coordinate transforms.

Runtime frame format: gzip-compressed little-endian float32 values, eight channels per node in this order: Bx, By, Bz [nT]; Ux, Uy, Uz [km/s]; density [amu/cmÂ³]; thermal pressure [nPa]. X is the fastest dimension, then Y, then Z. `manifest.json` describes dimensions, spacing, units by schema, transforms, metadata and checksums.

For a broader library, select real episodes covering the desired measured pressure/field conditions and Earth-fixed approach angles. Keep full multivariate sequences together. Future high-fidelity exports should use the model's native interpolator and finer/adaptive sampling near the magnetopause; include convergence and divergence diagnostics before using the display quantitatively.

## Validation performed

- Unity 6000.0.28f1 compiled the runtime, Editor scripts, compute shader and line shader without reported errors.
- Six spatial sampling checks and three timeline checks passed. All nine snapshots passed dimension, finite-value and SHA-256 checks.
- Play Mode checks passed at baseline, peak input pressure and the last frame: 13,666, 14,134 and 13,712 valid GPU segments respectively. Checks cover non-planar geometry, finite bounded trace steps, excluded-domain behavior, planar drift bounds and an isolated Rigidbody's expected acceleration.
- Playback stopped at the recorded endpoint. Disabling and re-enabling the source masked its output, reloaded successfully, and left exactly four live snapshot textures.
- The separate gameplay scene passed the same runtime checks with 12 wired player components, two active players and no active legacy storm scheduler. Clearing and restoring the configured episode was also checked.
- The exported magnetic north axis varied by less than 0.06 degrees in GEO across the excerpt, while the measured flow's Earth-fixed azimuth varied from approximately -47 to -67 degrees.
- Baseline and pulse camera captures were inspected. This is a functional prototype validation, not a target-hardware performance certification or validation of the underlying scientific model against spacecraft measurements.
