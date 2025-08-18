# Contributing to Morpheus

Thanks for wanting to contribute! This guide covers the common workflows to get the project running locally, the expectations for changes, and a few practical tips (including regenerating `COMMANDS.md`).

## Quick start

1. Fork the repo and create a feature branch.
2. Run the build to ensure nothing is broken:

```powershell
# from repository root
dotnet build
```

3. When changing command code, regenerate `COMMANDS.md` (see below) and commit it.

## Development setup

- Requires .NET 8 SDK and a Python 3 interpreter on your PATH.
- Optional: `dotnet-ef` tools if you will add migrations.

Install dotnet-ef (if needed):

```powershell
dotnet tool install --global dotnet-ef
```

## Running the command list generator

This repository includes a small Python script that extracts command metadata from the C# modules and writes `COMMANDS.md`.

```powershell
python .\tools\generate_commands_md.py
```

If you update or add commands, regenerate `COMMANDS.md` and include the updated file in your PR.

## Migrations and database changes

- Do not edit historical migration files unless you know what you're doing and the migration has not been applied anywhere important.
- To change the EF Core schema, create a new migration and include it in your PR:

```powershell
# create a new migration
dotnet ef migrations add <DescriptiveName>
# apply locally
dotnet ef database update
```

If the change needs a manual database migration for production, explain it in the PR description.

## Coding style and expectations

- Language: C# targeting .NET 8.
- Use the project's existing style for naming, nullability annotations, and dependency injection patterns.
- Prefer small, focused PRs. Unit tests are not required, but are nice to have. 
- If you change public behavior (commands, DB schema, bot permissions), document it in the PR description.

## Running tests

There are no project-wide unit tests mandated by the repository, but if you add tests run:

```powershell
dotnet test
```

## Submitting a PR

- Rebase or merge from `main` before opening the PR.
- Make sure `dotnet build` succeeds and `COMMANDS.md` is up to date.
- In your PR description include what you changed, why, and any migration steps or deploy notes.

## Reporting issues

If you find a bug or want to request a feature, open an issue and include:
- A short title and description
- Steps to reproduce (or expected behavior)
- Any relevant logs or error messages

## Helpful notes for maintainers

- The command generator is intentionally simple (regex-based).
- Avoid editing old migrations that have been released; prefer to add a migration that alters schema forward.

---
Thanks again for contributing!
