# Contributing to CLeARINET

Thanks for your interest in CLeARINET, an open source HTTP(S) inspection
proxy built as a spiritual successor to the Fiddler Web Debugger. This
document covers what to know before you open an issue or a pull request.

## Clean-room implementation policy

This is the most important rule in this repository, and it exists to keep
the project on solid legal footing.

Fiddler Classic, Fiddler Everywhere, and FiddlerCore are proprietary
software. CLeARINET is built independently, from:

- publicly available documentation (such as the [Fiddler Classic
  docs](https://github.com/telerik/fiddler-docs)),
- publicly released examples or sample code,
- publicly observed behavior (for example, the structure of `.saz` files
  produced by running the real tool and inspecting the output), and
- our own design decisions.

**Contributors must not decompile, disassemble, or reverse-engineer any
Fiddler binary, and must not copy code, comments, or other material from
Fiddler's source (if you have seen it under an NDA or similar) into this
project.** If you have prior exposure to Fiddler's proprietary source
code, please disclose that in your pull request so a maintainer can review
it with that in mind.

Compatibility with Fiddler's documented formats, command syntax, and
scripting hooks is a project goal. Reimplementing *documented, observed
behavior* in your own code is expected and encouraged; copying
implementation code is not.

## No-relicensing commitment

CLeARINET is licensed under the MIT License (see [LICENSE](LICENSE)).

No maintainer, sponsor, or acquirer gets to pull the project behind a paywall
or revoke rights already granted to users. This commitment is a key reason
why this project exists at all.

## Getting started

1. Fork the repository and clone your fork.
2. Install the [.NET SDK](https://dotnet.microsoft.com/download) matching
   the version in `global.json` (once one exists) or the current .NET LTS.
3. `dotnet build` from the repository root should succeed with no
   warnings-as-errors on Windows and macOS; CI checks both.
4. `dotnet test` should pass.

## Pull requests

- Keep pull requests focused on one change. Large, unrelated changes are
  harder to review and more likely to get stuck.
- Add or update tests for anything you change in the proxy core or the
  compatibility adapter.
- Reference the relevant open question or gap from the project plan when
  your change addresses one.
- Be prepared for feedback. Early on especially, the architecture is still
  being worked out, and review comments are how we keep it coherent.

## Reporting bugs and security issues

Open a regular issue for a bug. For a security-sensitive issue (for
example, anything involving the interception root certificate or private
key handling), please do not open a public issue — a `SECURITY.md` with
a private reporting channel will be added before 1.0; until then, contact
a maintainer directly.

## Code of conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By
participating, you're expected to uphold it.
