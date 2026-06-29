# Domain Glossary

The language this product speaks. Definitions only — no implementation, no specs, no decisions.

## Current Conditions
The present-moment weather snapshot for the active Location — the values that describe the weather *right now* (such as temperature, weather condition, and wind). Distinct from a Forecast, which describes *future* weather rather than the present. Sourced from the Weather Provider. The freshness of a displayed snapshot is named by its **Updated-at** time — when *the app last successfully fetched it* — not by any provider-reported observation time.

## Forecast
The predicted future weather for the active Location, broken down by hour and/or day. Distinct from Current Conditions (the present moment) — a Forecast is always about times that have not yet happened. Sourced from the Weather Provider.

## Geocoder
The external service that resolves a Location Search query into one or more candidate Locations (a place name → geographic coordinates + human-readable name). A separate concern from the Weather Provider — it supplies *places*, not *weather* — even though **today both are Open-Meteo**. Named generically so the glossary survives swapping the geocoding source independently of the weather source.

## Location
The single place the app is currently showing weather for, identified by its geographic coordinates (and a human-readable name). There is one active Location at a time — the product does not maintain a collection of saved or favourite locations. Both Current Conditions and a Forecast are always *for* a Location.

## Location Search
The act of finding a Location from a free-text query (typically a place name) by asking the Geocoder. Distinct from the Location it produces — Location Search is the *lookup*, the Location is the *result*.

## Weather Provider
The external service the app retrieves weather data from — **Open-Meteo**. It is the source of both Current Conditions and Forecast, and is not part of the app itself; the app is a client of it. Referred to generically as the "Weather Provider" so the glossary survives a future change of provider. Distinct from the Geocoder, which supplies places rather than weather.

## Relationships
- A **Location Search** is performed by the **Geocoder** and produces a **Location** (zero, one, or many candidates).
- A **Location** is the subject of both **Current Conditions** and a **Forecast**, both sourced from the **Weather Provider**.
- There is exactly one active **Location** at a time; selecting a candidate from a **Location Search** replaces it.
