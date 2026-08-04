# Contributing to CowBull

Thank you for helping modernize CowBull. Current development targets `CowBull.Modern.sln`; do not add features to the legacy .NET Framework solution. Legacy retirement is tracked in [issue #3](https://github.com/Yasma90/cowBull/issues/3).

## Issue-first workflow

Every change starts with a GitHub issue. Before writing code:

1. Search existing issues and avoid duplicates.
2. Create or claim an issue with a clear problem statement, scope, and verifiable acceptance criteria.
3. Discuss architecture or protocol changes in the issue before implementation.
4. Create a branch whose name includes the issue number.
5. Keep the pull request focused on that issue and link it with `Closes #<issue>` when appropriate.

The primary migration is tracked in [#1](https://github.com/Yasma90/cowBull/issues/1), WPF UI automation and measured performance baselines in [#2](https://github.com/Yasma90/cowBull/issues/2), and legacy retirement in [#3](https://github.com/Yasma90/cowBull/issues/3).

## Git Flow

- `master` contains production-ready history. Do not commit directly to it.
- `develop` is the integration branch for completed features.
- `feature/<issue>-description` branches from `develop` and returns to `develop`, for example `feature/42-reject-invalid-guess`.
- `release/<version>` branches from `develop` for release stabilization only. Merge the finished release into both `master` and `develop`, then tag it.
- `hotfix/<issue>-description` branches from `master` for an urgent production fix. Merge it into both `master` and `develop`.

Typical feature setup:

```powershell
git switch develop
git pull --ff-only
git switch -c feature/42-reject-invalid-guess
```

Use lowercase branch descriptions separated by hyphens. Rebase or merge the latest target branch according to the repository's current policy before requesting final review; never rewrite another contributor's published history.

## TDD: red, green, refactor

Use a short test-driven cycle for observable behavior:

1. **Red:** add the smallest test that describes the requirement and confirm it fails for the expected reason.
2. **Green:** implement the smallest production change that makes the test pass.
3. **Refactor:** improve names, duplication, and design while keeping all tests green.

Prefer fast Domain and Application unit tests. Use test doubles at Application ports, and reserve Infrastructure tests for framing, transport, serialization, and adapter boundaries. A bug fix must include a regression test. Run the affected project throughout development and the full canonical solution before pushing:

```powershell
dotnet test .\CowBull.Domain.Tests\CowBull.Domain.Tests.csproj
dotnet test .\CowBull.Application.Tests\CowBull.Application.Tests.csproj
dotnet build .\CowBull.Modern.sln --configuration Release --warnaserror
dotnet format .\CowBull.Modern.sln --verify-no-changes --no-restore
dotnet test .\CowBull.Modern.sln --configuration Release --no-build
```

Do not weaken an assertion, suppress a warning, or bypass an invariant merely to make the suite pass.

## Architecture and code quality

- Keep Domain code independent of WPF, networking, persistence, logging frameworks, and system time.
- Put orchestration in Application use cases and external capabilities behind narrow ports.
- Implement ports and TCP concerns in Infrastructure.
- Keep WPF projects responsible for composition and presentation; do not place authoritative game rules in view models or code-behind.
- Preserve server authority: only the server generates secrets, evaluates guesses, and changes the official session state.
- Validate data at trust boundaries and keep the 4-byte big-endian, bounded UTF-8 frame contract backward compatible unless the issue explicitly changes the protocol.
- Prefer clear names, small cohesive methods, immutable state where practical, cancellation for asynchronous I/O, and deterministic tests.
- Remove duplication only when behavior is covered. Do not mix unrelated cleanup into a feature pull request.
- Treat performance changes as measured work with an explicit baseline and acceptance target; coordinate that work through [issue #2](https://github.com/Yasma90/cowBull/issues/2).

## Commits

Write Conventional Commit messages in English using an imperative, concise subject:

```text
<type>(<scope>): <description>
```

Examples:

```text
feat(game): reject guesses with duplicate digits
fix(protocol): reject truncated payload frames
test(application): cover exhausted game attempts
refactor(server): extract the request handler
docs(contributing): document the Git Flow policy
```

Common types are `feat`, `fix`, `test`, `refactor`, `perf`, `docs`, `build`, and `ci`. Keep commits atomic and buildable. Use the commit body for motivation or migration notes, and add `Refs #42` when the issue is not closed by the pull request itself.

## Logging and secrets

Never commit or log secrets. This includes credentials, tokens, private keys, connection strings, the generated game secret number, and any payload that may contain sensitive data. Do not include secrets in exception messages, structured-log properties, screenshots, fixtures, or CI output.

Log event names and safe operational metadata instead. Use deterministic fake secret generators in tests, with obviously non-production values, and keep local configuration outside version control.

## Pull requests

Open feature pull requests against `develop`; use the Git Flow targets above for releases and hotfixes. A pull request is ready for review when:

- It links its issue and explains the user-visible or architectural outcome.
- The change is limited to the agreed acceptance criteria.
- New or changed behavior has tests developed through red-green-refactor.
- `CowBull.Modern.sln` restores, builds with warnings treated as errors, and passes all tests.
- Architecture boundaries and server authority remain intact.
- Protocol changes document compatibility and include boundary tests.
- User-facing behavior and contributor documentation are updated where necessary.
- UI changes include concise manual verification steps and screenshots when they help reviewers.
- No secrets, generated build output, test results, or unrelated formatting changes are included.

Respond to review with additional focused commits. Squash only when requested by the repository maintainer, and do not merge while required CI checks are failing.
