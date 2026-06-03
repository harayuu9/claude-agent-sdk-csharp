# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview.7] - 2026-06-03

> `0.1.0-preview.6` was published from an earlier state; the patch number is
> intentionally skipped so this release stays ahead of it.

### Breaking

- Moved the `Transport` abstract class from the `ClaudeAgentSdk.Internal.Transport`
  namespace to the root `ClaudeAgentSdk` namespace and promoted it to a supported
  public extension point. Custom transport implementations should drop
  `using ClaudeAgentSdk.Internal.Transport;` — the type is now visible via
  `using ClaudeAgentSdk;`.

### Added

- `StreamJsonParser`: a public utility that parses Claude Code `stream-json` output
  (partial-JSON buffering, embedded-newline splitting, non-JSON line skipping) from
  any `IAsyncEnumerable<string>` source. Reusable by custom transports — for example
  a WebSocket/vsock relay of a remote `claude --output-format=stream-json` — so that
  local and remote execution converge on the same `IAsyncEnumerable<Message>`.

### Changed

- `Transport` is now documented as a stable extension point; the previous
  "internal API, may change or be removed" warning has been removed.
- The built-in subprocess transport now parses stdout via `StreamJsonParser`,
  removing duplicated buffering logic. Behavior is unchanged.
