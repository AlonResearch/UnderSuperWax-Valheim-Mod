# Changelog for UnderSuperWax

## 0.0.2-alpha

- Added owner-authoritative per-piece RPC wax application flow for improved multiplayer consistency.
- Kept hammer-based wax apply gameplay unchanged while moving final state mutation to the authoritative owner.
- Added result callback handling so Beewax is consumed only on successful authoritative application.
- Added clearer failure feedback for rejected or failed wax apply attempts.
- Standardized the release packaging to use the Thunderstore README-only layout.

## 0.0.1-alpha

- Initial alpha fork from SuperWax.
- Keeps the original Beewax item, model, and icon.
- Replaces collider retagging with `WearNTear` waterproofing patches.
- Adds protection against rain and submersion.