# TitleFlow API

.NET 10 LTS Web API for title operations, organized as Controller → Service → Repository → EF Core.

## API surface

- `GET /api/titles` — paginated multi-field search
- `GET /api/titles/{id}` — title detail
- `POST /api/titles` — create
- `PUT /api/titles/{id}` — update
- `DELETE /api/titles` — bulk delete
- `GET /api/titles/dashboard` — dashboard metrics
- `GET /api/titles/dropdowns` — distinct autocomplete data
- `POST /api/titles/import/preview` — validate Excel without saving
- `POST /api/titles/import/commit` — save clean preview rows
- `GET /api/titles/template` — download upload template
- `GET /api/titles/export` — filtered Excel export

## Run

```bash
dotnet restore
dotnet run --project src/TitleFlow.Api
```

Swagger opens at `https://localhost:7184/swagger`. Demo mode uses seeded in-memory data, so no database setup is required. To use the existing SQL Server `TBL_TITLES` table, set `Database:UseDemoData` to `false` and update `ConnectionStrings:DefaultConnection`.
