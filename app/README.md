# App

ASP.NET Core backend for server-side integrations.

Current responsibility:

- `GET /api/config`
  Returns browser runtime config such as the Google Maps JavaScript API key and vector map ID.
- `POST /api/address-validation`
  Calls Google Address Validation on the server.
- `POST /api/places/nearby-search`
  Calls Google Places Nearby Search on the server.
- `POST /api/places/text-search`
  Calls Google Places Text Search on the server.
- `GET /api/postal-boundary-target?postalCode=...&iso2=...`
  Calls Google Geocoding on the server and returns a postal-code place ID and viewport for Google boundary rendering.
- Swagger UI at `/swagger`

Run locally:

```bash
dotnet run --project app/AddressEnrichment.Api.csproj
```

By default the API listens on the standard ASP.NET Core local ports. The Angular app is configured to call `http://localhost:5080`.

Configuration:

- `Google:ApiKey` in `app/appsettings.json`
- `Google:MapsApiKey` in `app/appsettings.json`
- `Google:MapId` in `app/appsettings.json`
- or environment variables `Google__ApiKey`, `Google__MapsApiKey`, `Google__MapId`

Note:

- Address Validation, Places, and postal boundary target lookup now stay server-side.
- Postal boundary shading now uses Google Maps data-driven styling for `POSTAL_CODE`, which requires a vector map ID with the `POSTAL_CODE` boundary layer enabled in Google Cloud.
- The Google Maps JavaScript key is no longer hardcoded in the UI, but it is still delivered to the browser at runtime because the Maps JS SDK requires a browser-usable key. Restrict that key by HTTP referrer.
- Serilog request and API logs are written to `app/logs/app-*.log`.

Open Swagger:

```text
http://localhost:5080/swagger
```
