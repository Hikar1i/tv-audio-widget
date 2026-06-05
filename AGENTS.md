# AGENTS.md

## Project Overview

This repository is currently an empty starting point for `tv-audio-widget`.

Agents working in this repo should first inspect the project structure before making assumptions about the stack, build system, or runtime.

## Working Guidelines

- Keep changes focused on the user's request.
- Prefer existing project conventions once files and tooling are added.
- Do not introduce large frameworks, dependencies, or generated files without a clear need.
- Avoid overwriting user changes. Check the working tree before editing files that may have been modified.
- Use ASCII by default unless the project already uses non-ASCII content or the task requires it.

## Git Guidelines

- Review `git status` before and after edits.
- Do not run destructive Git commands such as `git reset --hard` or `git checkout --` unless explicitly requested.
- If creating branches, use the `codex/` prefix unless the user requests a different naming convention.

## Verification

When project tooling exists, run the smallest relevant verification command after changes, such as tests, linting, type checks, or a local build.

If no tooling exists yet, state that verification was limited to file and Git checks.
