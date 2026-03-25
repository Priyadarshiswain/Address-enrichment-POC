# Address Enrichment POC

Project layout:

- `ui/`: Angular frontend for map rendering, interaction, and cost analysis
- `app/`: ASP.NET Core backend for Google Address Validation, Google Places, Google postal-boundary target lookup, runtime config, and backend tests

Backend highlights:

- Controller-based ASP.NET Core API
- Google integrations isolated in a service layer
- Swagger enabled at `/swagger`
- Serilog file logging to `app/logs/app-*.log`
- Unit and integration tests for the backend service layer and HTTP endpoints

Frontend commands:

```bash
cd ui
npm install
npm start
```

Backend commands:

```bash
dotnet run --project app/AddressEnrichment.Api.csproj --urls http://localhost:5080
```

```bash
dotnet test app/tests/AddressEnrichment.Api.Tests/AddressEnrichment.Api.Tests.csproj
```

Backend solution:

```bash
open app/address-enrichment-poc.sln
```

Endpoints:

- `GET /api/config`
- `GET /api/postal-boundary-target`
- `POST /api/address-validation`
- `POST /api/places/nearby-search`
- `POST /api/places/text-search`

Swagger:

```text
http://localhost:5080/swagger
```

Notes:

- Postal boundary rendering uses Google Maps `POSTAL_CODE` boundary layers.
- The browser map requires a valid Google Maps JavaScript key and a vector `Map ID`.
- Service APIs use a separate server-side Google API key.
