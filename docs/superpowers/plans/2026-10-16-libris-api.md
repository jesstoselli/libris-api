# Libris API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Libris backend — a .NET 10 REST API (auth copied from Verto, books / genres / reading logs with the spec's status rules, ISBN lookup, covers on disk, yearly/monthly stats) — deployed on the BillFolder VPS at `libris-api.billfolder.app`.

**Architecture:** Same Clean Architecture as Verto (`Domain` → `Application` → `Infrastructure` → `Api`, minimal APIs, `OperationResult<T>` mapped to HTTP by `HttpResults`). The reading rules (status transitions, date auto-fill, page deltas) live as pure code in `Libris.Domain` and are unit-tested without a database; services in `Libris.Application` load, apply the rules and save through EF Core. Covers are files in a Docker volume, indexed by a `covers` table. ISBN lookup composes two typed `HttpClient`s (Open Library, then Google Books).

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10.0.7 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1, PostgreSQL 16 (+ `unaccent`), FluentValidation 12.1.1, Konscious Argon2 1.3.1, SixLabors.ImageSharp 3.1.x, xUnit 2.9.3 + FluentAssertions 8.9.0 + Testcontainers.PostgreSql 4.15.0, Docker, GitHub Actions, nginx + certbot, Cloudflare DNS.

**Spec:** `docs/superpowers/specs/2026-10-16-libris-design.md`

**Scope:** Plan 1 of 2. The Angular + Tauri app (`LibrisApp`) gets its own plan, written against the contract this plan produces (the DTOs in Tasks 4–9 are that contract).

**Reference repo:** `~/Repos/Verto` (working, deployed). Many files are copied with a `Verto` → `Libris` rename (namespaces, project names, JWT issuer/audience, container/db names). When a step says "copy", open the Verto file, copy it, rename, and keep its behavior unless the step says otherwise.

## Global Constraints

- Target framework `net10.0`; package versions as in Verto's `.csproj` files; new package `SixLabors.ImageSharp` pinned to the latest 3.1.x patch at implementation time (≥ 3.1.7) — the only dependency Verto does not have.
- `Directory.Build.props` and `.editorconfig` copied from Verto (`TreatWarningsAsErrors=true`, Meziantou analyzer); `<Company>Libris</Company>`. Fix analyzer findings in code you wrote; never suppress a rule repo-wide.
- Code, identifiers and comments in English; comments explain WHY only. User-facing API error `message`s in Brazilian Portuguese. Commit subjects in Portuguese, Verto style (`feat(books): ...`), ending with the `Co-Authored-By` trailer.
- Error body is always `{ "error": "<snake_code>", "message": "<pt-BR>" }` (Verto's `HttpResults`).
- JSON: camelCase properties; enums as snake_case lowercase strings (`not_started`, `wishlist`) via `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`; dates as `DateOnly` (`"2026-10-16"`).
- Every query is scoped by the `sub` claim's user id. Another user's resource answers **404**, never 403.
- "Today" always comes from the client in the request body (`today`), never `DateTime.UtcNow` — the API runs in UTC, the user in America/Sao_Paulo.
- Creating a book **never** auto-fills `startedAt`/`finishedAt` (spec). Only the status change, the reading log and "Comprei" rules fill dates.
- Signup gated by `Auth:AllowSignup` (default `false`); `Jwt:RefreshTokenDays = 90`.
- Local dev Postgres on port **5434** (5432 = BillFolder, 5433 = Verto). Local API on `http://localhost:5090`.
- Production: host `libris-api.billfolder.app`, image `ghcr.io/jesstoselli/libris-api:latest`, container port 8080 published on `127.0.0.1:8082`, database/role `libris`, covers volume mounted at `/data/covers`.
- CORS origins from config `Cors:AllowedOrigins` (array); defaults: `tauri://localhost`, `http://tauri.localhost`, `http://localhost:1420`.

## Review Focus

1. **Accents in search** — typing `assombracao` or `ECO` must find "A Assombração da Casa da Colina" / "Umberto Eco". Pinned by `List_Search_IgnoresCaseAndAccents` (Task 5).
2. **Correcting a page backwards** — logging 120 after 140 must count 0 pages for that log (not −20) and never make a month negative. Pinned by `Deltas_LowerPageThanPrevious_CountsZero` (Task 3) and `Year_CorrectionBackwards_DoesNotSubtract` (Task 9).
3. **Two logs on the same day** — the later-created one is the current page and deltas chain in creation order. Pinned by `Deltas_SameDate_OrderedByCreation` (Task 3).
4. **Old book registered as Lido in 2024** — counts in 2024's finished books, adds no pages, and does not appear in the current year. Pinned by `Create_ReadWithPastDates_KeepsDatesAndCountsInThatYear` (Task 5) and `Year_OldBookWithoutLogs_CountsFinishedButNoPages` (Task 9).
5. **Guessing another user's ids** — books, logs, covers attach and genres of user B are invisible to user A (404) and never leak in lists. Pinned by `OtherUsersBook_Returns404` (Task 5) and `Log_OnOtherUsersBook_Returns404` (Task 6).

---

### Task 1: Solution skeleton, health check, CORS

**Files:**
- Create (copied from Verto, renamed): `.gitignore`, `.editorconfig`, `.dockerignore`, `dotnet-tools.json`, `Directory.Build.props`, `Libris.slnx`, `src/Libris.Domain/Libris.Domain.csproj`, `src/Libris.Application/Libris.Application.csproj`, `src/Libris.Infrastructure/Libris.Infrastructure.csproj`, `src/Libris.Infrastructure/DependencyInjection.cs`, `src/Libris.Infrastructure/Persistence/ApplicationDbContext.cs`, `src/Libris.Api/Libris.Api.csproj`, `src/Libris.Api/Program.cs`, `src/Libris.Api/appsettings.json`, `src/Libris.Api/Properties/launchSettings.json`, `tests/Libris.Api.Tests/Libris.Api.Tests.csproj`, `tests/Libris.Api.Tests/Infrastructure/ApiWebApplicationFactory.cs`
- Create: `docker-compose.dev.yml`, `README.md`
- Create: `tests/Libris.Domain.Tests/Libris.Domain.Tests.csproj` (xUnit + FluentAssertions, references `Libris.Domain` only)
- Test: `tests/Libris.Api.Tests/Health/HealthEndpointTests.cs`, `tests/Libris.Api.Tests/Health/CorsTests.cs`

**Interfaces:**
- Produces: `ApiWebApplicationFactory` (class fixture, `Create(overrides)`), `AddInfrastructure(IServiceCollection, IConfiguration)`, `ApplicationDbContext`, `GET /v1/health → { status, version, timestamp }`, config keys `ConnectionStrings:Postgres`, `Database:MigrateOnStartup`, `Cors:AllowedOrigins`.

- [ ] **Step 1: Copy the skeleton from Verto** at the state of its Task 1 (no auth, no sync): solution with the 4 `src` projects + `tests/Libris.Api.Tests`, `Program.cs` with only Kestrel, infrastructure, `/v1/health`, `public partial class Program;`. Add `tests/Libris.Domain.Tests` to `Libris.slnx`. `docker-compose.dev.yml`: Verto's, with `container_name: libris-postgres-dev`, db/user `libris`, password `libris_dev`, port `"5434:5432"`, volume `libris_postgres_dev_data`. `launchSettings.json` `http` profile on `http://localhost:5090`. `appsettings.json` connection string `Host=localhost;Port=5434;Database=libris;Username=libris;Password=libris_dev`. Testcontainers label `com.libris.test-suite`.

- [ ] **Step 2: Write failing tests**

`HealthEndpointTests.Health_ReturnsOkWithDatabaseStatus`: `GET /v1/health` → 200, body `status == "ok"`.

`CorsTests.Preflight_FromTauriOrigin_IsAllowed`: `OPTIONS /v1/health` with `Origin: tauri://localhost` and `Access-Control-Request-Method: GET` → response header `Access-Control-Allow-Origin == "tauri://localhost"`.
`CorsTests.Preflight_FromUnknownOrigin_IsNotAllowed`: same with `Origin: https://evil.example` → no `Access-Control-Allow-Origin` header.

- [ ] **Step 3: Run** `docker compose -f docker-compose.dev.yml up -d && dotnet test` — Expected: Cors tests FAIL (no CORS yet), health PASS.

- [ ] **Step 4: Add CORS in `Program.cs`**: default policy from `Cors:AllowedOrigins` (`string[]`, defaults above in `appsettings.json`), `AllowAnyHeader()`, `AllowAnyMethod()`; `app.UseCors()` before auth.

- [ ] **Step 5: Run** `dotnet build && dotnet test` — Expected: 0 warnings, all PASS.

- [ ] **Step 6: Commit** `feat: esqueleto da API com health check e CORS`

---

### Task 2: Auth copied from Verto + login rate limit per IP

**Files:**
- Create (copied from Verto, renamed — every file Verto's auth uses): `src/Libris.Domain/Entities/{User,RefreshToken}.cs`, `src/Libris.Application/Abstractions/Auth/{IJwtTokenService,IPasswordHasher}.cs`, `src/Libris.Application/Abstractions/Persistence/IApplicationDbContext.cs`, `src/Libris.Application/Common/OperationResult.cs`, `src/Libris.Application/Dtos/Auth/AuthDtos.cs`, `src/Libris.Application/Validators/Auth/AuthValidators.cs`, `src/Libris.Application/UseCases/Auth/{AuthOptions,AuthService}.cs`, `src/Libris.Infrastructure/Auth/{Argon2idPasswordHasher,JwtOptions,JwtTokenService}.cs`, `src/Libris.Infrastructure/Persistence/Configurations/{User,RefreshToken}Configuration.cs`, `src/Libris.Api/Endpoints/{HttpResults,AuthEndpoints,UsersEndpoints}.cs`, `src/Libris.Api/Extensions/ClaimsPrincipalExtensions.cs`
- Create: `src/Libris.Infrastructure/Migrations/*_InitialAuth.cs` (generated)
- Modify: `src/Libris.Api/Program.cs`, `ApplicationDbContext.cs`, `DependencyInjection.cs`, `ApiWebApplicationFactory.cs` (Jwt test config)
- Create (copied): `tests/Libris.Api.Tests/Infrastructure/AuthTestClient.cs`
- Test (copied): `tests/Libris.Api.Tests/Auth/{AuthEndpointsTests,AuthRateLimitTests,SignupDisabledTests}.cs`; new `tests/Libris.Api.Tests/Auth/LoginRateLimitTests.cs`

**Interfaces:**
- Produces: `/v1/auth/{signup,login,refresh,logout}`, `GET /v1/me`; `AuthResponse`, `UserDto`, `LoginRequest`, etc. (same shapes as Verto); `OperationResult<T>`, `OperationResult.Ok/Fail`; `HttpResults.ToHttpResult<T>`; `ClaimsPrincipal.TryGetUserId(out Guid)`; `User` entity (table `users`); `AuthTestClient.CreateAuthorizedClientAsync(factory)`; JWT issuer `libris`, audience `libris-app`.
- Also produces: `JsonOptions` configured in `Program.cs` with the enum converter from Global Constraints (later tasks rely on it).

- [ ] **Step 1: Copy Verto's auth tests and `AuthTestClient`**, renamed. Write the new test `LoginRateLimitTests.Login_OverPerIpLimit_Returns429` using `ApiWebApplicationFactory.Create(new() { ["Auth:LoginPermitPerMinute"] = "3" })`: 3 wrong-password logins → 401 each, the 4th → 429.

- [ ] **Step 2: Run** `dotnet test --filter Auth` — Expected: FAIL (no auth code).

- [ ] **Step 3: Copy Verto's auth implementation**, renamed, and generate the migration: `dotnet ef migrations add InitialAuth -p src/Libris.Infrastructure -s src/Libris.Api`.

- [ ] **Step 4: Add the login limiter in `Program.cs`**: policy `"login-per-ip"`, `PartitionedRateLimiter` fixed window, partition key = `HttpContext.Connection.RemoteIpAddress`, permit `Auth:LoginPermitPerMinute` (default 10), window 1 min, queue 0; apply with `.RequireRateLimiting("login-per-ip")` on the login endpoint only, keeping Verto's concurrency limiter on the group. Add `app.UseForwardedHeaders()` (first middleware) with `ForwardedHeaders.XForwardedFor | XForwardedProto`, `KnownNetworks`/`KnownProxies` cleared and `ForwardLimit = 1` — the container port is bound to `127.0.0.1` so only nginx can reach it, and without this every request looks like it comes from the Docker gateway and shares one rate-limit bucket. Configure `ConfigureHttpJsonOptions` with the enum converter.

- [ ] **Step 5: Run** `dotnet test` — Expected: all PASS.

- [ ] **Step 6: Commit** `feat(auth): auth copiado do Verto com limite de login por IP`

---

### Task 3: Domain — book rules, page deltas, ISBN

Pure code, no EF, no I/O. Unit-tested in `Libris.Domain.Tests`.

**Files:**
- Create: `src/Libris.Domain/Entities/{Book,Genre,ReadingLog,Cover}.cs`, `src/Libris.Domain/Enums/{BookOwnership,BookStatus}.cs`
- Create: `src/Libris.Domain/Reading/ReadingProgress.cs`, `src/Libris.Domain/Books/Isbn.cs`
- Test: `tests/Libris.Domain.Tests/Books/BookRulesTests.cs`, `tests/Libris.Domain.Tests/Reading/ReadingProgressTests.cs`, `tests/Libris.Domain.Tests/Books/IsbnTests.cs`

**Interfaces:**
- Produces:
  - `enum BookOwnership { Owned, Wishlist }`, `enum BookStatus { NotStarted, Reading, Read, Abandoned }`
  - `Book`: `Guid Id, Guid UserId, string Title, List<string> Authors, string? Isbn, int? TotalPages, string? Publisher, int? PublishedYear, string? Synopsis, Guid? CoverId, List<Genre> Genres, BookOwnership Ownership, BookStatus? Status, DateOnly? StartedAt, DateOnly? FinishedAt, DateOnly AddedAt, List<ReadingLog> Logs, DateTime CreatedAt, DateTime UpdatedAt`
  - `void Book.ChangeStatus(BookStatus status, DateOnly today)`
  - `void Book.ApplyNewLog(ReadingLog log)` — call after adding the log
  - `OperationResult`-free domain errors: `Book.Acquire(DateOnly today, int? totalPages)` returns `bool` (false when the book has no `TotalPages` and none was given; caller maps to `total_pages_required`); throws `InvalidOperationException` if already owned.
  - `Genre`: `Guid Id, Guid UserId, string Name, List<Book> Books`
  - `ReadingLog`: `Guid Id, Guid BookId, DateOnly Date, int Page, DateTime CreatedAt`
  - `Cover`: `Guid Id, Guid UserId, DateTime CreatedAt`
  - `static IReadOnlyList<LogDelta> ReadingProgress.Deltas(IEnumerable<ReadingLog> logs)` with `record LogDelta(ReadingLog Log, int PagesRead)` — ordered by `(Date, CreatedAt)` ascending
  - `static int ReadingProgress.CurrentPage(IEnumerable<ReadingLog> logs)`; `static int ReadingProgress.Percent(int currentPage, int? totalPages)` (0 when `totalPages` is null/0; rounded down; capped 100)
  - `static bool Isbn.TryNormalize(string? input, out string isbn13)`

- [ ] **Step 1: Write failing tests**

`ReadingProgressTests` (build logs with explicit `Date`/`CreatedAt`):
- `Deltas_FirstLogCountsFromZero`: one log page 20 → `PagesRead == 20`.
- `Deltas_ChainInDateOrder_RegardlessOfInputOrder`: pages 20 @ 2026-10-02, 51 @ 2026-10-06, 86 @ 2026-10-11, passed in shuffled order → deltas `[20, 31, 35]`.
- `Deltas_SameDate_OrderedByCreation`: 2026-10-14 p100 created 10:00, 2026-10-14 p112 created 21:00 → deltas `[100, 12]`; `CurrentPage == 112`.
- `Deltas_LowerPageThanPrevious_CountsZero`: p140 then p120 → `[140, 0]`; `CurrentPage == 120`.
- `CurrentPage_NoLogs_IsZero`; `Percent_140Of512_Is27`; `Percent_NullTotal_IsZero`; `Percent_OverTotal_CapsAt100`.

`BookRulesTests` (`today = 2026-10-16`):
- `ChangeStatus_ToReading_FillsStartedWhenEmpty` and `_KeepsExistingStarted` (started 2026-10-02 stays).
- `ChangeStatus_ToRead_FillsFinishedWhenEmpty`.
- `ChangeStatus_ToAbandoned_TouchesNoDates`.
- `ChangeStatus_ToNotStarted_ClearsBothDates`.
- `ApplyNewLog_OnNotStarted_SetsReadingAndStartedFromLogDate`: log dated 2026-10-02 → `Status == Reading`, `StartedAt == 2026-10-02`.
- `ApplyNewLog_OnNotStartedWithOldLogs_StillStartsReading` (spec decision: any log, not only the first).
- `ApplyNewLog_AtLastPage_SetsReadAndFinishedFromLogDate`: `TotalPages = 246`, log p246 @ 2026-10-08 → `Read`, `FinishedAt == 2026-10-08`.
- `ApplyNewLog_AtLastPage_KeepsExistingFinished`.
- `Acquire_SetsOwnedNotStartedAndAddedToday`; `Acquire_WithoutTotalPages_ReturnsFalseAndChangesNothing`; `Acquire_WithTotalPagesArgument_SetsIt`.

`IsbnTests` (`[Theory]`):
- valid → normalized: `"978-85-01-01279-6"`, `" 9788501012796 "` → `"9788501012796"`; ISBN-10 `"0-15-144647-4"` → `"9780151446476"`; ISBN-10 with `X` `"080442957X"` → `"9780804429573"`.
- invalid → `false`: `"9788501012797"` (bad check digit), `"12345"`, `""`, `null`, `"97885010127AB"`.

- [ ] **Step 2: Run** `dotnet test tests/Libris.Domain.Tests` — Expected: FAIL (types missing).

- [ ] **Step 3: Implement** the entities, enums, `ReadingProgress`, `Isbn` and the `Book` methods per the spec's "Regras". `ChangeStatus` only applies to owned books (throw `InvalidOperationException` on wishlist). `ApplyNewLog` checks "last page" against `TotalPages` and, for status, uses the spec rule order: not_started → reading first, then last page → read.

- [ ] **Step 4: Run** `dotnet test tests/Libris.Domain.Tests` — Expected: all PASS.

- [ ] **Step 5: Commit** `feat(domain): regras de leitura, deltas de página e ISBN`

---

### Task 4: Persistence model and genres

**Files:**
- Create: `src/Libris.Infrastructure/Persistence/Configurations/{Book,Genre,ReadingLog,Cover}Configuration.cs`
- Create: `src/Libris.Infrastructure/Migrations/*_Library.cs` (generated)
- Modify: `IApplicationDbContext.cs` (add `DbSet<Book> Books`, `DbSet<Genre> Genres`, `DbSet<ReadingLog> ReadingLogs`, `DbSet<Cover> Covers`), `ApplicationDbContext.cs`
- Create: `src/Libris.Application/Dtos/Genres/GenreDtos.cs`, `src/Libris.Application/UseCases/Genres/GenreService.cs`, `src/Libris.Application/Validators/Genres/GenreValidators.cs`, `src/Libris.Api/Endpoints/GenresEndpoints.cs`
- Modify: `HttpResults.cs` (new codes), `Program.cs`, DI
- Test: `tests/Libris.Api.Tests/Genres/GenresEndpointsTests.cs`

**Interfaces:**
- Consumes: entities (Task 3), `OperationResult`, `HttpResults`, `TryGetUserId` (Task 2).
- Produces:
  - Tables `books` (authors as `text[]`), `genres`, `book_genres` (skip navigation), `reading_logs` (FK cascade on book delete), `covers`. Unique indexes: `genres (user_id, lower(name))`, `books (user_id, isbn) WHERE isbn IS NOT NULL`. Migration enables `unaccent` (`HasPostgresExtension("unaccent")`).
  - `GenreDto(Guid Id, string Name, int BookCount)`, `GenreRequest(string Name)`
  - `GET /v1/genres` (ordered by name), `POST /v1/genres`, `PUT /v1/genres/{id}`, `DELETE /v1/genres/{id}` (204; removes the genre from its books, books stay)
  - `Task<IReadOnlyList<Genre>> GenreService.ResolveByNamesAsync(Guid userId, IEnumerable<string> names, CancellationToken ct)` — trims, drops blanks, dedupes case-insensitively, finds existing case-insensitively, creates the missing ones (keeps the user's casing). Task 5 uses it.
  - Error codes: `genre_already_exists` → 409, `not_found` → 404.

- [ ] **Step 1: Write failing tests** in `GenresEndpointsTests`:
  - `Create_ThenList_ReturnsGenreWithZeroBooks`.
  - `Create_SameNameDifferentCase_Returns409` (`"Terror"` then `"terror"`).
  - `Create_BlankName_Returns400`; name > 60 chars → 400.
  - `Rename_ToExistingName_Returns409`.
  - `Delete_RemovesGenre_Returns204`.
  - `OtherUsersGenre_UpdateAndDelete_Return404` (second authorized client).

- [ ] **Step 2: Run** `dotnet test --filter Genres` — Expected: FAIL.

- [ ] **Step 3: Implement** configurations (snake_case table/column names as Verto), migration `dotnet ef migrations add Library ...`, `GenreService` + validators + endpoints (group `/v1/genres`, `RequireAuthorization()`), new codes in `HttpResults`.

- [ ] **Step 4: Run** `dotnet test` — Expected: all PASS.

- [ ] **Step 5: Commit** `feat(genres): modelo do acervo e CRUD de gêneros`

---

### Task 5: Books — CRUD, list filters, status, "Comprei"

**Files:**
- Create: `src/Libris.Application/Dtos/Books/BookDtos.cs`, `src/Libris.Application/UseCases/Books/BookService.cs`, `src/Libris.Application/Validators/Books/BookValidators.cs`, `src/Libris.Api/Endpoints/BooksEndpoints.cs`
- Modify: `HttpResults.cs`, `Program.cs`, DI
- Create: `tests/Libris.Api.Tests/Infrastructure/LibraryTestData.cs`
- Test: `tests/Libris.Api.Tests/Books/{BooksCrudTests,BooksListTests,BookStatusTests,BookAcquireTests}.cs`

**Interfaces:**
- Consumes: `Book` rules, `ReadingProgress`, `Isbn.TryNormalize` (Task 3); `GenreService.ResolveByNamesAsync` (Task 4).
- Produces (app contract):
  - `BookDto(Guid Id, string Title, IReadOnlyList<string> Authors, string? Isbn, int? TotalPages, string? Publisher, int? PublishedYear, string? Synopsis, Guid? CoverId, string? CoverUrl, IReadOnlyList<GenreRefDto> Genres, BookOwnership Ownership, BookStatus? Status, DateOnly? StartedAt, DateOnly? FinishedAt, DateOnly AddedAt, int CurrentPage, int Percent)`; `GenreRefDto(Guid Id, string Name)`; `CoverUrl = "/v1/covers/{coverId}"` (relative).
  - `CreateBookRequest(string Title, IReadOnlyList<string> Authors, string? Isbn, int? TotalPages, string? Publisher, int? PublishedYear, string? Synopsis, Guid? CoverId, IReadOnlyList<string> Genres, BookOwnership Ownership, BookStatus? Status, DateOnly? StartedAt, DateOnly? FinishedAt, DateOnly Today)` — `Genres` are **names** (find-or-create); `AddedAt = Today`; `Status` defaults to `NotStarted` when owned, must be null when wishlist.
  - `UpdateBookRequest(string Title, IReadOnlyList<string> Authors, string? Isbn, int? TotalPages, string? Publisher, int? PublishedYear, string? Synopsis, Guid? CoverId, IReadOnlyList<string> Genres, DateOnly? StartedAt, DateOnly? FinishedAt)` — full replace of these fields; status/ownership unchanged.
  - `ChangeStatusRequest(BookStatus Status, DateOnly Today)`; `AcquireRequest(DateOnly Today, int? TotalPages)`.
  - `GET /v1/books?ownership=owned|wishlist&status=&genreId=&q=` → `BookDto[]` ordered by title; `POST /v1/books` → 201 + `BookDto`; `GET|PUT|DELETE /v1/books/{id}` (DELETE → 204); `PATCH /v1/books/{id}/status` → `BookDto`; `POST /v1/books/{id}/acquire` → `BookDto`.
  - In this task `CoverId` is stored as given; Task 7 adds the ownership check and file cleanup.
  - `BookService.ToDto(Book)` used by Tasks 6 and 7.
  - Error codes: `invalid_isbn` → 400, `isbn_already_registered` → 409 (message names the existing book: `"Esse ISBN já está cadastrado: <título>."`), `total_pages_required` → 400, `not_owned` → 400 (status change on wishlist), `already_owned` → 409.
  - Test helper: `LibraryTestData.CreateBookAsync(HttpClient, Action<CreateBookRequestBuilder>? configure = null) → Task<BookDto>` with sane defaults (title "O Nome da Rosa", author "Umberto Eco", 512 pages, owned, today 2026-10-16).

Validation (FluentValidation): title 1–300 chars; 1–10 authors, each 1–200; `TotalPages` 1–20000, required when owned; `PublishedYear` 1400–2100; synopsis ≤ 5000; ≤ 10 genres each ≤ 60; `FinishedAt >= StartedAt` when both; dates not after `Today` on create.

- [ ] **Step 1: Write failing tests**

`BooksCrudTests`:
- `Create_Owned_DefaultsNotStartedAndAddedToday` (no dates filled).
- `Create_ReadWithPastDates_KeepsDatesAndCountsInThatYear` — create with `Status = Read, StartedAt 2024-03-01, FinishedAt 2024-03-20` → returned dates unchanged (stats half is asserted in Task 9).
- `Create_ReadWithoutDates_LeavesDatesEmpty`.
- `Create_WithGenreNames_ReusesExistingCaseInsensitive` — existing "Terror"; create with `["terror", "Gótico"]` → 2 genres, `GET /v1/genres` has 2 entries, "Terror" keeps its casing.
- `Create_IsbnHyphenated_IsStoredAs13Digits`; `Create_InvalidIsbn_Returns400`.
- `Create_DuplicateIsbn_Returns409WithTitleInMessage` — also when the existing one is on the wishlist.
- `Create_Wishlist_WithoutPages_IsAccepted_AndStatusNull`.
- `Update_ReplacesFieldsAndGenres_KeepsStatus`.
- `Delete_Returns204_AndGetReturns404`.
- `OtherUsersBook_Returns404` for GET, PUT, DELETE, PATCH status, acquire.

`BooksListTests` (seed: Nome da Rosa/reading/mistério, Drácula/read/terror, Assombração/not_started/terror, Rebecca/wishlist):
- `List_DefaultOwnership_ReturnsAllOwnedOrderedByTitle` — ownership omitted means owned; wishlist excluded.
- `List_StatusFilter`, `List_GenreFilter`, `List_FiltersCombine` (terror + not_started → only Assombração).
- `List_Wishlist_ReturnsOnlyWishlist`.
- `List_Search_IgnoresCaseAndAccents` — `q=assombracao` → Assombração; `q=ECO` → Nome da Rosa (matches author).
- `List_IncludesProgress` — after a log (Task 6 not yet — seed via DbContext in the test) `CurrentPage`/`Percent` set.

`BookStatusTests`: `Patch_ToRead_FillsFinishedWithRequestToday`; `Patch_ToNotStarted_ClearsDates`; `Patch_OnWishlist_Returns400`.

`BookAcquireTests`: `Acquire_MovesToOwnedNotStarted_AddedToday`; `Acquire_WithoutPages_Returns400TotalPagesRequired`; `Acquire_WithPagesInRequest_Succeeds`; `Acquire_AlreadyOwned_Returns409`.

- [ ] **Step 2: Run** `dotnet test --filter Books` — Expected: FAIL.

- [ ] **Step 3: Implement** `BookService` (`ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ChangeStatusAsync`, `AcquireAsync`) and `BooksEndpoints`. Search: `EF.Functions.ILike(EF.Functions.Unaccent(b.Title), EF.Functions.Unaccent("%" + q + "%"))` OR the same over `array_to_string(authors, ' ')` (map via `EF.Functions` or a `HasDbFunction`; escape `%`/`_` in `q`). Load `Logs` with the books to compute `CurrentPage`/`Percent` (single-user scale; no projection tricks needed).

- [ ] **Step 4: Run** `dotnet test` — Expected: all PASS.

- [ ] **Step 5: Commit** `feat(books): CRUD, filtros, situação e "Comprei"`

---

### Task 6: Reading logs

**Files:**
- Create: `src/Libris.Application/Dtos/Reading/ReadingLogDtos.cs`, `src/Libris.Application/UseCases/Reading/ReadingLogService.cs`, `src/Libris.Application/Validators/Reading/ReadingLogValidators.cs`, `src/Libris.Api/Endpoints/ReadingLogsEndpoints.cs`
- Test: `tests/Libris.Api.Tests/Reading/ReadingLogsTests.cs`

**Interfaces:**
- Consumes: `Book.ApplyNewLog`, `ReadingProgress.Deltas` (Task 3); `BookService.ToDto` (Task 5).
- Produces:
  - `ReadingLogDto(Guid Id, DateOnly Date, int Page, int FromPage, int PagesRead)` — `FromPage` is the previous log's page (0 for the first), not `Page − PagesRead`, which breaks after a backwards correction.
  - `ReadingLogRequest(DateOnly Date, int Page)`; `ReadingLogResult(BookDto Book, IReadOnlyList<ReadingLogDto> Logs)`.
  - `GET /v1/books/{id}/logs` → `ReadingLogDto[]` newest first (date desc, created desc).
  - `POST /v1/books/{id}/logs` → 201 `ReadingLogResult`; `PUT /v1/books/{id}/logs/{logId}` → `ReadingLogResult`; `DELETE /v1/books/{id}/logs/{logId}` → `ReadingLogResult` (200, so the app refreshes progress in one round trip).
  - Error codes: `page_out_of_range` → 400 (`Page < 1` or `> TotalPages`), `not_owned` → 400 (wishlist book).

Rules: POST runs `Book.ApplyNewLog`; PUT and DELETE only recalculate progress — status and dates never roll back (spec).

- [ ] **Step 1: Write failing tests** in `ReadingLogsTests`:
  - `FirstLog_OnNotStarted_StartsReadingWithLogDate` (log 2026-10-02 p20 → book `reading`, `startedAt 2026-10-02`, `currentPage 20`).
  - `LogAtLastPage_MarksReadWithLogDate`.
  - `List_ReturnsNewestFirst_WithFromAndPagesRead` — logs p20 @10-02, p51 @10-06, p86 @10-11 → first item `{ Page 86, FromPage 51, PagesRead 35 }`.
  - `Log_PageAboveTotal_Returns400`; `Log_PageZero_Returns400`; `Log_OnWishlistBook_Returns400`.
  - `Delete_LastLog_RecalculatesProgress_KeepsStatus` — after logging the last page (read), delete it → `currentPage` back, status stays `read`.
  - `Update_ChangesPageAndRecalculates`.
  - `Log_OnOtherUsersBook_Returns404`; `Update_LogOfAnotherBook_Returns404` (logId exists but under a different bookId).

- [ ] **Step 2: Run** `dotnet test --filter Reading` — Expected: FAIL.
- [ ] **Step 3: Implement** service + endpoints.
- [ ] **Step 4: Run** `dotnet test` — Expected: all PASS.
- [ ] **Step 5: Commit** `feat(reading): registros de leitura com progresso e regras de situação`

---

### Task 7: Covers — upload, download from URL, public serving, cleanup

**Files:**
- Create: `src/Libris.Application/Abstractions/Covers/ICoverStorage.cs`, `src/Libris.Application/UseCases/Covers/CoverService.cs`, `src/Libris.Application/Dtos/Covers/CoverDtos.cs`
- Create: `src/Libris.Infrastructure/Covers/{FileSystemCoverStorage,CoverImageProcessor,CoverOptions,CoverJanitor}.cs`
- Create: `src/Libris.Api/Endpoints/CoversEndpoints.cs`
- Modify: `BookService.cs` (validate `CoverId` belongs to the user; delete the old cover on replace and on book delete), `Libris.Infrastructure.csproj` (ImageSharp), DI, `Program.cs` (Kestrel/form limit 6 MB)
- Create: `tests/Libris.Api.Tests/Covers/Fixtures/{cover.jpg,cover.png,not-an-image.txt}` (small generated files)
- Test: `tests/Libris.Api.Tests/Covers/CoversTests.cs`

**Interfaces:**
- Produces:
  - `ICoverStorage`: `Task SaveAsync(Guid coverId, Stream webp, CancellationToken ct)`, `Stream? OpenRead(Guid coverId)`, `void Delete(Guid coverId)`.
  - `CoverOptions`: `Covers:Path` (default `/data/covers`; tests use a temp dir), `Covers:MaxUploadBytes` = 5 MB, `Covers:MaxHeight` = 600, `Covers:AllowedDownloadHosts` = `covers.openlibrary.org`, `books.google.com`, `books.googleusercontent.com`.
  - `CoverDto(Guid CoverId, string Url)`; `CoverFromUrlRequest(string Url)`.
  - `POST /v1/covers` (auth; multipart field `file`) → 201 `CoverDto`; `POST /v1/covers/from-url` (auth; JSON) → 201 `CoverDto`; `GET /v1/covers/{id}` (**anonymous**) → `image/webp`, `Cache-Control: public, max-age=31536000, immutable`; 404 if missing.
  - Stored format: WebP, resized to height ≤ `MaxHeight` keeping aspect (never upscaled), EXIF stripped.
  - Error codes: `invalid_image` → 400, `image_too_large` → 413, `host_not_allowed` → 400, `download_failed` → 502, `cover_not_found` → 400 (when a book references a cover that isn't the user's).
  - `CoverJanitor`: `BackgroundService`, runs every 24 h; deletes covers (row + file) with no book referencing them and `CreatedAt` older than 24 h.

- [ ] **Step 1: Write failing tests** in `CoversTests` (factory override `Covers:Path` = per-test temp dir; the from-url tests replace the named `HttpClient` with a stub handler):
  - `Upload_Jpeg_StoresWebpResizedTo600High` — 900×1350 JPEG → GET returns `image/webp`, decoded height 600.
  - `Upload_SmallImage_IsNotUpscaled` (200×300 stays 300 high).
  - `Upload_NotAnImage_Returns400`; `Upload_Over5MB_Returns413`.
  - `Get_IsAnonymous_WithImmutableCache`.
  - `FromUrl_AllowedHost_Downloads`; `FromUrl_OtherHost_Returns400` (`https://evil.example/x.jpg`); `FromUrl_UpstreamFails_Returns502`.
  - `Book_WithOtherUsersCover_Returns400`.
  - `Book_ReplaceCover_DeletesOldFile`; `Book_Delete_DeletesCoverFile`.
  - `Janitor_RemovesOnlyOldUnreferencedCovers` — call the janitor's single-pass method (`Task SweepAsync(DateTime now, CancellationToken ct)`) directly.

- [ ] **Step 2: Run** `dotnet test --filter Covers` — Expected: FAIL.
- [ ] **Step 3: Implement**. Upload flow: read up to `MaxUploadBytes + 1` (reject above), `Image.LoadAsync` (catch `UnknownImageFormatException`/`InvalidImageContentException` → `invalid_image`), `Mutate(Resize)` only when taller, `SaveAsWebpAsync`, insert `Cover` row, write file `{Path}/{coverId:N}.webp`. From-url: `https` only, host in allow-list, `HttpClient` named `"covers"` with 10 s timeout, same pipeline.
- [ ] **Step 4: Run** `dotnet test` — Expected: all PASS.
- [ ] **Step 5: Commit** `feat(covers): capas em disco, redimensionadas e servidas públicas`

---

### Task 8: ISBN lookup

**Files:**
- Create: `src/Libris.Application/Abstractions/Isbn/IIsbnSource.cs`, `src/Libris.Application/UseCases/Isbn/IsbnLookupService.cs`, `src/Libris.Application/Dtos/Isbn/IsbnDtos.cs`
- Create: `src/Libris.Infrastructure/Isbn/{OpenLibraryIsbnSource,GoogleBooksIsbnSource,GenreSuggestions}.cs`
- Create: `src/Libris.Api/Endpoints/IsbnEndpoints.cs`
- Create: `tests/Libris.Api.Tests/Isbn/Fixtures/*.json` — recorded real responses for: Open Library hit with pages + subjects (`/isbn/9788501012796.json`, its `/authors/*.json`, `/works/*.json`), Open Library hit **without** `number_of_pages`, Open Library 404, Google Books hit, Google Books `totalItems: 0`.
- Test: `tests/Libris.Api.Tests/Isbn/IsbnLookupTests.cs`

**Interfaces:**
- Produces:
  - `IIsbnSource`: `Task<IsbnCandidate?> FindAsync(string isbn13, CancellationToken ct)`; `record IsbnCandidate(string? Title, IReadOnlyList<string> Authors, int? TotalPages, string? Publisher, int? PublishedYear, string? Synopsis, string? CoverUrl, IReadOnlyList<string> Subjects)`.
  - `IsbnLookupDto(string Isbn, string Title, IReadOnlyList<string> Authors, int? TotalPages, string? Publisher, int? PublishedYear, string? Synopsis, string? CoverUrl, IReadOnlyList<string> SuggestedGenres, string Source)` — `Source` is `"open_library"`, `"google_books"` or `"open_library+google_books"`.
  - `GET /v1/isbn/{isbn}` (auth) → 200 `IsbnLookupDto`; `invalid_isbn` → 400; `isbn_not_found` → 404; both sources failing at network level → `isbn_lookup_unavailable` 503.
- Merge rule: Open Library first; if its result lacks `Title` or `TotalPages`, query Google Books and fill only the missing fields (and add its categories to subjects). Open Library cover: `https://covers.openlibrary.org/b/isbn/{isbn13}-L.jpg?default=false`. Google cover: `imageLinks.thumbnail` with `http:` → `https:` and `&zoom=1` kept.
- `GenreSuggestions.From(IEnumerable<string> subjects) → IReadOnlyList<string>`: split Google's `"Fiction / Horror"` on `/`, trim, drop entries longer than 40 chars, drop ones containing `"Accessible book"`, `"Protected DAISY"`, `"In library"`, `"Fiction"` alone, `"nyt:"`, `"Large type"`; dedupe case-insensitively; first 5.

- [ ] **Step 1: Write failing tests** (sources wired to stub handlers that serve the fixtures):
  - `Lookup_OpenLibraryComplete_DoesNotCallGoogle` (assert Google handler call count 0; `Source == "open_library"`).
  - `Lookup_OpenLibraryWithoutPages_FillsFromGoogle` (`Source == "open_library+google_books"`, title from OL, pages from Google).
  - `Lookup_OnlyGoogleHas_ReturnsGoogle`.
  - `Lookup_NeitherHas_Returns404`.
  - `Lookup_Isbn10_IsNormalizedBeforeQuerying` (handler sees the 13-digit ISBN in the URL).
  - `Lookup_Invalid_Returns400`.
  - `Lookup_BothSourcesThrow_Returns503`.
  - `Suggestions_FilterNoiseAndLimitToFive` (unit-level over `GenreSuggestions.From`).
- [ ] **Step 2: Run** `dotnet test --filter Isbn` — Expected: FAIL.
- [ ] **Step 3: Implement** typed clients (`AddHttpClient<OpenLibraryIsbnSource>` base `https://openlibrary.org/`, `AddHttpClient<GoogleBooksIsbnSource>` base `https://www.googleapis.com/books/v1/`, 8 s timeout each, `User-Agent: Libris/1.0 (personal use)`). Open Library: read `title`, `number_of_pages`, `publishers[0]`, year parsed from `publish_date`, author names via `authors[].key` → `/authors/{id}.json`, synopsis + subjects from `works[0].key` → `/works/{id}.json` (`description` is either a string or `{ value }`).
- [ ] **Step 4: Run** `dotnet test` — Expected: all PASS.
- [ ] **Step 5: Commit** `feat(isbn): busca de metadados por ISBN (Open Library + Google Books)`

---

### Task 9: Stats

**Files:**
- Create: `src/Libris.Application/Dtos/Stats/StatsDtos.cs`, `src/Libris.Application/UseCases/Stats/StatsService.cs`, `src/Libris.Api/Endpoints/StatsEndpoints.cs`
- Test: `tests/Libris.Api.Tests/Stats/StatsTests.cs`

**Interfaces:**
- Consumes: `ReadingProgress.Deltas` (Task 3).
- Produces:
  - `YearStatsDto(int Year, int BooksFinished, int PagesRead, int ReadingNow, IReadOnlyList<MonthStatsDto> Months)` — `Months` always 12 entries; `MonthStatsDto(int Month, int PagesRead, int BooksFinished)`.
  - `MonthDetailDto(int Year, int Month, int PagesRead, IReadOnlyList<FinishedBookDto> Finished, IReadOnlyList<ProgressedBookDto> Progressed)`; `FinishedBookDto(Guid BookId, string Title, IReadOnlyList<string> Authors, DateOnly FinishedAt)` ordered by `FinishedAt` desc; `ProgressedBookDto(Guid BookId, string Title, int PagesRead)` ordered by `PagesRead` desc, only `PagesRead > 0`.
  - `GET /v1/stats/{year}` (year 1900–2100 else 400), `GET /v1/stats/{year}/{month}` (month 1–12 else 400).
- Definitions are the spec's "Estatísticas" section verbatim: finished counts by `FinishedAt` (owned books with status `read` only), pages by the sum of deltas whose log `Date` falls in the period, `ReadingNow` = owned books with status `reading` regardless of year. Compute in memory from the user's logs (single-user scale).

- [ ] **Step 1: Write failing tests** in `StatsTests` (seed through the API: books, logs, status changes):
  - `Year_SumsPagesByLogDate_AcrossBooks` — Drácula logs 2026-10-03 p100, 2026-10-15 p214; Rosa logs 2026-09-28 p40, 2026-10-02 p140 → October 214+100 = 314, September 40.
  - `Year_DeltaCrossingMonths_CountsInLogMonth` — p40 @09-28 then p140 @10-02 → the 100 go to October.
  - `Year_CorrectionBackwards_DoesNotSubtract` — p140 then p120 same month → month total 140.
  - `Year_OldBookWithoutLogs_CountsFinishedButNoPages` — created `read` with `finishedAt 2024-03-20` → `/v1/stats/2024` March `booksFinished 1`, `pagesRead 0`; `/v1/stats/2026` `booksFinished` excludes it.
  - `Year_AbandonedWithFinishedDate_IsNotCountedAsFinished`.
  - `Year_ReadingNow_IgnoresYear`.
  - `Month_Detail_ListsFinishedAndProgressed`.
  - `Stats_OtherUsersData_IsNotCounted`.
  - `Year_OutOfRange_Returns400`.
- [ ] **Step 2: Run** `dotnet test --filter Stats` — Expected: FAIL.
- [ ] **Step 3: Implement** `StatsService.GetYearAsync(Guid userId, int year, CancellationToken ct)` and `GetMonthAsync(Guid userId, int year, int month, CancellationToken ct)`.
- [ ] **Step 4: Run** `dotnet test` — Expected: all PASS.
- [ ] **Step 5: Commit** `feat(stats): números por ano e por mês`

---

### Task 10: Docker image, CI/CD and deploy runbook

**Files:**
- Create (copied from Verto, renamed): `Dockerfile`, `.github/workflows/build-and-deploy.yml`, `.github/dependabot.yml`
- Create: `docs/deploy.md` (Verto's runbook adapted)
- Modify: `README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: image `ghcr.io/jesstoselli/libris-api:latest`; runbook for Jess.

- [ ] **Step 1: Dockerfile** — Verto's with `Libris` names and both test projects in the restore layer, plus, in the runtime stage before `USER app`: `RUN mkdir -p /data/covers && chown app:app /data/covers` and `VOLUME /data/covers` (the app runs as non-root and must own the mount point, or every upload fails with permission denied).
- [ ] **Step 2: Verify locally** — `docker build -t libris-api:local .` succeeds; `docker run --rm -e ConnectionStrings__Postgres="Host=host.docker.internal;Port=5434;Database=libris;Username=libris;Password=libris_dev" -e Jwt__Key=$(openssl rand -hex 48) -e Jwt__Issuer=libris -e Jwt__Audience=libris-app -e Database__MigrateOnStartup=true -p 8090:8080 libris-api:local` then `curl -s localhost:8090/v1/health` → `"status":"ok"`.
- [ ] **Step 3: Workflow** — Verto's with the image name changed; the test job must run both test projects (Testcontainers needs Docker on the runner, as in Verto).
- [ ] **Step 4: `docs/deploy.md`** — Verto's runbook with these substitutions and additions:
  - names: `libris`, `LIBRIS_DB_PASSWORD`, `LIBRIS_JWT_KEY`, `LIBRIS_ALLOW_SIGNUP`, host `libris-api.billfolder.app`, port `127.0.0.1:8082:8080`, `mem_limit: 512m`;
  - repo created as `gh repo create libris-api --private`;
  - compose service also gets `Cors__AllowedOrigins__0: "tauri://localhost"`, `Cors__AllowedOrigins__1: "http://tauri.localhost"` and `volumes: - libris_covers:/data/covers`, plus a top-level `volumes: libris_covers:` entry (check whether the file already has a `volumes:` key — add under it, never a second one);
  - nginx `client_max_body_size 6m`;
  - after first deploy: create the account with `curl POST /v1/auth/signup`, then set `LIBRIS_ALLOW_SIGNUP=false` and `docker compose up -d libris-api`; verify signup returns 403;
  - backup at 03:30: Verto's `pg_dump` line for `libris` **and** `docker run --rm -v billfolder_libris_covers:/c -v /opt/billfolder/backups:/b alpine tar czf /b/libris-covers-$(date +%F).tgz -C /c .` (confirm the real volume name with `docker volume ls | grep libris`), same 14-day retention and log file `backup-libris.log`.
- [ ] **Step 5: Run** `dotnet test` once more and `docker compose -f docker-compose.dev.yml config --quiet` — Expected: all PASS / no output.
- [ ] **Step 6: Commit** `ci: imagem Docker, deploy e runbook da VPS`

Deploy itself (GitHub repo, secrets, DNS, VPS steps) is executed by Jess following `docs/deploy.md`; it is not part of the agent's work.
