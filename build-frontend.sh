
#!/usr/bin/env bash

set -o pipefail

APP_NAME="GDB Frontend"
PROJECT_DIR="${PROJECT_DIR:-frontend}"
LOG_DIR="${LOG_DIR:-logs}"
LOG_FILE="${LOG_FILE:-$LOG_DIR/frontend-build.log}"
BUILD_MODE="${BUILD_MODE:-production}"

mkdir -p "$LOG_DIR" || exit 1

exec > >(tee -a "$LOG_FILE") 2>&1

echo "========================================"
echo "Application: $APP_NAME"
echo "Timestamp: $(date '+%Y-%m-%d %H:%M:%S %z')"
echo "Project: $PROJECT_DIR"
echo "Build mode: $BUILD_MODE"
echo "========================================"

if [[ ! -d "$PROJECT_DIR" ]]; then
    echo "ERROR: Frontend directory not found: $PROJECT_DIR"
    echo "RESULT: FAILED"
    exit 1
fi

if [[ ! -f "$PROJECT_DIR/package.json" ]]; then
    echo "ERROR: package.json not found"
    echo "RESULT: FAILED"
    exit 1
fi

if [[ ! -f "$PROJECT_DIR/package-lock.json" ]]; then
    echo "ERROR: package-lock.json not found; cannot run npm ci"
    echo "RESULT: FAILED"
    exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
    echo "ERROR: npm is not installed or not in PATH"
    echo "RESULT: FAILED"
    exit 1
fi

run_step() {
    local step_name="$1"
    shift

    echo "Starting: $step_name"
    "$@"
    local exit_code=$?

    echo "$step_name exit code: $exit_code"

    if [[ $exit_code -ne 0 ]]; then
        echo "ERROR: $step_name failed"
        echo "RESULT: FAILED"
        exit "$exit_code"
    fi
}

run_step "Install dependencies" npm --prefix "$PROJECT_DIR" ci
echo "SKIPPED: Lint requires an ESLint configuration file"
run_step "Build" npm --prefix "$PROJECT_DIR" run build

echo "RESULT: SUCCESS"
