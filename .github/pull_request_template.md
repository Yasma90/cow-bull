## Summary

Describe the user-visible outcome and the architectural decisions made.

Closes #

## Change type

- [ ] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Test or build improvement

## Verification

- [ ] Tests were written first or updated with the behavior (red-green-refactor).
- [ ] `dotnet build CowBull.Modern.sln --configuration Release --no-restore --warnaserror` passes.
- [ ] `dotnet format CowBull.Modern.sln --verify-no-changes --no-restore` passes.
- [ ] `dotnet test CowBull.Modern.sln --configuration Release --no-build` passes.
- [ ] Domain and application code remain independent of UI and infrastructure details.
- [ ] Public behavior and migration notes are documented where necessary.
- [ ] Commits use English Conventional Commit messages.
- [ ] The branch follows the applicable Git Flow pattern: `feature/<issue>-<description>`, `release/<version>`, or `hotfix/<issue>-<description>`.
