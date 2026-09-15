---
name: unity-scene-workflow
description: "Drive the Unity Editor via UnityMCP tools for Flipper_IUT. Use when inspecting scenes, GameObjects, console logs, materials, prefabs, running tests, or recompiling scripts."
---
# Unity Scene Workflow (UnityMCP)

Use the `unityMCP` MCP server tools (HTTP `http://127.0.0.1:8080/mcp`). Unity Editor must be open with `Assets/Scenes/Main.unity`, package `com.coplaydev.unity-mcp` installed, and HTTP bridge started (`Window > MCP for Unity`, Connect).

## Procedure
1. Verify Editor state : read resource `mcpforunity://editor/state` (or `unity://scenes-hierarchy`).
2. Inspect hierarchy : list resources, then read `mcpforunity://scene/gameobject/{id}` or use `get_gameobject` / `get_scene_info`.
3. Check errors first : `get_console_logs` (or resource `unity://logs`) before editing.
4. Edit scene safely : prefer `update_gameobject` / `update_component` / `move_gameobject` / `set_transform` over destructive ops. Batch with `batch_execute` when creating many objects.
5. Scripts : edit C# in `Assets/Scripts/`, then `recompile_scripts`. Never claim compile success without recompiling or checking logs.
6. Tests : `run_tests` (EditMode) or `Window > General > Test Runner`.
7. Save : `save_scene` only on explicit request.

## Constraints
- Never invent GameObject names/IDs : always list hierarchy first.
- Never claim a scene change is visible without `get_gameobject` confirmation.
- If MCP tools report "not connected", tell user to open Unity Editor and start the MCP Unity server (`Window > MCP for Unity`).
