#!/usr/bin/env bash
# Run Stryker.NET and parse the results. Stryker output goes to a developer-visible
# terminal window only — nothing is piped to the agent's context.
#
# Usage (from MEditService/):
#   bash ../.claude/skills/mutation-test/run.sh
#   bash ../.claude/skills/mutation-test/run.sh --all              # same scope (since disabled)
#   bash ../.claude/skills/mutation-test/run.sh --file ConflictClassifier.cs
#   bash ../.claude/skills/mutation-test/run.sh --mutant-ids 42 57
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="stryker-config.json"
PARSE="python3 $SCRIPT_DIR/parse-report.py"

# --- arg parsing ---
MUTANT_IDS=()
FILE_FILTER=""
ALL=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        --all) ALL=true; shift ;;
        --file) shift; FILE_FILTER="$1"; shift ;;
        --mutant-ids)
            shift
            while [[ $# -gt 0 && "$1" != --* ]]; do
                MUTANT_IDS+=("$1"); shift
            done
            ;;
        *) shift ;;
    esac
done

# --- optional config patch for --mutant-ids ---
ORIGINAL_CONFIG=""
restore_config() {
    if [[ -n "$ORIGINAL_CONFIG" ]]; then
        echo "$ORIGINAL_CONFIG" > "$CONFIG"
    fi
}
trap restore_config EXIT

if [[ ${#MUTANT_IDS[@]} -gt 0 ]]; then
    echo "Targeted run — mutant IDs: ${MUTANT_IDS[*]}"
    ORIGINAL_CONFIG=$(cat "$CONFIG")
    # Inject mutant-id array into config
    ID_JSON=$(printf '%s\n' "${MUTANT_IDS[@]}" | python3 -c "import json,sys; print(json.dumps([int(l) for l in sys.stdin]))")
    python3 -c "
import json, sys
cfg = json.load(open('$CONFIG'))
cfg['stryker-config']['mutant-id'] = $ID_JSON
with open('$CONFIG', 'w') as f:
    json.dump(cfg, f, indent=2)
    f.write('\n')
"
elif [[ "$ALL" == "true" ]]; then
    echo "Scope: all of MEditService.Core (since disabled)"
    ORIGINAL_CONFIG=$(cat "$CONFIG")
    python3 -c "
import json
cfg = json.load(open('$CONFIG'))
cfg['stryker-config']['since']['enabled'] = False
with open('$CONFIG', 'w') as f:
    json.dump(cfg, f, indent=2)
    f.write('\n')
"
elif [[ -n "$FILE_FILTER" ]]; then
    echo "Scope: $FILE_FILTER (since: changes vs main)"
else
    # When HEAD == main (all changes uncommitted), git diff main..HEAD is empty and
    # Stryker's `since` filter finds nothing. Detect this and fall back to the
    # working-tree diff so uncommitted Core changes are still covered.
    SINCE_TARGET=$(python3 -c "import json; cfg=json.load(open('$CONFIG')); print(cfg['stryker-config']['since']['target'])" 2>/dev/null || echo "main")
    COMMITTED_CHANGES=$(git diff --name-only "${SINCE_TARGET}..HEAD" -- "*.cs" 2>/dev/null || true)
    if [[ -z "$COMMITTED_CHANGES" ]]; then
        # Nothing committed ahead of target — fall back to working-tree diff
        WORKTREE_CORE_CHANGES=$(git diff --name-only "$SINCE_TARGET" 2>/dev/null | grep "MEditService.Core/.*\.cs$" || true)
        WORKTREE_CORE_NEW=$(git ls-files --others --exclude-standard 2>/dev/null | grep "MEditService.Core/.*\.cs$" || true)
        WORKTREE_CHANGES=$(printf '%s\n%s' "$WORKTREE_CORE_CHANGES" "$WORKTREE_CORE_NEW" | grep -v '^$' | sort -u || true)
        if [[ -n "$WORKTREE_CHANGES" ]]; then
            echo "Scope: uncommitted Core changes vs ${SINCE_TARGET} (since disabled, explicit mutate list)"
            ORIGINAL_CONFIG=$(cat "$CONFIG")
            MUTATE_PATTERNS=$(echo "$WORKTREE_CHANGES" | sed 's|.*/MEditService.Core/|**/|' | awk '{print "\"" $0 "\""}' | paste -sd, -)
            python3 -c "
import json
cfg = json.load(open('$CONFIG'))
cfg['stryker-config']['since']['enabled'] = False
cfg['stryker-config']['mutate'] = [$MUTATE_PATTERNS]
with open('$CONFIG', 'w') as f:
    json.dump(cfg, f, indent=2)
    f.write('\n')
"
        else
            echo "Scope: files changed vs ${SINCE_TARGET} (no changes detected — running full corpus)"
            ORIGINAL_CONFIG=$(cat "$CONFIG")
            python3 -c "
import json
cfg = json.load(open('$CONFIG'))
cfg['stryker-config']['since']['enabled'] = False
with open('$CONFIG', 'w') as f:
    json.dump(cfg, f, indent=2)
    f.write('\n')
"
        fi
    else
        echo "Scope: files changed vs ${SINCE_TARGET} (use --all for full corpus)"
    fi
fi

# --- build stryker args ---
STRYKER_ARGS=(--config-file "$CONFIG")
if [[ -n "$FILE_FILTER" ]]; then
    STRYKER_ARGS+=(--mutate "**/$FILE_FILTER")
fi

# --- run stryker; open a developer-visible terminal ---

# Record start time so we can identify the newly-generated report (not a stale one)
RUN_START=$(date +%s)

# Stryker writes to the TTY (not stdout/stderr), so use `script` to provide a
# pseudo-TTY and capture output into a log the terminal window can tail.
TTY_LOG=$(mktemp /tmp/stryker_tty_XXXXXX.log)
trap 'restore_config; rm -f "$TTY_LOG"' EXIT

STRYKER_CMD="dotnet stryker ${STRYKER_ARGS[*]}"
script -q -c "$STRYKER_CMD" "$TTY_LOG" >/dev/null 2>&1 &
STRYKER_PID=$!

# Open a terminal following the tty log so the user can watch progress
TAIL_CMD="tail -n +1 -f '$TTY_LOG'"
if command -v cosmic-term &>/dev/null; then
    cosmic-term -- bash -c "$TAIL_CMD" 2>/dev/null &
elif command -v gnome-terminal &>/dev/null; then
    gnome-terminal --title="Stryker Mutation Test" -- bash -c "$TAIL_CMD" 2>/dev/null &
elif command -v xterm &>/dev/null; then
    xterm -T "Stryker Mutation Test" -e "bash -c \"$TAIL_CMD\"" 2>/dev/null &
fi

# Wait for stryker; capture exit code without failing the script
wait "$STRYKER_PID" && STRYKER_RC=0 || STRYKER_RC=$?

# Append completion banner so the open terminal shows it and the user knows it's done
printf '\n================================================================\n' >> "$TTY_LOG"
printf '  Stryker complete (exit %s) — safe to close this window\n' "$STRYKER_RC" >> "$TTY_LOG"
printf '================================================================\n' >> "$TTY_LOG"

echo "Stryker finished (exit $STRYKER_RC)."

# --- find the report generated by this run (not a stale one) ---
REPORT=$(python3 - <<EOF
import glob, os
from pathlib import Path
reports = glob.glob("StrykerOutput/**/mutation-report.json", recursive=True)
fresh = [r for r in reports if os.path.getmtime(r) >= $RUN_START]
print(max(fresh, key=os.path.getmtime) if fresh else "")
EOF
)

if [[ -z "$REPORT" ]]; then
    echo "ERROR: Stryker did not produce a new report (exit $STRYKER_RC). Check the terminal window for details." >&2
    exit 2
fi

echo "Report: $REPORT"

# --- parse and print filtered results ---
$PARSE "$REPORT"
