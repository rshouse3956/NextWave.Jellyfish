# Schedule Diagnostics Endpoints

These diagnostic endpoints are available when the API is running with diagnostics enabled (typically in Development mode).

## Test IsActiveAt Logic

**Endpoint:** `POST /api/schedules/debug/is-active`

Tests whether a schedule would be active at a specific datetime.

### Request Body

```json
{
  "onTime": "18:00",
  "onDay": "* * 1-5",
  "offTime": "23:00",
  "offDay": "* * 1-5",
  "nowUtc": "2025-01-15T20:30:00Z"
}
```

### Parameters

- `onTime` (string, required): Time specification for ON event (HH:mm or "Sunrise"/"Sunset")
- `onDay` (string, required): Cron fragment for ON day (3 fields: day-of-month month day-of-week)
- `offTime` (string, required): Time specification for OFF event
- `offDay` (string, required): Cron fragment for OFF day
- `nowUtc` (DateTime?, optional): The UTC datetime to evaluate. Defaults to current UTC time if not provided.

### Response

```json
{
  "isActive": true,
  "evaluatedAt": "2025-01-15T20:30:00Z",
  "onTime": "18:00",
  "onDay": "* * 1-5",
  "offTime": "23:00",
  "offDay": "* * 1-5"
}
```

### Example Usage

```bash
# Test if a weekday evening schedule is active at 8:30 PM
curl -X POST http://localhost:5000/api/schedules/debug/is-active \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "18:00",
    "onDay": "* * 1-5",
    "offTime": "23:00",
    "offDay": "* * 1-5",
    "nowUtc": "2025-01-15T20:30:00Z"
  }'

# Test with current time (omit nowUtc)
curl -X POST http://localhost:5000/api/schedules/debug/is-active \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "Sunset",
    "onDay": "* * *",
    "offTime": "23:00",
    "offDay": "* * *"
  }'
```

---

## Test ComputeMultiDayEvents Logic

**Endpoint:** `POST /api/schedules/debug/multi-day-events`

Computes all ON/OFF events for a schedule within a date range.

### Request Body

```json
{
  "onTime": "18:00",
  "onDay": "1 * *",
  "offTime": "06:00",
  "offDay": "5 * *",
  "fromUtc": "2025-01-01T00:00:00Z",
  "toUtc": "2025-02-01T00:00:00Z"
}
```

### Parameters

- `onTime` (string, required): Time specification for ON event
- `onDay` (string, required): Cron fragment for ON day
- `offTime` (string, required): Time specification for OFF event
- `offDay` (string, required): Cron fragment for OFF day
- `fromUtc` (DateTime?, optional): Start of date range. Defaults to current UTC time.
- `toUtc` (DateTime?, optional): End of date range. Defaults to fromUtc + 7 days.

### Response

```json
{
  "events": [
    {
      "eventUtc": "2025-01-01T18:00:00Z",
      "kind": "On",
      "localDate": "2025-01-01"
    },
    {
      "eventUtc": "2025-01-02T06:00:00Z",
      "kind": "Off",
      "localDate": "2025-01-02"
    },
    {
      "eventUtc": "2025-01-05T18:00:00Z",
      "kind": "On",
      "localDate": "2025-01-05"
    }
  ],
  "fromUtc": "2025-01-01T00:00:00Z",
  "toUtc": "2025-02-01T00:00:00Z",
  "onTime": "18:00",
  "onDay": "1 * *",
  "offTime": "06:00",
  "offDay": "5 * *",
  "eventCount": 3
}
```

### Example Usage

```bash
# Test a multi-day schedule (1st to 5th of each month)
curl -X POST http://localhost:5000/api/schedules/debug/multi-day-events \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "18:00",
    "onDay": "1 * *",
    "offTime": "06:00",
    "offDay": "5 * *",
    "fromUtc": "2025-01-01T00:00:00Z",
    "toUtc": "2025-02-01T00:00:00Z"
  }'

# Test next 7 days (default range)
curl -X POST http://localhost:5000/api/schedules/debug/multi-day-events \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "Sunset",
    "onDay": "* * 1-5",
    "offTime": "Sunrise",
    "offDay": "* * 1-5"
  }'

# Test a specific week
curl -X POST http://localhost:5000/api/schedules/debug/multi-day-events \
  -H "Content-Type: application/json" \
  -d '{
    "onTime": "17:00",
    "onDay": "* * 6-7",
    "offTime": "01:00",
    "offDay": "* * 6-7",
    "fromUtc": "2025-01-11T00:00:00Z",
    "toUtc": "2025-01-18T00:00:00Z"
  }'
```

---

## Common Test Scenarios

### 1. Weekday Evening Schedule
```json
{
  "onTime": "18:00",
  "onDay": "* * 1-5",
  "offTime": "23:00",
  "offDay": "* * 1-5"
}
```

### 2. Weekend All-Night Schedule
```json
{
  "onTime": "18:00",
  "onDay": "* * 6-7",
  "offTime": "06:00",
  "offDay": "* * 6-7"
}
```

### 3. Multi-Day Event (1st-5th of month)
```json
{
  "onTime": "18:00",
  "onDay": "1 * *",
  "offTime": "23:00",
  "offDay": "5 * *"
}
```

### 4. Sunrise to Sunset Daily
```json
{
  "onTime": "Sunrise",
  "onDay": "* * *",
  "offTime": "Sunset",
  "offDay": "* * *"
}
```

### 5. Holiday Schedule (using specific dates)
```json
{
  "onTime": "17:00",
  "onDay": "25 12 *",
  "offTime": "23:59",
  "offDay": "25 12 *"
}
```

---

## Tips for Debugging

1. **Use specific timestamps**: When testing `is-active`, provide explicit `nowUtc` values to test edge cases
2. **Check timezone handling**: Remember all times are in UTC; the service converts to configured timezone internally
3. **Verify event sequences**: Use `multi-day-events` to see the full sequence of ON/OFF events
4. **Test boundary conditions**: Test times right before/after ON and OFF events
5. **Compare with existing schedules**: Use `/api/schedules/diagnostics` to see how real schedules behave

---

## Related Endpoints

- `GET /api/schedules/diagnostics` - Get diagnostics for all existing schedules
- `GET /api/schedules/active-windows` - List schedules currently in their active window

