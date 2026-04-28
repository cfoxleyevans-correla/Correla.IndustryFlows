# AGENTS.md

Operating instructions for AI coding agents working in the **Correla.IndustryFlows** repository.
These rules are binding. If a rule conflicts with a user request, pause and confirm with the operator before proceeding.

---

## 1. Code Style & Language Standards

- Write **clean, idiomatic C#** targeting the latest stable .NET (currently `net10.0`).
- Follow the latest **Microsoft C# coding conventions** and **.NET design guidelines**:
  - https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions
  - https://learn.microsoft.com/dotnet/standard/design-guidelines/
- Prefer **modern language features** where they improve clarity (file-scoped namespaces, primary constructors, collection expressions, `required` members, pattern matching, `nullable` enabled).
- **Simple and verbose beats short and exotic.** If a junior developer would have to stop and decode it, rewrite it.
- **Nullable reference types must be enabled** and respected — no `!` null-forgiving operator without a justified comment.
- Async all the way down. No `.Result`, no `.Wait()`, no `async void` (except event handlers).

## 2. Comments

- Comment **what** the code does, never **why** it was written or its history.
- Every non-trivial method, class, and branch gets a short comment describing behaviour.
- Public APIs get XML doc comments (`///`).
- Do **not** leave TODO/FIXME/HACK markers — either fix it now or raise it with the operator.

## 3. Abstractions

- **Lightweight abstractions only.** Reach for an interface, generic, or layer **only** when it earns its keep.
- Before introducing **any** new abstraction (interface, base class, mediator, factory, wrapper, DI extension, etc.):
  1. **Pitch it to the operator** in plain English.
  2. State the concrete problem it solves.
  3. State the cost (indirection, files, mental load).
  4. **Wait for explicit approval** before writing the code.
- Default to concrete types and direct calls. YAGNI applies.

## 4. Shared Code

- All code reused by more than one project lives in **`Correla.IndustryFlows.Shared`**.
- Do not duplicate types, helpers, DTOs, or constants across projects — promote them to `Shared`.
- `Shared` must have **no dependency** on Api, Web, or Infrastructure projects. It is a leaf.

## 5. README.md

A `README.md` at the repository root must be **kept current at all times**. It must contain, in this order:

1. **Quick Start** — clone, restore, build, run, test (copy-pasteable commands).
2. **Architecture** — a short description of the projects, their responsibilities, and how they fit together. Include a diagram (Mermaid) when it helps.
3. **Repo Map** — a tree of the top-level folders/projects with a one-line description of each.

Any change that adds a project, renames a folder, changes the run/test commands, or shifts architecture **must** update `README.md` in the same change set.

## 6. Workflow — Phased Delivery

Every non-trivial task is delivered in **phases**. For each phase:

1. **Plan** — break the work into phases up front, list them, and present to the operator.
2. **Implement** the current phase only.
3. **Run `dotnet format`** — must pass clean.
4. **Run `dotnet test`** — must be green.
5. **Stop and request operator review.** Do not start the next phase until approved.

Phases should be small enough to review in one sitting.

## 7. TDD — Red, Green, Refactor

We follow **strict TDD**:

1. **Red** — write the smallest failing test that captures the next slice of behaviour. Run it. Confirm it fails for the right reason.
2. **Green** — write the **minimum** production code to make it pass. No gold-plating.
3. **Refactor** — clean up production and test code with tests staying green.

Tests are first-class code: same standards, same review, same comments-for-what.

## 8. Dead Code

- **Remove dead code immediately.** No commented-out blocks, no unused usings, no orphan files, no unreferenced members.
- If you remove a public API, mention it in the phase summary.
- Source control is the history — the codebase is not.

## 9. Definition of Done (per phase)

- [ ] New behaviour covered by tests written test-first.
- [ ] `dotnet format` clean.
- [ ] `dotnet test` green.
- [ ] No dead code, no TODOs, no unused usings.
- [ ] `README.md` updated if anything user-visible changed.
- [ ] Shared code lives in `Correla.IndustryFlows.Shared`.
- [ ] Any new abstraction was pitched and approved.
- [ ] Operator review requested.

---

**When in doubt: stop and ask.** A short clarifying question is always cheaper than the wrong implementation.

