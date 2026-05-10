# Extra Overlays

A client-side Vintage Story mod that draws a health bar over any entity you look at.

- **Original mod page:** https://mods.vintagestory.at/extraoverlays
- **This fork's mod page:** https://mods.vintagestory.at/show/mod/48190
- **Forum thread:** https://www.vintagestory.at/forums/topic/6041-extra-overlays/

## About this fork

This is a 1.22 compatibility/bugfix fork of [DArkHekRoMaNT/ExtraOverlays](https://github.com/DArkHekRoMaNT/ExtraOverlays),
which is unmaintained past Vintage Story 1.21. Published under modid `extraoverlaysm4`
to coexist with the original on ModDB.

Changes vs. upstream:

- Builds clean against VS 1.22 / .NET 10.
- No CommonLib dependency — config loading uses the native VS API, so the mod
  ships as a single self-contained DLL.
- Migrates `Entity.SidedPos` → `Entity.Pos` (SidedPos is `[Obsolete]` in 1.22).
- Recovers gracefully from stale CommonLib-shaped `extraoverlays.json` files
  left over from older installs (resets to defaults on parse failure with a
  log warning, instead of crashing on first entity selection).
- Hardened renderer: idempotent `Dispose`, null-guarded shader/health-attribute
  reads, hex-color validation at config load.

## Configuration

First run writes `<VS data dir>/ModConfig/extraoverlays.json` with defaults.
Edit and reload. Invalid hex colors fall back to defaults with a warning.

| Field | Default | Notes |
|---|---|---|
| `FadeIn` / `FadeOut` | `0.2` / `0.4` | Seconds to fade in/out when targeting/untargeting an entity. |
| `Width` / `Height` | `100` / `10` | Bar size in pixels at point-blank range. |
| `YOffset` | `10` | Pixels above the entity's collision box top. |
| `HighHPColor` / `MidHPColor` / `LowHPColor` | `#7FBF7F` / `#BFBF7F` / `#BF7F7F` | Hex colors for the three HP bands. |
| `LowHPThreshold` / `MidHPThreshold` | `0.25` / `0.5` | HP fractions where colors switch. |

## License

Same as upstream — see [LICENSE](LICENSE).
