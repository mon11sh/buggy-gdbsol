
#!/usr/bin/env bash

# Backend build automation script

set -o pipefail

APP_NAME="GDB Backend"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

PROJECT_DIR="$SCRIPT_DIR"
LOG_FILE="$SCRIPT_DIR/logs/backend-build.log"
BUILD_MODE="Release"
RUN_TESTS="true"

# Read command-line parameters
while [[ $# -gt 0 ]]; do
    case "$1" in
        --project-dir)
            PROJECT_DIR="$2"
            shift 2
            ;;
        --log-file)
            LOG_FILE="$2"
            shift 2
            ;;
        --mode)
            BUILD_MODE="$2"
            shift 2
            ;;
        --run-tests)
            RUN_TESTS="$2"
            shift 2
            ;;
        *)
            echo "Unknown parameter: $1"
            exit 2
            ;;
    esac
done

# Validate parameters
if [[ ! -d "$PROJECT_DIR" ]]; then
    echo "ERROR: Project directory not found: $PROJECT_DIR"
    exit 1
fi

if [[ "$BUILD_MODE" != "Debug" && "$BUILD_MODE" != "Release" ]]; then
    echo "ERROR: Mode must be Debug or Release"
    exit 2
fi

if [[ "$RUN_TESTS" != "true" && "$RUN_TESTS" != "false" ]]; then
    echo "ERROR: Run-tests must be true or false"
    exit 2
fi

SOLUTION="$PROJECT_DIR/gdb-service-dotnet.slnx"

if [[ ! -f "$SOLUTION" ]]; then
    echo "ERROR: Solution file not found: $SOLUTION"
    exit 1
fi

mkdir -p "$(dirname "$LOG_FILE")" || exit 1

# Log the execution details
{
    echo "========================================"
    echo "Application: $APP_NAME"
    echo "Timestamp: $(date '+%Y-%m-%d %H:%M:%S %z')"
    echo "Project: $PROJECT_DIR"
    echo "Build mode: $BUILD_MODE"
    echo "Run tests: $RUN_TESTS"
    echo "========================================"
} | tee -a "$LOG_FILE"

# Run a command, log its output, and preserve its exit code
run_step() {
    local step_name="$1"
    shift

    echo "Starting: $step_name" | tee -a "$LOG_FILE"

    "$@" 2>&1 | tee -a "$LOG_FILE"
    local result="${PIPESTATUS[0]}"

    echo "$step_name exit code: $result" | tee -a "$LOG_FILE"
    return "$result"
}

# Restore dependencies
if ! run_step "Restore" dotnet restore "$SOLUTION"; then
    echo "RESULT: FAILED during restore" | tee -a "$LOG_FILE"
    exit 1
fi

# Compile the backend
if ! run_step "Build" dotnet build "$SOLUTION" \
    --configuration "$BUILD_MODE" --no-restore; then
    echo "RESULT: FAILED during build" | tee -a "$LOG_FILE"
    exit 1
fi

# Run tests when requested
if [[ "$RUN_TESTS" == "true" ]]; then
    if ! run_step "Tests" dotnet test "$SOLUTION" \
        --configuration "$BUILD_MODE" --no-build; then
        echo "RESULT: FAILED during tests" | tee -a "$LOG_FILE"
        exit 1
    fi
else
    echo "Tests skipped by parameter" | tee -a "$LOG_FILE"
fi

echo "RESULT: SUCCESS" | tee -a "$LOG_FILE"
exit 0
