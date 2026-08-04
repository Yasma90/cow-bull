# CowBull

CowBull is a client/server Bulls and Cows game being modernized on .NET 10 with Clean Architecture, test-driven development, and a server-authoritative game model. The supported entry point for current development is `CowBull.Modern.sln`.

## Platform decision

The modern code targets **.NET 10 LTS**. It was selected instead of .NET 8 because this migration is establishing a new long-lived baseline, and .NET 10 provides the later [LTS support window](https://dotnet.microsoft.com/platform/support/policy). The repository pins SDK `10.0.302` in `global.json` so local and CI builds use the same toolchain policy.

The WPF applications require Windows. Install the .NET 10 SDK (or a compatible patch allowed by `global.json`) before building.

## Architecture

The layers, from the stable core to the delivery mechanism, are:

```text
Domain -> Application -> Infrastructure -> WPF hosts
```

Compile-time dependencies point inward:

```text
CowBullClient.Modern / CowBullServer.Modern
                    |
                    v
          CowBull.Infrastructure
                    |
                    v
           CowBull.Application
                    |
                    v
             CowBull.Domain
```

| Layer | Responsibility | Dependency rule |
| --- | --- | --- |
| `CowBull.Domain` | Game rules, state, scoring, and domain invariants | Depends on no outer layer or UI/network framework |
| `CowBull.Application` | Use cases and ports for IDs, persistence, and secret generation | Depends on Domain |
| `CowBull.Infrastructure` | TCP framing, transport configuration, and adapters for Application ports | Depends inward on Application/Domain abstractions |
| `CowBullServer.Modern` | WPF server host and composition root | Wires Application use cases to Infrastructure |
| `CowBullClient.Modern` | WPF client host and presentation | Sends player intent and renders server responses |

Tests mirror and protect these boundaries. Domain tests exercise rules without I/O, Application tests exercise use cases through test doubles, Infrastructure tests cover adapters and protocol behavior, Presentation tests cover view models and client/server integration, and Architecture tests enforce the allowed project references.

### Server-authoritative gameplay

The server owns each game session. It generates and retains the secret number, validates guesses, applies attempt limits, calculates bulls and cows, and decides whether a game has been won or lost. The client submits commands and displays the server's response; it must not calculate authoritative outcomes or receive the secret during an active game.

This boundary keeps the game rules in one place and makes them independently testable. Authoritative rules belong in Domain and Application. Client presentation sends player intent through its client port and never duplicates those rules in a view model or code-behind file.

## TCP message framing

TCP is a byte stream, so every application message uses one bounded frame:

```text
+----------------------------+-------------------------------+
| 4-byte unsigned length     | exactly N UTF-8 payload bytes |
| (big-endian/network order) |                               |
+----------------------------+-------------------------------+
```

`LengthPrefixedUtf8Protocol` reads the complete header and payload, uses strict UTF-8, and rejects truncated, invalid, or oversized frames. The default payload limit is 64 KiB; endpoints may configure a lower or higher limit up to the hard 16 MiB ceiling. The framing layer provides message boundaries only; it is not an encryption or identity mechanism.

## Build and test

Run all commands from the repository root. `CowBull.Modern.sln` is the canonical solution used by CI.

```powershell
dotnet restore .\CowBull.Modern.sln
dotnet build .\CowBull.Modern.sln --configuration Release --no-restore --warnaserror
dotnet format .\CowBull.Modern.sln --verify-no-changes --no-restore
dotnet test .\CowBull.Modern.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory .\TestResults --logger trx
```

The GitHub Actions workflow runs restore, a warnings-as-errors Release build, formatting verification, and the test suite with coverage on Windows. For a shorter local TDD loop, target the affected test project first, then run the complete solution before opening a pull request.

## Run the applications

Start the server before the client. Use two PowerShell terminals from the repository root.

Terminal 1:

```powershell
dotnet run --project .\CowBullServer.Modern\CowBullServer.Modern.csproj
```

Start the server from its window; the default endpoint is `127.0.0.1:4510`.

Terminal 2:

```powershell
dotnet run --project .\CowBullClient.Modern\CowBullClient.Modern.csproj
```

Connect from the client window, start a game, and submit four-digit guesses. Stop the client and server cleanly before starting another local session on the same port.

## Modernization roadmap

- [Issue #1](https://github.com/Yasma90/cowBull/issues/1) tracks the .NET 10 and Clean Architecture migration.
- [Issue #2](https://github.com/Yasma90/cowBull/issues/2) tracks WPF UI automation and measured performance baselines after the architecture is stable.
- [Issue #3](https://github.com/Yasma90/cowBull/issues/3) tracks retirement of the legacy solution and duplicated legacy code.

### Legacy notice

`CowBull.sln` and its .NET Framework projects are **legacy**. They are retained temporarily for migration reference, are not the canonical build, and should not receive new features. Their removal is pending [issue #3](https://github.com/Yasma90/cowBull/issues/3).

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before starting work. Contributions use issue-first Git Flow, English Conventional Commits, TDD, and pull requests into the appropriate integration branch.
