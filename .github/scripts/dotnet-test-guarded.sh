#!/usr/bin/env bash
# Runs one test project under Microsoft.Testing.Platform, with the two guards that are worth having.
#
# Usage: dotnet-test-guarded.sh <project> [extra dotnet test args...]
#
# `--fail-skips on` turns a skipped test into a failed one. A skip is a test that stopped testing, and
# CI is the wrong place to find out quietly; the repository's protocol for a known-failing test is
# `[Fact(Explicit = true)]`, which is reported as NOT RUN rather than skipped and so survives this.
#
# Exit 8 -- a filter matched nothing, or discovery failed -- stays called out by name, because a stale
# filter that runs nothing used to pass. It is the platform's own signal, not a number anyone maintains.
#
# What this script no longer does is carry a per-suite `--minimum-expected-tests` floor. Nine of them
# were maintained by hand across two workflows and they failed at their own job: one commit added six
# tests, raised the floor in one workflow and left the other, and that leg then tolerated a six-test
# regression in silence for four commits. A floor is satisfied by editing a digit, it names nothing
# when it reddens, and it fails a leg for the wrong reason whenever a test is legitimately quarantined.
# Suite membership is gated instead by the checked-in test-class inventories (src/<Suite>/test-classes.txt,
# asserted by set equality inside each suite), which name the class that appeared or vanished.

set -uo pipefail

if [ "$#" -lt 1 ]; then
  echo "usage: $0 <project> [extra args...]" >&2
  exit 64
fi

project=$1
shift 1

dotnet test --project "$project" "$@" -- --fail-skips on
status=$?

if [ "$status" -eq 8 ]; then
  echo "::error::Zero tests ran in ${project} -- a filter matched nothing, or discovery failed."
fi

exit "$status"
