#!/bin/bash
# Test script for Schedule Diagnostics endpoints
# Run this when the API is running in Development mode

API_URL="${API_URL:-http://localhost:5000}"

echo "Testing Schedule Diagnostics Endpoints"
echo "======================================="
echo ""

# Test 1: IsActive - Weekday Evening Schedule
echo "Test 1: IsActive - Weekday Evening Schedule"
echo "Testing if schedule is active on Tuesday at 8 PM..."
curl -s -X POST "$API_URL/api/schedules/debug/is-active" \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "18:00",
    "onDay": "* * 1-5",
    "offTime": "23:00",
    "offDay": "* * 1-5",
    "nowUtc": "2025-01-14T20:00:00Z"
  }' | jq '.'
echo ""
echo ""

# Test 2: IsActive - Weekend Schedule (should be inactive on Tuesday)
echo "Test 2: IsActive - Weekend Schedule on Tuesday (should be inactive)"
curl -s -X POST "$API_URL/api/schedules/debug/is-active" \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "18:00",
    "onDay": "* * 6-7",
    "offTime": "06:00",
    "offDay": "* * 6-7",
    "nowUtc": "2025-01-14T20:00:00Z"
  }' | jq '.'
echo ""
echo ""

# Test 3: MultiDayEvents - Multi-Day Schedule
echo "Test 3: MultiDayEvents - Multi-Day Schedule (1st-5th of month)"
curl -s -X POST "$API_URL/api/schedules/debug/multi-day-events" \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "18:00",
    "onDay": "1 * *",
    "offTime": "23:00",
    "offDay": "5 * *",
    "fromUtc": "2025-01-01T00:00:00Z",
    "toUtc": "2025-02-01T00:00:00Z"
  }' | jq '.'
echo ""
echo ""

# Test 4: MultiDayEvents - Daily Schedule
echo "Test 4: MultiDayEvents - Daily Schedule (next 7 days)"
curl -s -X POST "$API_URL/api/schedules/debug/multi-day-events" \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "19:00",
    "onDay": "* * *",
    "offTime": "23:00",
    "offDay": "* * *"
  }' | jq '. | {eventCount, fromUtc, toUtc, events: .events[:5]}'
echo ""
echo ""

# Test 5: IsActive - Current Time (no nowUtc specified)
echo "Test 5: IsActive - Current Time (defaults to now)"
curl -s -X POST "$API_URL/api/schedules/debug/is-active" \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "00:00",
    "onDay": "* * *",
    "offTime": "23:59",
    "offDay": "* * *"
  }' | jq '.'
echo ""
echo ""

echo "Tests completed!"
echo ""
echo "Note: If you see errors, make sure:"
echo "  1. The API is running at $API_URL"
echo "  2. The API is in Development mode (diagnostics enabled)"
echo "  3. jq is installed for JSON formatting"

