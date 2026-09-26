# Project context

- This is the Futebol Brasileiro Unity project. The user's repository is `https://github.com/dirrini/futebol-brasileiro`; the original project is `https://github.com/HTANV/Soccer-Unity`.
- Use the Unity version recorded in `ProjectSettings/ProjectVersion.txt` (currently **2022.3.62f2**) with **WebGL Build Support** installed. Do not upgrade the Editor or packages as an incidental change.
- Keep project assets and gameplay code organized under `Assets/FootballSimulator`. Preserve the existing URP, Addressables, Input System, UGUI, and TextMeshPro workflows.
- Consult `DESIGN.md` when changing presentation and keep it consistent with the implemented visuals.

# Docker

- The project must use Docker Compose to serve the generated WebGL build.
- Keep the Compose project name explicitly defined as `futebol-brasileiro` in `compose.yaml`. The web service is `soccer-web` and is available at `http://localhost:8080`.
- The current server does not require persistent volumes. Do not add volumes without a concrete need.
- Persistent volumes must have explicit Docker `name` values that include `futebol-brasileiro`.
- Do not create anonymous Docker volumes.
- Before creating new volumes, check whether a project volume already exists and reuse it when appropriate.
- Do not run `docker system prune --volumes` without user confirmation.
- Limit container operations to this Compose project; preserve unrelated containers and volumes.

# Visual authoring in Unity

- Whenever possible, assets, animations, positioning, scale, timing, colors, and scene composition must be visually editable in the Unity Editor.
- Prefer scenes, prefabs and prefab variants, materials, ScriptableObjects, and serialized Inspector properties over hard-coded visual values.
- For frame-by-frame adjustments, prefer property curves in Animation Clips, Animator Controllers, and Timeline when appropriate. Use serialized AnimationCurves and Gradients for tunable timing and color transitions.
- Author UI layouts using prefabs, Canvas, RectTransform, UGUI, and TextMeshPro components that can be previewed and adjusted in the Editor.
- Reserve code for gameplay rules, state coordination, events, input, and procedural effects.
- When modifying a legacy animation or visual controlled by code, migrate the affected visual portion to Editor-editable assets or properties whenever this can be done without harming game behavior.
- Keep scenes and resources organized so artists can preview, adjust, and play animations without running the complete game.
- Preserve asset GUIDs. Include the corresponding `.meta` files when adding assets or folders, and move or rename an asset together with its `.meta` file. Do not regenerate existing `.meta` files unnecessarily.
- Keep `UnityEditor` dependencies in Editor-only code or guard them with `UNITY_EDITOR` so player builds compile.

# Web builds

- After every project update, generate a fresh WebGL build and serve it so the user can test without exporting manually.
- From the project root in PowerShell, run `./scripts/webgl.ps1`. Follow `README-WEBGL.md` for prerequisites and troubleshooting.
- The activated local Unity Editor compiles the game and Addressables. Docker Compose packages and serves the generated files through Nginx. Do not require Unity credentials or a license file inside a container for this workflow.
- The script builds from a synchronized copy of saved `Assets`, `Packages`, and `ProjectSettings` in `Builds/UnityWebGLProject`. Do not edit that generated copy or force-close the user's original Unity Editor. Unsaved Editor changes are not part of the build.
- Preserve the previous successful build when compilation fails. Keep build logs under `Logs/WebGL` and report the relevant log path on failure.
- `docker compose up --build` and `./scripts/webgl.ps1 -SkipBuild` only publish an existing WebGL export; they do not satisfy the requirement to generate a fresh build after an update.
- Do not consider an update complete until the fresh build succeeds and `soccer-web` is healthy. If compilation or serving is blocked, clearly report what failed and what remains unverified.
- Check the browser behavior affected by gameplay or presentation changes. Report the checks actually performed; do not claim complete gameplay coverage from a build or menu smoke test.
- Preserve the Nginx MIME types and Gzip headers required by the Unity loader, including `application/wasm` for WebAssembly.
- Use the existing `AddressableAsync.AwaitResult()` and `UnityAsync.Delay()` helpers for the affected asynchronous runtime flows. Do not introduce Addressables `.Task`, thread-based timers, or blocking waits that are unsupported in this project's WebGL player.

# Git handoff commands

- After every completed update, include ready-to-run `git add`, `git commit`, and `git push` commands in the final response.
- The `git add` command must list only the files changed for that update, including corresponding `.meta` files and relevant documentation, so unrelated user changes are not staged. Do not use `git add .` or `git add -A` for handoff.
- Keep unrelated staged changes out of the proposed commit as well; use an explicit commit path list (for example, `git commit --only ... -- <files>`) when providing commands.
- Use an English Conventional Commit message that accurately describes the update. Group overlapping changes so each proposed commit remains coherent and compilable.
- Do not stage generated WebGL output, `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, IDE-generated files, build caches, or local credentials. `Builds` is ignored in this project; the container receives the export from the working directory.
- Some generated files may already be tracked in the inherited repository. Preserve unrelated changes and do not perform repository-wide cache removal or line-ending normalization as part of another task.
- Do not execute Git commands unless the user explicitly asks you to execute them. A request to provide commands is not authorization to run them.
