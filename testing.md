# UnderSuperWax In-Game Testing Guide (0.0.2-alpha)

This guide verifies each feature of UnderSuperWax in Valheim, including single-player behavior, multiplayer RPC behavior, persistence, and regressions.

## 1. Test Setup

1. Install dependencies:
- BepInExPack for Valheim
- Jotunn

2. Install mod build:
- Place `UnderSuperWax.dll` in `BepInEx/plugins`

3. Use a test character/world where you can spawn resources quickly.

4. Optional but recommended:
- Enable BepInEx console/log output while testing.

## 2. Quick Smoke Test

1. Launch game with mod enabled.
2. Open world and verify no startup errors.
3. Open hammer build menu and check Misc category contains the wax apply piece.

Expected:
- Game loads normally.
- Mod does not spam exceptions.
- Wax apply piece appears in hammer Misc.

## 3. Feature Test Cases

### 3.1 Beewax Crafting Registration

Steps:
1. Go to a workbench.
2. Check crafting list for Beewax.
3. Craft Beewax.

Expected:
- Beewax recipe exists.
- Costs match design (Honey + Resin).
- Crafted amount matches design.
- Item icon/model appear correctly.

### 3.2 Hammer-Based Application Flow

Steps:
1. Equip hammer.
2. Select the UnderSuperWax apply piece in Misc.
3. Target a valid wood or core wood building piece.
4. Left-click to apply.

Expected:
- Message confirms success (Piece waxed).
- VFX/SFX play.
- Player attack animation trigger plays.
- Exactly 1 Beewax is consumed.

### 3.3 Validation Messages

Steps:
1. Try applying to invalid target (non-wood piece).
2. Try applying without Beewax in inventory.
3. Apply once, then attempt to apply again to same piece.

Expected:
- Invalid target: rejection message (wood only).
- Missing item: rejection message (need Beewax).
- Already waxed: rejection message.
- No Beewax consumed on rejection.

### 3.4 Rain Protection (IsWet Override)

Steps:
1. Build two identical wood pieces exposed to rain.
2. Wax only one piece.
3. Let rain occur long enough for visible condition differences.

Expected:
- Waxed piece behaves as dry/protected.
- Unwaxed piece shows normal rain exposure behavior.

### 3.5 Submersion Protection (IsUnderWater Override)

Steps:
1. Place two valid pieces where they are submerged/partially submerged.
2. Wax one, leave one unwaxed.
3. Observe over time.

Expected:
- Waxed piece remains protected from water/submersion behavior.
- Unwaxed piece behaves normally under water exposure.

### 3.6 Roof Override Behavior (HaveRoof Override)

Steps:
1. Place test object/piece below a waxed structure and compare with unwaxed equivalent setup.
2. Verify shelter/covered behavior consistency where applicable.

Expected:
- Waxed target piece is treated as having roof coverage behavior.

### 3.7 Persistence Across Reloads

Steps:
1. Wax multiple valid pieces.
2. Save and exit world.
3. Re-enter world.
4. Re-test waxed pieces in wet/submerged conditions.

Expected:
- Waxed state persists after reload.
- Reapplication still blocked as already waxed.
- Waterproof behavior still active.

### 3.8 Visual Behavior

Steps:
1. Wax a piece and observe look changes.
2. Change Glossiness/Metallic config values.
3. Restart game and compare visual effect.

Expected:
- Waxed visuals show configured shine.
- Unwaxed visuals remain normal.
- Config changes affect visual intensity.

### 3.9 Destroy Cleanup

Steps:
1. Wax a piece.
2. Destroy/remove the piece.
3. Rebuild same type piece in same area.

Expected:
- Old piece state does not leak to new piece.
- New piece starts unwaxed.

## 4. Multiplayer RPC Tests (0.0.2-alpha Critical)

Run these in both a hosted session and, if available, a dedicated server session.

### 4.1 Client Applies to Host-Owned Piece

Steps:
1. Host creates piece.
2. Client uses hammer wax apply on that piece.

Expected:
- Apply succeeds.
- Client gets success message.
- Exactly 1 Beewax consumed on client only when success result returns.
- Host and all clients observe protected behavior afterward.

### 4.2 Concurrent Apply Attempt

Steps:
1. Two players attempt to wax same unwaxed piece at nearly same time.

Expected:
- One succeeds, piece becomes waxed.
- Second receives already waxed/failure path.
- No duplicate state corruption.

### 4.3 Failure/Invalid Path Does Not Consume Item

Steps:
1. Client targets invalid piece or induces failure path.
2. Attempt apply.

Expected:
- Reject/failure message shown.
- Beewax is not consumed.

### 4.4 Replication Consistency

Steps:
1. Client A waxes piece.
2. Client B joins/rejoins world after waxing.
3. Client B checks same piece.

Expected:
- Client B sees piece as waxed behaviorally (waterproof rules active).
- No desync between players.

## 5. Regression Tests

1. Normal building/repair with hammer still works.
2. Non-waxed pieces continue vanilla behavior.
3. Other mods do not lose basic functionality (sanity check with common building mods).
4. No severe error spam during extended play (10-20 minutes with repeated waxing).

## 6. Pass/Fail Checklist

Mark each as Pass/Fail:

- [ ] Game startup stable with mod loaded
- [ ] Beewax recipe and crafting correct
- [ ] Hammer apply tool available and functional
- [ ] Success path consumes exactly 1 Beewax
- [ ] Invalid/no-item/already-waxed paths reject correctly
- [ ] Rain protection works
- [ ] Submersion protection works
- [ ] Roof behavior override works
- [ ] Wax state persists across reload
- [ ] Visual config behavior works
- [ ] Destroy/rebuild cleanup works
- [ ] Multiplayer RPC apply works from non-owner client
- [ ] Multiplayer replication consistent after rejoin
- [ ] No major runtime errors during repeated usage

## 7. Suggested Evidence to Capture

1. Short clips/screenshots for:
- Before/after wax application
- Rain test comparison
- Submersion test comparison
- Multiplayer client apply success

2. Log snippets for any failures:
- Timestamp
- Action attempted
- Error stack trace
- Whether item was consumed