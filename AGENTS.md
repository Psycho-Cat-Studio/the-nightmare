# Repository Guidelines

## Project Structure & Module Organization

The Nightmare is a cooperative FPS built with Unity **6000.6.7f1** and Mirror networking. Read `docs/ARCHITECTURE.md` for onboarding, `docs/BUILDING.md` for builds, and `docs/ROADMAP.md` for planned work.

- `Assets/Scripts/`: gameplay C#, organized into `Player/FPS`, `Networking`, `Game-Logic`, `Enemy`, `Weapons`, `Traps`, `Building`, and `UI`; editor utilities live in `Editor`.
- `Assets/Scenes/`: build scenes are `MainMenu`, `LobbyTests`, and `ExtremeHomeMakeover`; other scenes include legacy experiments.
- `Assets/Prefabs/`, `Assets/Resources/`, and other asset folders contain serialized objects, artwork, and audio. `Assets/Mirror/` contains vendored networking code.
- `Packages/` and `ProjectSettings/` define dependencies and Unity configuration. `Tools/` and `.github/ci/` contain maintenance and validation scripts.

## Build, Test, and Development Commands

Open the repository in the pinned Unity editor, open `Assets/Scenes/MainMenu.unity`, and press Play. Install the required desktop Build Support modules.

- **The Nightmare → Build**: build desktop players into `Builds/<platform>/`.
- `python3 Tools/assign_scene_ids.py --check`: verify saved Mirror scene IDs.
- Run the license-free compile and asset checks using Docker:

```bash
docker run --rm -v "$PWD:/project" -w /project \
  unityci/editor:ubuntu-6000.6.7f1-base-3 bash .github/ci/compile-check.sh
```

## Coding Style & Naming Conventions

Follow nearby C# code: four-space indentation, PascalCase types and methods, camelCase parameters, and `_camelCase` private fields. Match component filenames to class names. Preserve serialized field names unless explicitly migrating their references. No repository-wide C# formatter is configured; CI uses actionlint for workflows.

Keep gameplay decisions server-authoritative. Use Mirror Commands, SyncVars, and RPCs consistently with existing components.

## Testing Guidelines

No dedicated automated gameplay test suite or coverage threshold is configured. Compile checks cover desktop platform defines, editor scripts, and asset integrity; they do not verify runtime behavior.

For gameplay changes, test an editor instance alongside a built player: host with `./TheNightmare -host -name "Test room" -port 7777`, then join with `./TheNightmare -connect 127.0.0.1:7777`. Exercise affected behavior on both peers.

## Commit & Pull Request Guidelines

History uses short, imperative subjects, sometimes prefixed with `CI:`, `Docs:`, or `Changelog:`. Keep commits focused. PRs should describe the problem, resulting behavior, validation, and relevant issues; include screenshots for UI changes. Update documentation or `CHANGELOG.md` when appropriate.

## Asset Integrity

Commit Unity `.meta` files with their assets. Preserve scene/prefab YAML structure and saved Mirror scene IDs. Use WebM/VP8 or OGV for videos imported by Linux CI. Keep generated builds, caches, and Unity license credentials out of Git.
