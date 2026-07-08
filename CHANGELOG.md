# Changelog

All notable changes to Morpheus will be documented in this file.

## [Unreleased]

### Fixed
- Fixed bare `catch { }` in `HelpModule.cs` that silently swallowed all exceptions
  when reading `RateLimitAttribute` field values. Now catches only
  `InvalidCastException` and `TargetException`.
- Removed dead code `ValidateDays` method from `LevelsModule.cs` that contained a
  synchronous blocking call (`GetAwaiter().GetResult()`) on an async method,
  which posed a deadlock risk.
- Fixed `InteractionsHandler` registering a separate Discord event handler for
  every registered interaction. Now uses a single router that dispatches to the
  correct handler by custom ID, preventing redundant handler invocations on
  every interaction event.

### Added
- Initial changelog file.