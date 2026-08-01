#!/usr/bin/env bash
# Runs `dotnet test` with the arguments given, and fails the step if xUnit dropped a theory
# row as a duplicate.
#
# xUnit drops a theory row whose arguments duplicate another row's, logging "Skipping test
# case with duplicate ID" and leaving the run green at a lower count. `Skipped:` still reads
# 0, because the row is dropped rather than skipped, and the empty-run guard in
# tests.runsettings cannot see it either — the filter still matches tests. The log line is
# the only signal there is.
#
# A test run that fails on its own merits still gets checked, and still reports its own exit
# code: a dropped row is worth knowing about even in a run that was going red anyway.

set -uo pipefail

log=$(mktemp)
trap 'rm -f "$log"' EXIT

status=0
dotnet test "$@" 2>&1 | tee "$log" || status=$?

if grep -q "duplicate ID" "$log"; then
  echo "::error::A test case was dropped as a duplicate; a theory row is not a test until the count says so."
  grep "duplicate ID" "$log"
  exit 1
fi

exit "$status"
