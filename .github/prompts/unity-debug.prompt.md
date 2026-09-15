---
description: "Diagnose Unity playmode or EditMode failures in Flipper_IUT using console logs, scene state, and physics setup."
agent: "agent"
argument-hint: "Describe the symptom (e.g. ball falls through, flipper inert)"
---
Check Unity console logs via UnityMCP (`get_console_logs`), then hierarchy (`unity://scenes-hierarchy`) and the suspect GameObject details.

Verify for Flipper_IUT :
- Tag `Ball` present on ball prefab, colliders non-trigger (except `DrainZone`), `Rigidbody` non-kinematic.
- `Flipper` has `HingeJoint` with `useSpring`, axis/limits correct.
- `TableGravity` active in scene, `Physics.gravity` not overridden elsewhere.
- Singletons `GameManager`/`MissionManager` present once, UI refs assigned.

Report : error messages verbatim, offending GameObject + component, minimal fix in `Assets/Scripts/`.
