#!/usr/bin/env bash

set -o pipefail

LOG_DIR="logs"
LOG_FILE="$LOG_DIR/build-all.log"

mkdir -p "$LOG_DIR" || exit 1

exec > >(tee -a "$LOG_FILE") 2>&1

echo "========================================"
echo "Application: GDB Full Stack Build"
echo "Timestamp: $(date '+%Y-%m-%d %H:%M:%S %z')"
echo "========================================"

echo ""
echo "Starting backend build..."
bash ./build-backend.sh
BACKEND_STATUS=$?

if [[ $BACKEND_STATUS -eq 0 ]]; then
    echo "BACKEND RESULT: SUCCESS"
else
    echo "BACKEND RESULT: FAILED (exit code: $BACKEND_STATUS)"
fi

echo ""
echo "Starting frontend build..."
bash ./build-frontend.sh
FRONTEND_STATUS=$?

if [[ $FRONTEND_STATUS -eq 0 ]]; then
    echo "FRONTEND RESULT: SUCCESS"
else
    echo "FRONTEND RESULT: FAILED (exit code: $FRONTEND_STATUS)"
fi

echo ""
echo "========================================"
echo "CONSOLIDATED BUILD RESULT"
echo "Backend exit code: $BACKEND_STATUS"
echo "Frontend exit code: $FRONTEND_STATUS"

if [[ $BACKEND_STATUS -eq 0 && $FRONTEND_STATUS -eq 0 ]]; then
    echo "OVERALL RESULT: SUCCESS"
    exit 0
else
    echo "OVERALL RESULT: FAILED"
    exit 1
fi