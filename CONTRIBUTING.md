# Contributing to TLio

Thanks for taking the time. Bug reports, fixes, new commands/functions and doc improvements are all welcome.

## Getting started

```sh
git clone https://github.com/SQUORA-NL/TLio.git
cd TLio
dotnet build
dotnet test
```

You need the .NET 10 SDK. Libraries target `net8.0;net9.0;net10.0`; see [docs/target-frameworks.md](docs/target-frameworks.md).
Use `fetch-depth: 0` / a full clone — the version comes from git tags (MinVer), so never add a version number to a project file ([docs/versioning.md](docs/versioning.md)).

## Before you open a pull request

1. **Open an issue first** for anything larger than a small fix, so we can agree on the shape.
2. Branch from `main`; one logical change per PR.
3. Add or update tests. Commands and functions are covered by file-based fixtures (input / script / result);
   behaviour that must hold in every format belongs in `TLio.Parity.Tests`.
4. `dotnet build -warnaserror` and `dotnet test` must pass — nullable warnings are errors.
5. Update the docs (`docs/ai-ref/`, the relevant README) when behaviour or the public surface changes.
6. If a command or function changes behaviour across formats on purpose, record it in `docs/behaviour-decisions.md`.

## Project conventions

Read [CLAUDE.md](CLAUDE.md) for the project layout and rules. The short version:

- Commands and functions are written against the JSON data model and know nothing about a concrete format;
  format specifics live in adapters (`INodeAdapter<TNode>`, `IItemsFetcher<TNode>`).
- A compiled script's commands are shared, concurrently executed singletons. Request-scoped state belongs on
  `IExecutionContext<TNode>`, never on a mutable property of a command.
- Follow the style of the surrounding code (naming, comment density, idiom).

## Commit messages and PRs

Short imperative subject line, a body that explains *why*. Reference the issue (`Fixes #123`). CI runs on Linux,
macOS and Windows; all three need to be green.

## Reporting security issues

Please do **not** open a public issue — see [SECURITY.md](SECURITY.md).

## Licence

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
