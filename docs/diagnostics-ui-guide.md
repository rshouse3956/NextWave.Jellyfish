# Schedule Diagnostics UI Guide

## Overview

The Diagnostics page provides a user-friendly interface for testing schedule activation logic without creating actual schedules. This is particularly useful for:

- Testing edge cases and boundary conditions
- Verifying multi-day schedule behavior
- Debugging timezone conversion issues
- Understanding how cron expressions affect schedule activation

## Accessing the Diagnostics Page

Navigate to **Diagnostics** in the sidebar menu, or go directly to `/diagnostics`.

> **Note:** The diagnostic endpoints are only available when the API is running in Development mode.

## Features

### 1. Quick Presets

Five common schedule patterns are available as quick presets:

- **Weekday Evening** - Lights on 6 PM to 11 PM, Monday-Friday
- **Weekend All-Night** - Lights on 6 PM to 6 AM, Saturday-Sunday
- **Multi-Day (1st-5th)** - Lights on from 1st to 5th of each month
- **Daily Sunrise-Sunset** - Lights on from sunrise to sunset every day
- **Christmas Day** - Lights on 5 PM to 11:59 PM on December 25th

Click any preset button to populate both test forms with the preset values.

### 2. Test IsActive

This section tests whether a schedule would be active at a specific point in time.

**Inputs:**
- **On Time** - When lights turn on (HH:mm format or "Sunrise"/"Sunset")
- **On Day** - Cron fragment for ON day (3 fields: day-of-month month day-of-week)
- **Off Time** - When lights turn off
- **Off Day** - Cron fragment for OFF day
- **Now UTC** - The datetime to test (optional, defaults to current time)

**Quick Actions:**
- **Now** - Set to current datetime
- **Clear** - Remove the datetime (use current time)

**Result:**
- Shows whether the schedule is active (✓ YES or ✗ NO)
- Displays the exact datetime that was evaluated

**Example Use Cases:**
- Test if a weekday schedule is active on a Tuesday at 8 PM
- Verify that a weekend schedule is inactive on Monday
- Check edge cases around midnight transitions

### 3. Test MultiDayEvents

This section computes all ON and OFF events within a date range.

**Inputs:**
- **On Time** - When lights turn on
- **On Day** - Cron fragment for ON day
- **Off Time** - When lights turn off
- **Off Day** - Cron fragment for OFF day
- **From UTC** - Start of date range (optional, defaults to current time)
- **To UTC** - End of date range (optional, defaults to +7 days from start)

**Quick Actions:**
- **Now** - Set to current datetime
- **+7 Days** - Set to one week from now
- **Clear** - Remove the datetime (use default)

**Result:**
- Total event count
- Date range evaluated
- Two columns showing ON and OFF events separately
- Each event shows:
  - UTC datetime
  - Local date

**Example Use Cases:**
- Visualize how a multi-day schedule generates events
- Verify that OFF events push to the next day when appropriate
- Check that sunrise/sunset schedules generate correct event sequences
- Debug overlapping schedule windows

## Common Testing Scenarios

### Scenario 1: Weekday Evening Schedule

**Setup:**
```
On Time: 18:00
On Day: * * 1-5
Off Time: 23:00
Off Day: * * 1-5
```

**Test IsActive:**
- Set "Now UTC" to a Tuesday at 8 PM → Should be active
- Set "Now UTC" to a Tuesday at 5 PM → Should be inactive
- Set "Now UTC" to a Saturday at 8 PM → Should be inactive

**Test MultiDayEvents:**
- Set range to cover a full week
- Verify 5 ON events (Mon-Fri) and 5 OFF events

### Scenario 2: Multi-Day Event

**Setup:**
```
On Time: 18:00
On Day: 1 * *
Off Time: 23:00
Off Day: 5 * *
```

**Test MultiDayEvents:**
- Set range to cover 2-3 months
- Verify ON event on 1st of each month
- Verify OFF event on 5th of each month
- Check that events are generated for each day in between

### Scenario 3: Overnight Schedule

**Setup:**
```
On Time: 22:00
On Day: * * *
Off Time: 06:00
Off Day: * * *
```

**Test IsActive:**
- Test at 11 PM → Should be active
- Test at 2 AM → Should be active
- Test at 7 AM → Should be inactive

**Test MultiDayEvents:**
- Verify OFF events are on the day after ON events

### Scenario 4: Sunrise/Sunset

**Setup:**
```
On Time: Sunset
On Day: * * *
Off Time: 23:00
Off Day: * * *
```

**Test MultiDayEvents:**
- Set range to cover a week
- Verify ON events occur at different times each day (sunset varies)
- Verify OFF events are consistently at 23:00

## Tips

1. **Use specific datetimes** - When testing edge cases, provide explicit "Now UTC" values rather than relying on current time
2. **Test boundary conditions** - Test times right before and after ON/OFF events
3. **Verify timezone handling** - Remember all times are in UTC; the service converts internally
4. **Compare with real schedules** - Use the `/api/schedules/diagnostics` endpoint to see how actual schedules behave
5. **Check event sequences** - Use MultiDayEvents to understand the full pattern of activations

## Troubleshooting

**"Failed to test IsActive" error:**
- Check that the API is running in Development mode
- Verify cron expressions are valid (3 fields: DOM MON DOW)
- Ensure time format is HH:mm or "Sunrise"/"Sunset"

**No events returned:**
- Verify the date range is wide enough
- Check that cron expressions match at least one day in the range
- For sunrise/sunset, ensure Schedule:Latitude, Longitude, and ElevationMeters are configured

**Unexpected results:**
- Compare with the `/api/schedules/diagnostics` endpoint for existing schedules
- Test with simpler patterns first (e.g., `* * *` for every day)
- Use MultiDayEvents to see the full event sequence

## Related Documentation

- [Schedule Diagnostics Endpoints](./schedule-diagnostics-endpoints.md) - API documentation
- [Jellyfish Lighting API Documentation](https://github.com/parkerjfl/JellyfishLightingAPIExplorer) - Controller API reference from JellyFish Lighting (overview: [jellyfishlighting.com/support/api](https://www.jellyfishlighting.com/support/api))

