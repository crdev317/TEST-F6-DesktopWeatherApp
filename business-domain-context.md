# Domain Glossary

The language this product speaks. Definitions only — no implementation, no specs, no decisions.

## Current Conditions
The present-moment weather snapshot for the active Location — the values that describe the weather *right now* (such as temperature, weather condition, and wind). Distinct from a Forecast, which describes *future* weather rather than the present. Sourced from the Weather Provider.

## Forecast
The predicted future weather for the active Location, broken down by hour and/or day. Distinct from Current Conditions (the present moment) — a Forecast is always about times that have not yet happened. Sourced from the Weather Provider.

## Location
The single place the app is currently showing weather for, identified by its geographic coordinates (and a human-readable name). There is one active Location at a time — the product does not maintain a collection of saved or favourite locations. Both Current Conditions and a Forecast are always *for* a Location.

## Weather Provider
The external service the app retrieves weather data from — **Open-Meteo**. It is the source of both Current Conditions and Forecast, and is not part of the app itself; the app is a client of it. Referred to generically as the "Weather Provider" so the glossary survives a future change of provider.
