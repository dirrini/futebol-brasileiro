# Project context

- This is the Futebol Brasileiro Unity project. The user's repository is `https://github.com/dirrini/futebol-brasileiro`; the original project is `https://github.com/HTANV/Soccer-Unity`.
- Use the Unity version recorded in `ProjectSettings/ProjectVersion.txt` (currently **2022.3.62f2**) with **WebGL Build Support** installed. Do not upgrade the Editor or packages as an incidental change.
- Keep project assets and gameplay code organized under `Assets/FootballSimulator`. Preserve the existing URP, Addressables, Input System, UGUI, and TextMeshPro workflows.
- Consult `DESIGN.md` when changing presentation and keep it consistent with the implemented visuals.

# Architecture and portable content

- Consult `ARCHITECTURE.md`, `DATA-FORMAT.md`, and `ROADMAP.md` before implementing competitions, database import/export, the database editor, or community skins. These documents distinguish planned contracts from implemented features.
- Keep new domain rules and application coordination independent of Unity, file formats, storage, and the external editor. Import adapters validate portable DTOs and map them into domain models; do not deserialize external files directly into domain objects.
- Keep authored database content, mutable season progress, and temporary match objects separate. Use stable IDs; never use names, array positions, Unity GUIDs, or match-local player IDs as persistent identity.
- Keep rosters and natural player positions independent of the legacy eleven-player `TeamEntry` and match formation slots.
- Keep the season session alive across UI unloads. Route match completion through one application operation correlated by fixture and execution IDs; duplicate completion must not award points twice.
- Community player skins are a required capability of the first usable external database editor, including custom model/texture import and assignment to a player. A preset-only selector does not fulfill this requirement.
- Keep `SkinId` and immutable skin revisions separate from `PlayerId`. Resolve media and skins through visual adapters; the competition core must not load textures, prefabs, or AssetBundles.
- Separate portable skin source packages from platform-specific prepared content. Use a versioned compatibility profile for rig, materials, kit integration, and resource budgets; retain gameplay controllers and animation-event ownership in game code.
- Preserve custom skin face/body materials when applying team uniforms. Changing a skin must not change player attributes, physics, hitboxes, or shot timing.
- Do not promise arbitrary FBX/GLB compatibility or automatic rig repair. Report validation failures in the database editor and provide a reference authoring template.
- Keep package import transactional, preserve the last valid content, and pin content revisions for active seasons. Reject unsupported rule types or versions. Fallback is allowed only for visual resources when declared by the contract, with a visible diagnostic.
- Introduce assemblies for new pure C# modules incrementally; do not reorganize the legacy engine as an incidental change. Follow the staged acceptance criteria in `ROADMAP.md`.

# Docker

- The project must use Docker Compose to serve the generated WebGL build.
- Keep the Compose project name explicitly defined as `futebol-brasileiro` in `compose.yaml`. The web service is `soccer-web` and is available at `http://localhost:8080`.
- The current server does not require persistent Docker volumes. The read-only host bind of `Assets/FootballSimulator/Data/FootballWorld/Examples` supplies the live database; preserve `create_host_path: false` and keep it outside the public web root. Do not add mounts or volumes without a concrete need.
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

- After updates to runtime code, compiled assets, scenes, bindings, shaders, packages, or the data contract, generate a fresh WebGL build and serve it so the user can test without exporting manually.
- Exception requested by the user: changes limited to the compatible external database JSON served by the existing Compose bind must be testable by saving the source and refreshing `http://localhost:8080`, without a Unity build or a container restart. Validate the edited data and verify its HTTP content and affected browser behavior. Preserve stable IDs and increment `databaseRevision`. This exception does not make images, models, uniforms, or other compiled Unity assets reloadable.
- From the project root in PowerShell, run `./scripts/webgl.ps1`. Follow `README-WEBGL.md` for prerequisites and troubleshooting.
- The activated local Unity Editor compiles the game and Addressables. Docker Compose packages and serves the generated files through Nginx. Do not require Unity credentials or a license file inside a container for this workflow.
- The script builds from a synchronized copy of saved `Assets`, `Packages`, and `ProjectSettings` in `Builds/UnityWebGLProject`. Do not edit that generated copy or force-close the user's original Unity Editor. Unsaved Editor changes are not part of the build.
- Preserve the previous successful build when compilation fails. Keep build logs under `Logs/WebGL` and report the relevant log path on failure.
- `docker compose up --build` and `./scripts/webgl.ps1 -SkipBuild` only publish an existing WebGL export; they do not satisfy the fresh-build requirement for compiled changes. JSON-only edits through the live bind require neither command.
- Do not consider a compiled update complete until the fresh build succeeds and `soccer-web` is healthy. For the JSON-only exception, require a healthy service and successful runtime import of the changed source after refresh. If compilation, import, or serving is blocked, clearly report what failed and what remains unverified.
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
