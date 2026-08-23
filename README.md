# TitleFlow API

.NET 10 LTS Web API for title operations, organized as Controller → Service → Repository → EF Core.

## API surface

- `GET /api/titles` — paginated multi-field search
- `GET /api/titles/{id}` — title detail
- `POST /api/titles` — create
- `PUT /api/titles/{id}` — update
- `DELETE /api/titles/{id}` — single delete
- `DELETE /api/titles` — bulk delete (`{ "ids": [1, 2] }`, maximum 1,000)
- `GET /api/titles/dashboard` — dashboard metrics
- `GET /api/titles/dropdowns` — all distinct autocomplete data; optional `query` and `limit` can narrow it
- `POST /api/titles/import/preview` — validate Excel without saving
- `POST /api/titles/import/commit` — save clean preview rows
- `GET /api/titles/template` — download upload template
- `GET /api/titles/export` — filtered Excel export

### Publication title API

Publication titles are isolated from the existing title module and use `dbo.TBL_TITLE_PUBLICATIONS`.
The public contract calls `InvoiceNumber` **Lot Number**.

- `GET /api/publication-titles` — paginated filters (`id`, `title`, `codeReference`, `lotNumber`, `paperId`, `titleYear`, `status`)
- `GET /api/publication-titles/modified` — records with an updated title
- `GET /api/publication-titles/{id}` — publication title detail
- `DELETE /api/publication-titles/{id}` and `DELETE /api/publication-titles` — single/bulk delete
- `GET /api/publication-titles/dashboard` and `/dropdowns` — screen support data
- `POST /api/publication-titles/import/preview` and `/import/commit` — validate then save clean publication rows
- `POST /api/publication-titles/modified/import/preview` and `/modified/import/commit` — validate then apply updated titles
- `GET /api/publication-titles/template` and `/modified/template` — Excel templates
- `GET /api/publication-titles/export` and `/modified/export` — filtered Excel exports

## Run

```bash
dotnet restore --configfile NuGet.Config
dotnet run --project src/TitleFlow.Api
```

Swagger opens at `https://localhost:7184/swagger`. The API connects to the existing SQL Server `SalesDataDB` database and reads `dbo.TBL_TITLES`. Update `ConnectionStrings:DefaultConnection` if SQL Server is hosted elsewhere.

## Title view filters

`GET /api/titles` accepts `page`, `pageSize` (maximum 200), `id`, `title`, `codeReference`, `invoiceNumber`, `titleYear`, and `status`. Text filters use contains matching. Record ID first matches the database ID and then falls back to the 1-based position in the filtered, newest-first result.

## Database performance

Run the idempotent index script once for a new database:

```powershell
sqlcmd -S . -E -b -i database\OptimizeTitles.sql
```

The API caches list/detail/dashboard/autocomplete reads for short periods and invalidates all title-data caches after create, update, delete, or import. Startup warm-up prepares the default title screen before the server begins listening.

Before using the Publication API, run the idempotent table/index script once:

```powershell
sqlcmd -S . -E -b -i database\CreatePublicationTitles.sql
```

The Publication cache and routes are separate from the original Title cache and routes.
