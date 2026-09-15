---
description: "Use when writing or editing Unity C# gameplay scripts (MonoBehaviour, Rigidbody, HingeJoint, collisions, triggers) in Assets/Scripts. Covers Flipper_IUT patterns."
applyTo: "Assets/Scripts/**/*.cs"
---
# Unity C# Gameplay Instructions

- MonoBehaviour lifecycle : init in `Awake`, wire UI in `Start`, no alloc in `Update`.
- Cache `HingeJoint`, `Rigidbody`, `Collider` in `Awake`. Never `GetComponent`/`Find` per-frame.
- Ball interactions : check `CompareTag("Ball")` first, then `GetComponent<Rigidbody>()`, then `AddForce(..., ForceMode.Impulse)`.
- Flippers : `HingeJoint` + `JointSpring` (`spring`, `damper`, `targetPosition`), toggle in `Update` via `Input.GetKey`.
- UI : `[SerializeField] private TMP_Text`, null-check before `.text =`.
- Singletons (`GameManager`, `MissionManager`) : guard `if (Instance != null && Instance != this) Destroy(gameObject)`.
- Scores via `GameManager.Instance.AddScore(int)` ; missions via `MissionManager.Instance.CompleteSubject(string)`.
- Keep serialized tuning fields (`points`, `impulseForce`, `flipSpeed`, `launchForce`) with sensible defaults.
