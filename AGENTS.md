# AGENTS.md

A first-person Chinese cosmic-horror game about four-dimensional beings: PS1-style low poly, China in 2000, dread without jump scares. A father searches for his daughter, who slipped through a prism into deeper sections of 4D space.

Status: design phase. The design docs are settled; the Godot project and the C# 4D engine are not created yet.

## Map

- `docs/` holds the design, in Chinese. Read the doc that owns a topic before changing code or content in it:
  - `docs/framework.md`: 4D geometry, content categories, cross-section rules, size norms, the engine/Godot split and its API groups.
  - `docs/gameplay.md`: controls, turning objects, seams, chirality, threats, puzzle types.
  - `docs/prologue.md`: prologue scenes and their puzzle chain.
  - `docs/story.md`: story and world.
  - `docs/style.md`: visual and audio style, interaction feel, how atmosphere changes with section depth.
- `archives/threejs-demo/` is the frozen three.js prototype (a Vite project). It is the reference implementation for ported 4D math; new work goes to the Godot project.

## Architecture

Godot with C#, in three layers. `docs/framework.md` (引擎模块) holds the full rules and API groups.

- **4D engine**: a plain .NET project with no Godot reference. It owns all 4D state, 4D math, and cross-section rules; computes in `double`; advances only when the host calls its step with a time delta; leaves rendering, audio, input, and files to the host. Its public surface is the fixed API, and everything else is `internal`.
- **Adapter**: the only code that knows both sides. It turns Godot scenes into the engine's world description, and engine output into meshes, collision shapes, and signals.
- **Game layer**: Godot rendering, 3D characters, audio, UI, scripts, and editor tools. It reaches the engine through the adapter and leaves all 4D geometry to the engine.

Two rules shape every feature:

- Exact kinematics, no 4D dynamics: geometry, slicing, support, and sweep collisions follow exact formulas; nothing simulates inertia, tumbling, or stacking.
- Puzzle and story conditions read the world through the engine's generic queries (is it supported, is this spot occupied, does it have a slice in that section). A condition that needs something new gets a new generic query in the engine.

Engine tests run under `dotnet test` without Godot. Ported math is checked against the archived prototype's output for the same inputs.

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
