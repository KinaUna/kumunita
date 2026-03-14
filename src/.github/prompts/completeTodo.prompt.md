---
name: completeTodo
---
Search the entire workspace for TODO, FIXME, and HACK comments. For each one found:

1. List all discovered items grouped by file, showing the line number and comment text.
2. Evaluate each item for:
   - **Actionability** — can it be implemented now with available context?
   - **Impact** — does it fix a bug, close a security gap, or complete missing behaviour?
   - **Scope** — is it self-contained, or does it require large cross-cutting changes?
3. Select the single most impactful, actionable, and self-contained item to complete. Explain the choice briefly.
4. Implement the fix:
   - Follow the existing code conventions and patterns in the file and surrounding module.
   - Remove the TODO/FIXME/HACK comment once the implementation is in place.
   - If the fix requires new types, exceptions, or helper methods, add them in the appropriate location following project conventions.
5. Build the workspace and resolve any compilation errors before finishing.
