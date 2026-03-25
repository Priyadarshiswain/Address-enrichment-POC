# Address Enrichment POC

Project layout:

- `ui/`: Angular frontend for map rendering, interaction, and cost analysis
- `app/`: ASP.NET Core backend for Google Address Validation, Google Places, Google postal-boundary target lookup, and runtime config

Current frontend commands:

```bash
cd ui
npm install
npm start
```

Backend command:

```bash
dotnet run --project app/AddressEnrichment.Api.csproj --urls http://localhost:5080
```

Swagger:

```text
http://localhost:5080/swagger
```
