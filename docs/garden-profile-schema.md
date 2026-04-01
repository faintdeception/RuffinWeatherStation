# Garden Plant Profile Schema

This document defines the expected shape and semantics for entries in:

- `RuffinWeatherStation/wwwroot/data/garden-plants.json`

## Required Core Fields

- `plantId` (string): stable unique id, lowercase slug preferred.
- `displayName` (string): user-facing name.
- `categories` (array of strings): labels like `Cool Season`, `Warm Season`, `Annual`, `Perennial`, `Bulb`.

## Timing Fields

- `actionType` (string): `plant`, `buy`, `harvest`, or `prep`.
- `windowStartMonthDay`, `windowEndMonthDay` (optional, `MM-dd`): primary season window.
- `secondaryWindowStartMonthDay`, `secondaryWindowEndMonthDay` (optional, `MM-dd`): second seasonal window.
- `leadDays` (optional, int): how early to surface `Soon` before a planting window.
- `latestPlantMonthDay` (optional, `MM-dd`): latest practical planting date.

## Harvest Fields

- `harvestWindowStartMonthDay`, `harvestWindowEndMonthDay` (optional, `MM-dd`): harvest season window.
- `harvestLeadDays` (optional, int): early warning days before harvest window.

## Temperature and Frost Fields

- `minNightTempC` (optional, number): nighttime threshold used by streak logic.
- `requiredConsecutiveNights` (int): number of consecutive qualifying nights.
- `daysAfterLastFrostToTransplant` (optional, int): offset from average last frost date.
- `daysBeforeLastFrostToStartIndoors` (optional, int): indoor-start offset before average last frost.

## Succession Planting Semantics

- `supportsSuccessionPlanting` (bool) is independent of seasonal windows.
- It is valid and expected to use `supportsSuccessionPlanting=true` together with fixed primary/secondary windows.
- Window fields define *when* sowing is in season.
- `supportsSuccessionPlanting` defines whether repeated sowings are encouraged during active window(s).

## Night-Streak Logic Semantics

- Most planting profiles use warming logic: nights should be at or above `minNightTempC`.
- Cooling logic (nights at or below threshold) is reserved for fall-planted bulb workflows.
- Do not assume all `Perennial` profiles use cooling logic; warm/cool seasonal tags still follow warming logic.

## Data Entry Rules

- Month-day fields must use `MM-dd`.
- For each window pair, include both start and end or neither.
- Keep `plantId` unique across all entries.
- Prefer Celsius (`minNightTempC`) for temperature thresholds.
- Use `notes` for packet guidance and human-readable reminders.

## Example

```json
{
  "plantId": "cilantro",
  "displayName": "Cilantro",
  "categories": ["Cool Season", "Annual"],
  "actionType": "plant",
  "windowStartMonthDay": "03-01",
  "windowEndMonthDay": "05-15",
  "secondaryWindowStartMonthDay": "08-15",
  "secondaryWindowEndMonthDay": "10-15",
  "leadDays": 7,
  "latestPlantMonthDay": "05-15",
  "supportsSuccessionPlanting": true,
  "minNightTempC": 4.44,
  "requiredConsecutiveNights": 2,
  "daysAfterLastFrostToTransplant": -14,
  "daysBeforeLastFrostToStartIndoors": null,
  "notes": "Sow in spring or fall after frost risk. Plant 1/4 inch deep, spacing 4-6 inches apart."
}
```