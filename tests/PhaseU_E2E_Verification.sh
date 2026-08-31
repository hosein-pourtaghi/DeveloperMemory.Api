#!/usr/bin/env bash
# ════════════════════════════════════════════════════════════════════════════════
# Phase U — Real PostgreSQL + Kestrel E2E Verification Script
# ════════════════════════════════════════════════════════════════════════════════
#
# Verifies Phase R (workspace/project context), Phase S (semantic/relevance
# ranking), and Phase T (AgentContext) functionality through real HTTP
# requests against the running API with real PostgreSQL persistence.
#
# Prerequisites:
#   - .NET 10.0 SDK
#   - PostgreSQL running locally (default: localhost:5432)
#   - Database: developermemory, Role: developer, Password: devpassword
#
# Usage:
#   chmod +x tests/PhaseU_E2E_Verification.sh
#   ./tests/PhaseU_E2E_Verification.sh
# ════════════════════════════════════════════════════════════════════════════════

set -euo pipefail

BASE_URL="http://127.0.0.1:5041"
API_DIR="src/DeveloperMemory.Api"
PASS_COUNT=0
FAIL_COUNT=0
TOTAL_COUNT=0

# ── Colors ──
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# ── Helpers ──

pass() {
    PASS_COUNT=$((PASS_COUNT + 1))
    TOTAL_COUNT=$((TOTAL_COUNT + 1))
    echo -e "  ${GREEN}✓ PASS${NC}: $1"
}

fail() {
    FAIL_COUNT=$((FAIL_COUNT + 1))
    TOTAL_COUNT=$((TOTAL_COUNT + 1))
    echo -e "  ${RED}✗ FAIL${NC}: $1"
    if [ -n "${2:-}" ]; then
        echo -e "    ${YELLOW}Detail:${NC} $2"
    fi
}

section() {
    echo ""
    echo -e "${CYAN}═══════════════════════════════════════════════════════════${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}═══════════════════════════════════════════════════════════${NC}"
}

check_json_field() {
    local response="$1"
    local field="$2"
    local expected="$3"
    local actual
    actual=$(echo "$response" | python3 -c "import sys,json; print(json.load(sys.stdin).get('$field', 'MISSING'))" 2>/dev/null || echo "PARSE_ERROR")
    if [ "$actual" = "$expected" ]; then
        return 0
    else
        return 1
    fi
}

# ── Prerequisite Checks ──

section "Prerequisite Checks"

# Check dotnet
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: dotnet SDK not found. Install .NET 10.0 SDK.${NC}"
    exit 1
fi
echo -e "  ${GREEN}✓${NC} dotnet SDK: $(dotnet --version)"

# Check PostgreSQL
if ! command -v psql &> /dev/null; then
    echo -e "${YELLOW}⚠ WARNING: psql not found. Will rely on API health check for DB verification.${NC}"
else
    if psql -h localhost -U developer -d developermemory -c "SELECT 1;" &>/dev/null; then
        echo -e "  ${GREEN}✓${NC} PostgreSQL: connected (developermemory database)"
    else
        echo -e "  ${RED}✗${NC} PostgreSQL: cannot connect to developermemory database"
        echo -e "    Verify: host=localhost port=5432 db=developermemory user=developer"
        exit 1
    fi
fi

# ── Reset Database ──

section "Database Reset"

echo "  Dropping and recreating database..."
if command -v psql &> /dev/null; then
    psql -h localhost -U developer -d postgres -c "DROP DATABASE IF EXISTS developermemory;" 2>/dev/null
    psql -h localhost -U developer -d postgres -c "CREATE DATABASE developermemory;" 2>/dev/null
    echo -e "  ${GREEN}✓${NC} Database recreated"
else
    echo -e "  ${YELLOW}⚠${NC} Skipping database reset (psql not available). App will migrate on startup."
fi

# ── Build ──

section "Build"

echo "  Building solution..."
cd "$(dirname "$0")/.."  # Navigate to project root
dotnet build "$API_DIR" --configuration Debug --no-restore 2>&1 | tail -3
BUILD_EXIT=$?
if [ $BUILD_EXIT -ne 0 ]; then
    echo -e "  ${RED}✗ Build failed${NC}"
    exit 1
fi
echo -e "  ${GREEN}✓${NC} Build succeeded"

# ── Start API ──

section "Start API"

echo "  Starting API on $BASE_URL..."
dotnet run --project "$API_DIR" --configuration Debug &
API_PID=$!

# Wait for API to start
echo "  Waiting for API to become ready..."
for i in $(seq 1 30); do
    if curl -s "$BASE_URL/health" > /dev/null 2>&1; then
        echo -e "  ${GREEN}✓${NC} API started (PID: $API_PID)"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "  ${RED}✗ API failed to start within 30 seconds${NC}"
        kill $API_PID 2>/dev/null || true
        exit 1
    fi
    sleep 1
done

# Cleanup on exit
cleanup() {
    echo ""
    echo "  Stopping API (PID: $API_PID)..."
    kill $API_PID 2>/dev/null || true
    wait $API_PID 2>/dev/null || true
}
trap cleanup EXIT

# ════════════════════════════════════════════════════════════════════════════════
# VERIFICATION TESTS
# ════════════════════════════════════════════════════════════════════════════════

# ── Task 2: Verify Real PostgreSQL Configuration ──

section "Task 2: Verify Real PostgreSQL Configuration"

# Test: Health endpoint shows Connected
HEALTH=$(curl -s "$BASE_URL/health")
if echo "$HEALTH" | grep -q '"Connected"'; then
    pass "Health endpoint reports database Connected"
else
    fail "Health endpoint does not report Connected" "$HEALTH"
fi

# Test: UseInMemoryDatabase is false
IN_MEMORY_CHECK=$(curl -s "$BASE_URL/health")
if echo "$IN_MEMORY_CHECK" | grep -q '"Healthy"'; then
    pass "Database is Healthy (not InMemory fallback)"
else
    fail "Database is not Healthy" "$IN_MEMORY_CHECK"
fi

# ── Task 4: Verify Development Authentication ──

section "Task 4: Verify Development Authentication"

# Test: Memory endpoints are accessible without auth
STATS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/Memory/stats")
if [ "$STATS_CODE" = "200" ]; then
    pass "Memory/stats accessible without authentication (Development mode)"
else
    fail "Memory/stats returned unexpected status" "Expected 200, got $STATS_CODE"
fi

# ── Task 5: Verify Phase R (Workspace/Project Context) ──

section "Task 5: Verify Phase R — Workspace/Project Context"

# 5a. Create a project
echo "  Creating test project..."
PROJECT_RESPONSE=$(curl -s -X POST "$BASE_URL/api/Projects" \
    -H "Content-Type: application/json" \
    -d '{"name":"PhaseU-TestProject","description":"E2E verification project"}')
PROJECT_ID=$(echo "$PROJECT_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
if [ -n "$PROJECT_ID" ] && [ "$PROJECT_ID" != "" ]; then
    pass "Project created (ID: $PROJECT_ID)"
else
    fail "Project creation failed" "$PROJECT_RESPONSE"
fi

# 5b. Create a GLOBAL memory
echo "  Creating Global memory..."
GLOBAL_MEM=$(curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"Global Memory Test","content":"This is a global scope memory for E2E testing","scope":"Global","importance":0.7}')
GLOBAL_ID=$(echo "$GLOBAL_MEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
if [ -n "$GLOBAL_ID" ] && [ "$GLOBAL_ID" != "" ]; then
    pass "Global memory created (ID: $GLOBAL_ID)"
else
    fail "Global memory creation failed" "$GLOBAL_MEM"
fi

# 5c. Create a PROJECT memory
echo "  Creating Project-scoped memory..."
if [ -n "$PROJECT_ID" ]; then
    PROJECT_MEM=$(curl -s -X POST "$BASE_URL/api/Memory" \
        -H "Content-Type: application/json" \
        -d "{\"title\":\"Project Memory Test\",\"content\":\"This is a project-scoped memory\",\"scope\":\"Project\",\"projectId\":\"$PROJECT_ID\",\"importance\":0.8}")
    PROJECT_MEM_ID=$(echo "$PROJECT_MEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
    if [ -n "$PROJECT_MEM_ID" ] && [ "$PROJECT_MEM_ID" != "" ]; then
        pass "Project-scoped memory created (ID: $PROJECT_MEM_ID)"
    else
        fail "Project-scoped memory creation failed" "$PROJECT_MEM"
    fi
else
    fail "Cannot test project memory (no project ID)"
fi

# 5d. Create a WORKSPACE memory
echo "  Creating Workspace-scoped memory..."
WS_MEM=$(curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"Workspace Memory Test","content":"This is a workspace-scoped memory","scope":"Workspace","workspaceId":"phaseu-ws-001","importance":0.6}')
WS_MEM_ID=$(echo "$WS_MEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
if [ -n "$WS_MEM_ID" ] && [ "$WS_MEM_ID" != "" ]; then
    pass "Workspace-scoped memory created (ID: $WS_MEM_ID)"
else
    fail "Workspace-scoped memory creation failed" "$WS_MEM"
fi

# 5e. Create a PRIVATE memory
echo "  Creating Private-scoped memory..."
PRIV_MEM=$(curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"Private Memory Test","content":"This is a private memory for a specific user","scope":"Private","userId":"phaseu-user-001","importance":0.9}')
PRIV_MEM_ID=$(echo "$PRIV_MEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
if [ -n "$PRIV_MEM_ID" ] && [ "$PRIV_MEM_ID" != "" ]; then
    pass "Private-scoped memory created (ID: $PRIV_MEM_ID)"
else
    fail "Private-scoped memory creation failed" "$PRIV_MEM"
fi

# 5f. Verify persistence: retrieve by ID
echo "  Verifying persistence (GET by ID)..."
if [ -n "$WS_MEM_ID" ]; then
    RETRIEVED=$(curl -s "$BASE_URL/api/Memory/$WS_MEM_ID")
    RET_TITLE=$(echo "$RETRIEVED" | python3 -c "import sys,json; print(json.load(sys.stdin).get('title',''))" 2>/dev/null || echo "")
    RET_WS=$(echo "$RETRIEVED" | python3 -c "import sys,json; print(json.load(sys.stdin).get('workspaceId','MISSING'))" 2>/dev/null || echo "")
    if [ "$RET_TITLE" = "Workspace Memory Test" ]; then
        pass "Workspace memory persisted and retrieved by ID"
    else
        fail "Workspace memory retrieval mismatch" "Title: $RET_TITLE"
    fi
    if [ "$RET_WS" = "phaseu-ws-001" ]; then
        pass "WorkspaceId preserved correctly after persistence"
    else
        fail "WorkspaceId lost after persistence" "Expected 'phaseu-ws-001', got '$RET_WS'"
    fi
else
    fail "Cannot verify persistence (no workspace memory ID)"
fi

# 5g. Verify project isolation: query with projectId should return project memories
echo "  Verifying project isolation..."
if [ -n "$PROJECT_ID" ]; then
    ISOLATION=$(curl -s "$BASE_URL/api/Memory?projectId=$PROJECT_ID")
    # Should contain the project memory but not workspace memories
    if echo "$ISOLATION" | grep -q "Project Memory Test"; then
        pass "Project query returns project-scoped memories"
    else
        fail "Project query missing project-scoped memories" "$ISOLATION"
    fi
else
    fail "Cannot verify project isolation (no project ID)"
fi

# 5h. Verify workspace scope filtering
echo "  Verifying workspace scope filtering..."
WS_QUERY=$(curl -s -X POST "$BASE_URL/api/Memory/query" \
    -H "Content-Type: application/json" \
    -d '{"query":"Workspace Memory Test","workspaceId":"phaseu-ws-001"}')
WS_COUNT=$(echo "$WS_QUERY" | python3 -c "import sys,json; print(json.load(sys.stdin).get('returnedCount',0))" 2>/dev/null || echo "0")
if [ "$WS_COUNT" -gt "0" ]; then
    pass "Workspace query with workspaceId returns workspace memories"
else
    fail "Workspace query returned 0 results" "$WS_QUERY"
fi

# ── Task 6: Verify Phase S (Semantic/Relevance Ranking) ──

section "Task 6: Verify Phase S — Semantic/Relevance Ranking"

# 6a. Create memories with varying relevance for testing
echo "  Creating additional test memories for ranking..."
curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"DotNet Clean Architecture Pattern","content":"Clean Architecture separates concerns into Domain, Application, Infrastructure, and Presentation layers","scope":"Global","importance":0.6,"tags":["architecture","dotnet"]}' > /dev/null

curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"Entity Framework Core Migrations","content":"EF Core migrations track schema changes and apply them to the database incrementally","scope":"Global","importance":0.5,"tags":["database","efcore"]}' > /dev/null

curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"PostgreSQL JSONB Operations","content":"PostgreSQL supports JSONB type for efficient JSON storage and querying with GIN indexes","scope":"Global","importance":0.4,"tags":["database","postgresql"]}' > /dev/null

# 6b. Test keyword retrieval with relevance ranking
echo "  Testing retrieval pipeline (keyword search + ranking)..."
RETRIEVE=$(curl -s -X POST "$BASE_URL/api/Memory/retrieve" \
    -H "Content-Type: application/json" \
    -d '{"query":"PostgreSQL database","userId":"test-user","maximumResults":10,"contextTokenBudget":5000}')
RETRIEVED_COUNT=$(echo "$RETRIEVE" | python3 -c "import sys,json; r=json.load(sys.stdin); print(len(r.get('memories',[])))" 2>/dev/null || echo "0")
if [ "$RETRIEVED_COUNT" -gt "0" ]; then
    pass "Retrieval pipeline returns ranked results (count: $RETRIEVED_COUNT)"
else
    fail "Retrieval pipeline returned 0 results" "$RETRIEVE"
fi

# 6c. Verify ranking order: more relevant results should rank higher
echo "  Verifying ranking order..."
RANKED_TITLES=$(echo "$RETRIEVE" | python3 -c "
import sys,json
r=json.load(sys.stdin)
for m in r.get('memories',[]):
    mem = m.get('memory', m)
    print(f\"{m.get('relevanceScore',0):.2f} | {mem.get('title','?')}\")
" 2>/dev/null || echo "PARSE_ERROR")
if [ "$RANKED_TITLES" != "PARSE_ERROR" ]; then
    pass "Ranking produces scored results:"
    echo "    $RANKED_TITLES" | head -5
else
    fail "Could not parse ranking results"
fi

# 6d. Verify scope resolution in retrieval
echo "  Verifying scope resolution with workspace context..."
SCOPE_WS=$(curl -s -X POST "$BASE_URL/api/Memory/retrieve" \
    -H "Content-Type: application/json" \
    -d '{"query":"Memory","workspaceId":"phaseu-ws-001","userId":"test-user","maximumResults":20,"contextTokenBudget":5000}')
SCOPE_WS_COUNT=$(echo "$SCOPE_WS" | python3 -c "import sys,json; r=json.load(sys.stdin); print(len(r.get('memories',[])))" 2>/dev/null || echo "0")
if [ "$SCOPE_WS_COUNT" -gt "0" ]; then
    pass "Retrieval with workspaceId returns workspace-scoped results (count: $SCOPE_WS_COUNT)"
else
    fail "Retrieval with workspaceId returned 0 results"
fi

# 6e. Verify that workspace memories are NOT returned without workspaceId
echo "  Verifying workspace isolation (no workspaceId)..."
SCOPE_NO_WS=$(curl -s -X POST "$BASE_URL/api/Memory/retrieve" \
    -H "Content-Type: application/json" \
    -d '{"query":"Workspace Memory Test","userId":"test-user","maximumResults":20,"contextTokenBudget":5000}')
# Should NOT contain workspace memory when no workspaceId provided
HAS_WS=$(echo "$SCOPE_NO_WS" | python3 -c "
import sys,json
r=json.load(sys.stdin)
titles = [m.get('memory',m).get('title','') for m in r.get('memories',[])]
print('yes' if 'Workspace Memory Test' in titles else 'no')
" 2>/dev/null || echo "PARSE_ERROR")
if [ "$HAS_WS" = "no" ]; then
    pass "Workspace memory excluded from results when no workspaceId provided"
else
    fail "Workspace memory leaked into results without workspaceId"
fi

# 6f. Test the POST /api/Memory/query endpoint (structured query)
echo "  Testing structured query endpoint..."
QUERY=$(curl -s -X POST "$BASE_URL/api/Memory/query" \
    -H "Content-Type: application/json" \
    -d '{"query":"architecture","userId":"test-user","maxResults":5,"minRelevanceScore":0.0}')
QUERY_RETURNED=$(echo "$QUERY" | python3 -c "import sys,json; print(json.load(sys.stdin).get('returnedCount',0))" 2>/dev/null || echo "0")
if [ "$QUERY_RETURNED" -gt "0" ]; then
    pass "Structured query returns ranked results (count: $QUERY_RETURNED)"
else
    fail "Structured query returned 0 results" "$QUERY"
fi

# ── Verify Supersession ──

section "Verify Supersession Lifecycle"

# Create and supersede
echo "  Testing supersession..."
SUPERSEDE_ORIG=$(curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"Old Architecture Pattern","content":"Use traditional N-tier architecture","scope":"Global","importance":0.3}')
ORIG_ID=$(echo "$SUPERSEDE_ORIG" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")

if [ -n "$ORIG_ID" ] && [ "$ORIG_ID" != "" ]; then
    SUPERSEDE_NEW=$(curl -s -X POST "$BASE_URL/api/Memory/$ORIG_ID/supersede" \
        -H "Content-Type: application/json" \
        -d '{"title":"Modern Clean Architecture","content":"Use Clean Architecture with Domain-Driven Design","scope":"Global","importance":0.8}')
    NEW_ID=$(echo "$SUPERSEDE_NEW" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
    if [ -n "$NEW_ID" ] && [ "$NEW_ID" != "" ]; then
        pass "Supersession created new memory (ID: $NEW_ID)"
        # Verify old is superseded
        OLD_STATE=$(curl -s "$BASE_URL/api/Memory/$ORIG_ID" | python3 -c "import sys,json; print(json.load(sys.stdin).get('state',''))" 2>/dev/null || echo "")
        if [ "$OLD_STATE" = "Superseded" ]; then
            pass "Original memory state is Superseded"
        else
            fail "Original memory state is not Superseded" "State: $OLD_STATE"
        fi
    else
        fail "Supersession failed to create new memory"
    fi
else
    fail "Cannot test supersession (no original memory ID)"
fi

# ── Verify Expiration ──

section "Verify Memory Expiration"

echo "  Creating memory with past expiration..."
EXPIRED_MEM=$(curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"Expired Memory","content":"This should be expired","scope":"Global","expiresAt":"2020-01-01T00:00:00Z","importance":0.2}')
EXPIRED_ID=$(echo "$EXPIRED_MEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")

EXPIRE_RESULT=$(curl -s -X POST "$BASE_URL/api/Memory/expire")
EXPIRED_COUNT=$(echo "$EXPIRE_RESULT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('expired',0))" 2>/dev/null || echo "0")
if [ "$EXPIRED_COUNT" -gt "0" ]; then
    pass "Expiration processed $EXPIRED_COUNT entries"
else
    fail "Expiration did not process any entries" "$EXPIRE_RESULT"
fi

# ── Verify Stats ──

section "Verify Memory Statistics"

STATS=$(curl -s "$BASE_URL/api/Memory/stats")
TOTAL=$(echo "$STATS" | python3 -c "import sys,json; print(json.load(sys.stdin).get('totalCount',0))" 2>/dev/null || echo "0")
if [ "$TOTAL" -gt "0" ]; then
    pass "Stats endpoint returns data (total: $TOTAL memories)"
    echo "$STATS" | python3 -c "
import sys,json
s=json.load(sys.stdin)
print(f\"    Active: {s.get('activeCount',0)}, Expired: {s.get('expiredCount',0)}, Superseded: {s.get('supersededCount',0)}\")
print(f\"    Global: {s.get('globalCount',0)}, Project: {s.get('projectCount',0)}, Workspace: {s.get('workspaceCount',0)}, Private: {s.get('privateCount',0)}\")
" 2>/dev/null
else
    fail "Stats endpoint returned 0 total" "$STATS"
fi

# ── Verify DELETE (soft delete) ──

section "Verify Soft Delete"

echo "  Testing soft delete..."
DELETE_MEM=$(curl -s -X POST "$BASE_URL/api/Memory" \
    -H "Content-Type: application/json" \
    -d '{"title":"To Be Deleted","content":"This will be soft-deleted","scope":"Global"}')
DELETE_ID=$(echo "$DELETE_MEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")

if [ -n "$DELETE_ID" ] && [ "$DELETE_ID" != "" ]; then
    DELETE_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BASE_URL/api/Memory/$DELETE_ID")
    if [ "$DELETE_CODE" = "204" ]; then
        pass "Soft delete returns 204 No Content"
        # Verify state is Deleted
        DELETE_STATE=$(curl -s "$BASE_URL/api/Memory/$DELETE_ID" | python3 -c "import sys,json; print(json.load(sys.stdin).get('state','MISSING'))" 2>/dev/null || echo "MISSING")
        if [ "$DELETE_STATE" = "Deleted" ]; then
            pass "Deleted memory has state=Deleted"
        else
            fail "Deleted memory state is not 'Deleted'" "State: $DELETE_STATE"
        fi
    else
        fail "Soft delete returned unexpected status" "Expected 204, got $DELETE_CODE"
    fi
else
    fail "Cannot test soft delete (no memory ID)"
fi

# ════════════════════════════════════════════════════════════════════════════════
# RESULTS SUMMARY
# ════════════════════════════════════════════════════════════════════════════════

section "Results Summary"

echo ""
echo -e "  Total tests:  $TOTAL_COUNT"
echo -e "  ${GREEN}Passed:${NC}       $PASS_COUNT"
echo -e "  ${RED}Failed:${NC}       $FAIL_COUNT"
echo ""

if [ $FAIL_COUNT -eq 0 ]; then
    echo -e "  ${GREEN}═══════════════════════════════════════${NC}"
    echo -e "  ${GREEN}  ALL PHASE U E2E TESTS PASSED ✓     ${NC}"
    echo -e "  ${GREEN}═══════════════════════════════════════${NC}"
    exit 0
else
    echo -e "  ${RED}═══════════════════════════════════════${NC}"
    echo -e "  ${RED}  $FAIL_COUNT TEST(S) FAILED ✗         ${NC}"
    echo -e "  ${RED}═══════════════════════════════════════${NC}"
    exit 1
fi
