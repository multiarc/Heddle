# Security Policy

## Supported versions

Security fixes are provided for the latest released `4.x` line. Please make sure you can
reproduce an issue against the most recent release before reporting.

| Version | Supported |
| --- | --- |
| 4.x (latest) | ✅ |
| < 4.0 | ❌ |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions,
or pull requests.**

Instead, use one of the private channels:

1. **Preferred:** GitHub's private vulnerability reporting — open the repository's
   **Security** tab and click **"Report a vulnerability"**
   (<https://github.com/multiarc/Heddle/security/advisories/new>).
2. **Email:** **multiarc@gmail.com** with the subject line `Heddle security report`.

Please include:

- a description of the vulnerability and its impact,
- the affected version(s) and platform/runtime,
- steps to reproduce or a proof-of-concept,
- any suggested mitigation, if known.

## What to expect

- **Acknowledgement** of your report within **5 business days**.
- An assessment and, where confirmed, a plan and timeline for a fix.
- Coordinated disclosure: we will agree on a disclosure date with you and credit you in the
  advisory unless you prefer to remain anonymous.

Please act in good faith and give us reasonable time to address the issue before any public
disclosure.

## Scope note

Heddle compiles templates (including embedded C# expressions) into executable code. It is
designed for **first-party, trusted templates** and does **not** sandbox template input.
Rendering untrusted or user-supplied templates is outside the intended threat model and is
not, by itself, considered a vulnerability. See the documentation for details.
