#!/usr/bin/env bash
# Runs one test project and fails unless at least the expected number of tests actually ran.
#
# Usage: dotnet-test-guarded.sh <minimum-expected-tests> <project> [extra dotnet test args...]
#
# A run that executes nothing is the failure mode this repo keeps hitting: a filter naming a test
# that was renamed or deleted, a suite that stopped being discovered, or a theory row dropped so the
# count falls silently. Under Microsoft.Testing.Platform the floor is enforced by the runner itself
# -- `--minimum-expected-tests` exits 9 when fewer ran, and a filter matching nothing exits 8 -- so
# the count is the guard, rather than scraping the log for a message the runner happens to print.
#
# The floor is the suite's real size, not 1. A suite that loses half its rows still ran "some"
# tests, and only a real number notices.

set -uo pipefail

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <minimum-expected-tests> <project> [extra args...]" >&2
  exit 64
fi

minimum=$1
project=$2
shift 2

dotnet test --project "$project" "$@" -- --minimum-expected-tests "$minimum"
status=$?

if [ "$status" -eq 9 ]; then
  echo "::error::Fewer than ${minimum} tests ran in ${project}; a test stopped being a test."
elif [ "$status" -eq 8 ]; then
  echo "::error::Zero tests ran in ${project} -- a filter matched nothing, or discovery failed."
fi

exit "$status"
