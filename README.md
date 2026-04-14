# UnderSuperWax 0.0.2-alpha

UnderSuperWax is a fork of SuperWax by JasterLee that preserves the Beewax content pipeline while replacing the old runtime protection approach with explicit `WearNTear`-based waterproofing hooks.

## Ground Truth (Current Build)

UnderSuperWax currently provides:

1. A Beewax craftable item and hammer-based apply action.
2. Permanent piece-level waterproof state stored in `ZNetView`/`ZDO`.
3. Weather and water protection through Harmony patches on `WearNTear` checks.
4. Rain protection by forcing `IsWet` to `false` when waxed.
5. Submersion protection by forcing `IsUnderWater` to `false` when waxed.
6. Roof behavior override by forcing `HaveRoof` to `true` when waxed.
7. Persistence across loads via `WearNTear.Awake` component attachment.
8. Reapplication guards and inventory consumption on apply.

## Waterproofing Pipeline

Every successful wax application follows this path:

1. Player selects the hammer misc tool piece (`UnderSuperWaxTool`).
2. `Player.UpdatePlacement` Harmony prefix intercepts left-click placement flow.
3. Target is raycast from camera on the `piece` layer.
4. Target validation checks `ZNetView`, `WearNTear`, and eligible material type.
5. Existing wax state is checked through the piece `ZDO` key.
6. On success, ownership is claimed and wax state is set in `ZDO`.
7. Beewax is consumed from inventory.
8. Visual and SFX/VFX feedback is emitted.

## Runtime Hook Model

The mod uses localized, piece-state-driven overrides rather than global weather changes.

Patched methods:

1. `WearNTear.Awake`:
Adds and refreshes the per-piece protection component on eligible pieces.
2. `WearNTear.IsWet`:
Postfix forces result to `false` when piece is waxed.
3. `WearNTear.IsUnderWater`:
Postfix forces result to `false` when piece is waxed.
4. `WearNTear.HaveRoof`:
Postfix forces result to `true` when piece is waxed.
5. `WearNTear.OnDestroy`:
Per-piece component cleanup.
6. `Player.UpdatePlacement`:
Intercepts hammer tool use to apply wax state.

## State and Networking

1. Key: `UnderSuperWax_Waxed_Final_v1`.
2. Scope: per building piece `ZDO` boolean.
3. Authority: owner claim via `ZNetView.ClaimOwnership()` before state mutation.
4. Replication: native ZDO replication; no client-only state dependency.

## Eligibility Rules

1. Requires target `WearNTear` component.
2. Current material filter allows wood and core wood (`m_materialType` 0 or 3).
3. Non-eligible targets are rejected with user-facing message.

## Visuals and Assets

1. Beewax model and icon are preserved from the original content.
2. Source assets live in `src/UnderSuperWax/Resources`.
3. Embedded resources are loaded at runtime from assembly manifest.
4. Shine values are configurable (`Glossiness`, `Metallic`).

## Repository Layout

```text
UnderSuperWax/          # Repository root
├─ description.md       # Short project description used for distribution metadata
├─ package/             # Thunderstore distribution metadata and package-facing docs/assets
├─ releases/            # Generated release zip output directory
├─ src/                 # Plugin source code and embedded resources
├─ .gitignore           # Git ignore rules for local/build artifacts
├─ BuildRelease.ps1     # Build and package script for release artifacts
├─ LICENSE              # Project license
├─ README.md            # Main technical/project documentation
├─ testing.md           # In-game QA and feature verification guide
└─ UnderSuperWax.sln    # Visual Studio solution entry point
```

## Build and Release

1. Build and package command:
`./BuildRelease.ps1`
2. Output zip:
`releases/UnderSuperWax-0.0.2.zip`
3. Zip layout:
`manifest.json`, `README.md`, `icon.png`, `plugins/UnderSuperWax.dll`

Reference resolution defaults:

1. `ValheimRoot` defaults to Steam Valheim install path.
2. `JotunnRoot` defaults to r2modman cache path.
3. Override via MSBuild properties if your local setup differs.

## Notes

1. Build may emit `System.Net.Http` conflict warnings from Valheim/Jotunn reference mixing; current build remains valid.
2. The mod intentionally avoids collider retagging loops and relies on explicit `WearNTear` method overrides.

## Credit

Mod author: Aloncifer.

Original mod and Beewax concept: SuperWax by JasterLee.
