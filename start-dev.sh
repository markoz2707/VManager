#!/usr/bin/env bash
#
# VManager - Development startup script (Linux/macOS/Git Bash)
#
# Starts the .NET backend and React dev server side by side.
# Press Ctrl+C to stop both processes.
#
# Usage:
#   ./dev.sh                 # start both backend + frontend
#   ./dev.sh --backend-only  # start only .NET backend
#   ./dev.sh --frontend-only # start only React dev server
#   ./dev.sh --no-browser    # don't auto-open browser

set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
AGENT_DIR="$ROOT/src/HyperV.Agent"
FRONTEND_DIR="$ROOT/src/HyperV.LocalManagement"

BACKEND_ONLY=false
FRONTEND_ONLY=false
NO_BROWSER=false

for arg in "$@"; do
    case $arg in
        --backend-only)  BACKEND_ONLY=true ;;
        --frontend-only) FRONTEND_ONLY=true ;;
        --no-browser)    NO_BROWSER=true ;;
    esac
done

cleanup() {
    echo ""
    echo "  Stopping services..."
    [ -n "$BACKEND_PID" ] && kill "$BACKEND_PID" 2>/dev/null
    [ -n "$FRONTEND_PID" ] && kill "$FRONTEND_PID" 2>/dev/null
    wait 2>/dev/null
    echo "  Done."
}
trap cleanup EXIT INT TERM

echo ""
echo "  VManager - Development Mode"
echo "  ==========================="
echo ""

# --- Backend ---
BACKEND_PID=""
if [ "$FRONTEND_ONLY" = false ]; then
    echo "  [Backend]  dotnet run  ->  https://localhost:8743"
    (cd "$AGENT_DIR" && dotnet run --no-launch-profile) &
    BACKEND_PID=$!
fi

# --- Frontend ---
FRONTEND_PID=""
if [ "$BACKEND_ONLY" = false ]; then
    if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
        echo "  [Frontend] Installing dependencies..."
        (cd "$FRONTEND_DIR" && npm install)
    fi

    echo "  [Frontend] vite dev   ->  http://localhost:3000"
    (cd "$FRONTEND_DIR" && npx vite --host) &
    FRONTEND_PID=$!
fi

echo ""
echo "  Press Ctrl+C to stop all services."
echo ""

# Open browser after a short delay
if [ "$NO_BROWSER" = false ] && [ "$BACKEND_ONLY" = false ]; then
    (sleep 4 && \
        if command -v xdg-open &>/dev/null; then xdg-open "http://localhost:3000"
        elif command -v open &>/dev/null; then open "http://localhost:3000"
        fi
    ) &
fi

# Wait for background processes
wait
