# AGENTS.md

A first-person Chinese cosmic-horror game about four-dimensional beings: PS1-style low poly, China in 2000, dread without jump scares. A father searches for his daughter, who slipped through a prism into deeper sections of 4D space.

Status: the design docs are settled and the project skeleton exists (4D engine library, its tests, Godot project), with no engine or game code yet.

## Map

- `docs/` holds the design, in Chinese. Read the doc that owns a topic before changing code or content in it:
  - `docs/frame/README.md`: 4D geometry, content categories, cross-section rules, size norms, the engine/Godot split and its API groups.
  - `docs/gameplay.md`: controls, turning objects, seams, chirality, threats, puzzle types.
  - `docs/prologue.md`: prologue scenes and their puzzle chain.
  - `docs/story.md`: story and world.
  - `docs/style.md`: visual and audio style, interaction feel, how atmosphere changes with section depth.
- `engine/` is the 4D engine (`FourD`), a plain .NET library; `engine.tests/` holds its xUnit tests.
- `game/` is the Godot 4.7 project (assembly `Game`); adapter code goes in `game/adapter/`. `game/Game.sln` holds all three projects and maps Godot's ExportDebug/ExportRelease builds to the engine's Debug/Release.
- `archives/threejs-demo/` is the frozen three.js prototype (a Vite project). It is a reference for gameplay and interaction feel; new work goes to the Godot project.

## Architecture

Godot with C#, in three layers. `docs/frame/engine.md` holds the full rules and API groups.

- **4D engine**: a plain .NET project with no Godot reference. It owns all 4D state, 4D math, and cross-section rules; computes in `double`; advances only when the host calls its step with a time delta; leaves rendering, audio, input, and files to the host. Its public surface is the fixed API, and everything else is `internal`.
- **Adapter**: the only code that knows both sides. It turns Godot scenes into the engine's world description, and engine output into meshes, collision shapes, and signals.
- **Game layer**: Godot rendering, 3D characters, audio, UI, scripts, and editor tools. It reaches the engine through the adapter and leaves all 4D geometry to the engine.

Two rules shape every feature:

- Exact kinematics, no 4D dynamics: geometry, slicing, support, and sweep collisions follow exact formulas; nothing simulates inertia, tumbling, or stacking.
- Puzzle and story conditions read the world through the engine's generic queries (is it supported, is this spot occupied, does it have a slice in that section). A condition that needs something new gets a new generic query in the engine.

Engine tests run under `dotnet test` without Godot. Rewrite 4D math from the design, without translating the prototype. Test expectations come from exact geometric results that can be calculated by hand, geometric invariants, and formula-derived values in the size norms.

## Language and terms

- Code, comments, and commit messages are in English; docs and in-game text are in Chinese.
- One concept, one word, everywhere. The 术语 table in `docs/gameplay.md` is the vocabulary; a new concept gets its term there first.
- The docs use 截面 for two things that code keeps apart: `Section` is the 3D world at one W position, and `Slice` is the cut shape an object shows inside a section. (In the code principles below, "slice" means a slice of work.) 页 is the daughter's word for a section and appears only in game text.

## Code principles

- **Less is more.** Finish the task with the fewest lines, files, types, and concepts. First decide whether the change is needed at all; then reuse existing repository code, the .NET standard library, or built-in Godot features before writing new code. Delete or inline helpers, wrappers, and settings that have no current use.
- **KISS.** Write plain code a reader understands in one pass. Ordinary methods, data types, and explicit branches come before reflection, code generation, generic frameworks, or clever tricks.
- **Composition over inheritance.** Build behaviour by holding or receiving collaborators, and express module boundaries as interfaces. Abstract records model closed variant sets such as results, commands, and reference kinds; share code through composition rather than base classes or inheritance chains. Godot node scripts inherit node types as the engine requires; plain .NET types stay independent of `GodotObject`.
- **Information hiding** keeps modules decoupled. Each module exposes the smallest contract its callers need and keeps internal state, data structures, and algorithms `internal` or `private`. Callers depend on contracts rather than implementations, and pass other modules' opaque data, such as save payloads, through unread. A change to one module's internals should leave other modules untouched; when it does not, the contract exposes too much, so fix the contract.
- **YAGNI: spend no complexity on features nobody needs yet.** Implement only the behaviour the current issue and slice require. Add parameters, switches, extension points, and compatibility layers when a real need arrives. Optimise performance on profiling evidence.
- **Avoid over-engineering.** Keep indirection to what the current design needs, without generic frameworks or catch-all base classes. Use interfaces for module contracts and for boundaries whose implementation must be swappable, such as host ports and test doubles; a class with one implementation and no need to swap it stays concrete.
- **Code over comments.** Make code readable through naming and structure. Write a comment only for a reason the code cannot show, such as an ordering required for determinism, an engine or platform quirk, or a check that looks redundant.

## Agent workflow

Maintained with the `setup-agent-workflow` skill. Edit values freely; keep the field names.

- Base branch: `main`
- Decision notes: `.agents/notes/`, written in Chinese. Every decision record, ADRs included, is a decision note, written during planning with the `decision-notes` skill; work carries out accepted notes and never edits a note. Notes in `accepted/`, `rejected/`, and `archived/` are frozen and read-only: only the `decision-notes` skill's `note_lifecycle.py` changes them, on the user's word.
- Tracker: GitHub issues on `origin` (`odradekk/project_4d`), through `gh`. Tickets are referenced as `#N`, and a pull request closes one with `Fixes #N`. The tracker records blocking relations and labels natively. Tickets for decision notes follow the `note-to-tickets` skill. The pull request that completes a ticket carries the closing reference in its description, and its commits carry no ticket reference. Work committed straight to the base branch, without a pull request, carries the ticket reference in its commits and the closing reference in the one that completes the ticket.
- Labels: every issue and pull request gets exactly one `kind/*` label and every `area/*` label that applies. Use only the labels listed here; when none fits, refresh this section with the `setup-agent-workflow` skill.
  - `kind/feature`: a new user- or model-facing capability. `kind/bug-fix`: corrects a defect. `kind/simplification`: removes code, behavior, or surface without adding capability. `kind/architecture`: the structure of the shipped code. `kind/process`: tooling and workflow around the code. `kind/testing`: test infrastructure and strategy.
  - `area/engine`: `engine/**`, `engine.tests/**`. `area/adapter`: `game/adapter/**`. `area/game`: the rest of `game/**`. `area/docs`: `docs/**`, `AGENTS.md`.
- Prose: documents, decision notes, tickets, pull request descriptions, and commit bodies are revised with the `plain-language` skill. House style: the "Language and terms" rules above; terms come from the 术语 table in `docs/gameplay.md`.
- File size: files stay under 5,000 tokens (bytes / 4) so an agent can read them whole. `.agents/file-size.json` lists excluded paths, exceptions with their reasons, and the baseline of older files over the limit, which may shrink but not grow. The pre-commit hook runs `.agents/scripts/check_file_size.py`; PR CI also checks every tracked file with `--all`.
- Hooks: pre-commit runs `.agents/scripts/check_file_size.py`; commit-msg removes AI attribution lines and pre-push rejects commits that still credit an AI tool, both through `.agents/scripts/strip_attribution.py`; post-checkout, post-merge, and post-rewrite make frozen decision notes read-only again through `.agents/scripts/note_lifecycle.py`. `core.hooksPath` connects them in one clone only; in each new clone run `git config core.hooksPath .agents/hooks`.
- CI: GitHub Actions runs on every pull request: `dotnet build game/Game.sln`, `dotnet test engine.tests`, and `python .agents/scripts/check_file_size.py --all`. Any failure fails the check. Godot scene checks stay local.

| Change | Narrowest check |
|---|---|
| Behavior in `engine/**/*.cs`, `engine.tests/**/*.cs` | `dotnet test engine.tests --filter "FullyQualifiedName~<test class>"` |
| C# in `game/**`, or the engine's public API | `dotnet build game/Game.csproj` |
| Scenes in `game/`, `game/project.godot` (use the main scene) | `<godot> --headless --path game --scene res://<scene>.tscn --quit` |
| Imported assets in `game/` (textures, models, audio) | `<godot> --headless --path game --import` |
| `*.csproj`, `game/Game.sln`, `global.json` | `dotnet build game/Game.sln` |

`<godot>` is the console build of Godot 4.7.2 mono. It is not on PATH; on this machine it is `D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`. Godot exits 0 even when a scene fails to load, so a Godot check fails on any `ERROR:` line in its output. It loads C# scripts from the last `dotnet build game/Game.csproj`. Docs (`docs/**`, `AGENTS.md`) have no automated check, so review them by hand. `archives/threejs-demo/` is frozen and gets no check.
