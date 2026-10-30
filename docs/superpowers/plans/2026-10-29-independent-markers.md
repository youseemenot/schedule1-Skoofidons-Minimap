# Independent markers implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** NPC portraits and green co-op dots track world objects without the phone map.

**Architecture:** Game references supply identity and position; cached game icon views supply NPC appearance. A bounded scheduler separates source discovery from coordinate sampling and UI movement.

**Tech Stack:** C#, MelonLoader/IL2CPP, Unity UI, .NET, executable offline tests.

**Spec:** ../specs/2026-10-29-independent-markers-design.md

## Global constraints

- Installed game directory is read-only; no launch or installation.
- No recurring global scene search or name matching for NPCs/co-op.
- Preserve NPC portraits, green co-op dots, existing police behavior and static POIs.
- Do not claim measured runtime performance without a game benchmark.

## Review focus

- First session before phone map is ever opened: discover owners and portraits without active UI lookup.
- Disconnect/reconnect and reload: no stale or duplicate markers.
- Disabled categories: no native sampling work.
- Large NPC batches: bounded work and reused views/buffers.
- Closed phone and non-map apps: no UI-coordinate dependency.

## Task 1: Tracking policy and tests

Files: `MinimapMod/TrackingPolicy.cs`, `tests/TrackingPolicy.Tests/`.

- [x] Write executable tests for sample cadence, per-frame work limits, overdue work without catch-up bursts, disabled tracking, identity removal/rejoin and projection.
- [x] Run tests and observe missing behavior fail (2/12 passed before implementation).
- [x] Implement the policy and run all tests (12/12 passed).

## Task 2: Game adapter and integration

Files: `MinimapMod/IndependentMarkers.cs`, `MinimapMod/MainMod.cs`, `SniccupMiniMap.csproj`.

- [x] Confirm real game APIs and portrait initialization from local binaries.
- [x] Connect NPC world POIs and Player.PlayerList to independent cached markers, using the tested policy.
- [x] Remove duplicate dynamic entries from old UI sync and fix inactive-phone bootstrap.
- [x] Build Release using workspace-only outputs; fix compatibility errors (zero warnings/errors).

## Task 3: Review and delivery

Files: `README.md`, `docs/testing/independent-markers.md`.

- [x] Document build requirements, work limits and manual performance acceptance.
- [x] Run tests and Release build; review the full diff with a fresh reviewer. Three P2 findings fixed in one pass, each RED (12/15) to GREEN (15/15).
- [x] Verify original installed files and leave source diff plus candidate DLL for review: all original 602 files retain size/mtime; installed minimap and GameAssembly SHA256 hashes match the initial values. One unrelated HR Department DLL appeared during the task and was left untouched; the complete difference is recorded in the workspace verification report.

## Execution notes

- Baseline source builds with zero warnings/errors when intermediate/output directories are redirected. Existing obj files have write restrictions; new build outputs will use workspace artifacts.
- Work remains in the existing clean source checkout at the user's requested location; no worktree, git metadata mutation, commit, or installation is needed.
- Final verification: 15/15 regression tests; Release 0 warnings/errors; candidate `artifacts/minimap-tracking/candidate/SkoofidMinimap.dll` outside the game directory. Game launch and performance benchmarking were not performed.
- Independent review's three lifecycle findings were fixed with failing-then-passing tests. Runtime hook delivery, portrait appearance, bootstrap, host/guest lifecycle and FPS/memory acceptance remain explicit manual checks, not unverified success claims.

## Accepted state recorded on 2026-10-30

The user approved the dev.3 dealer appearance and requested a local Git commit plus changelog update. Current version is 2.2.8-dev.3 / assembly2.2.8.2 on game0.4.7f12. Visual single-player feedback is recorded in the testing document; co-op remains explicitly unverified. The complete suite now has18 tests. No remote push, tag or publication is requested.
