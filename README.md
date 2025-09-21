**DPSMeter** tracks and visualizes your **damage per second (DPS)** using Gamehelper2 in Path of Exile 2 in real time, with multiple metrics and a modern overlay.

## :sparkles: Features
* **Rolling DPS** – smoothed average over a configurable window (default: 8 seconds).
* **Max DPS** – highest rolling DPS reached this session.
* **Session DPS** – total damage over time since you enabled the plugin.
* **Area DPS** – resets automatically when you change zones.
* **Sparkline graph** – optional mini-chart showing recent hits.
* **Progress bar** – visual indicator of current DPS vs. max DPS.
* **Damage filters**:
  * Only include Rare/Unique enemies (optional).
  * Ignore very small damage ticks below a configurable threshold.
  * Restrict calculations to enemies within a configurable on-screen radius.

## :gear: Configuration
All settings are available in the GameHelper2 settings UI:
* **Display Options**
  * Toggle rolling, max, session, and area DPS individually.
  * Humanize large numbers (`12.3K`, `4.5M`).
  * Customize colors, shadows, padding, corner radius, and widths.
  * Adjust scale of the “big number” for readability.

## :package: Installation
1. [Download the latest release zip](https://github.com/derekShaheen/DPSMeter-Gamehelper2/releases).
2. Extract the archive into your GameHelper2 `Plugins` folder:
   ```
   GameHelper2/Plugins/DPSMeter/
   ```