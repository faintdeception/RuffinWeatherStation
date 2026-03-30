# NWS Forecast Rollout Plan

## Goal
Use rich NWS forecast data without overshadowing station telemetry.

## Product Split
1. Home (`/weather-home`): compact `Today + Tomorrow` forecast card.
2. Garden (`/garden-data`): practical 5-period planner card for planting/mowing/watering choices.
3. Forecast (`/forecast`): full-detail NWS page with expanded period detail.

## Phases

### Phase 1 (started)
- Add backend forecast-summary API sourced from latest NWS snapshot.
- Add shared frontend forecast model + service call.
- Add reusable `NwsForecastCard` component.
- Integrate compact Home forecast card.
- Integrate Garden 5-period forecast planner card.
- Create dedicated `/forecast` page and nav entry.

### Phase 2
- Enhance forecast-specific visual language (icons, weather-state gradients).
- Add day grouping and "today/tonight/tomorrow" chips.
- Add period confidence indicators and stale-data warning when snapshot is old.

### Phase 3
- Add chart views on `/forecast` (precip chance trend, wind trend, temp range).
- Add garden action tags: `Good for mowing`, `Delay watering`, `Frost caution`.
- Add richer mobile layout optimization and keyboard accessibility polish.

## Non-Goals
- Replacing station-driven cards as dashboard hero content.
- Mixing high-density forecast details into Home primary metric cards.
