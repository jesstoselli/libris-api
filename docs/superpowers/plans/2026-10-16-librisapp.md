# LibrisApp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Libris macOS app — an Angular app inside a Tauri 2 shell that talks to the deployed `libris-api` and covers every screen of the spec (Login, Acervo, Ficha + registro de leitura, Registrar/Editar com ISBN e capa, Wishlist, Números).

**Architecture:** Online-only client. Angular (standalone components, signals, zoneless, reactive forms) with one thin typed API service per resource under `core/api`, an auth layer (token storage + interceptor with single-flight refresh + guard), and one folder per screen under `features/`. Presentational pieces of the "mesa de biblioteca" look (fita, ficha, carimbo, campo pautado, barra de progresso, capa) live in `shared/ui` and are ported from the approved mock. Tauri adds nothing but the native window and packaging: no Rust commands.

**Tech Stack:** Angular 22.2.x (CLI `ng new`, Vitest runner, SCSS), TypeScript strict, RxJS (Angular's version), Tauri 2.12.x (`@tauri-apps/cli`), `@fontsource/aleo` + `@fontsource/yantramanav` 5.3.x. No UI kit, no state library, no chart library.

**Spec:** `docs/superpowers/specs/2026-10-16-libris-design.md` (in the `Libris` repo). Visual reference: `docs/design/telas-v5.html` + `docs/design/libris-ledger-bg.svg` + `docs/design/mock-original.webp`.

**Scope:** Plan 2 of 2. The API (plan 1) is deployed at `https://libris-api.billfolder.app`. Its contract is the source of truth for every request/response below — when in doubt read the DTOs in `~/Repos/Libris/src/Libris.Application/Dtos/**`.

## Global Constraints

- New repo `~/Repos/LibrisApp`, branch `main` for the scaffold commit, then `feat/app-v1`. Commits in Portuguese, `type(scope): subject`, with the `Co-Authored-By` trailer. Code, identifiers, comments in English; every user-facing text in Brazilian Portuguese.
- Ask before adding any npm dependency not listed in Tech Stack.
- Standalone components only, `ChangeDetectionStrategy.OnPush` everywhere, signals for component state, `inject()` over constructor params, new control flow (`@if`/`@for` with `track`). No NgModules. Subscriptions only via `takeUntilDestroyed()` or `toSignal`/async pipe.
- API base URL from `src/environments`: dev `http://localhost:5090` (local `Libris` API), prod `https://libris-api.billfolder.app`. Dev server on port **1420** (the API's CORS allows `http://localhost:1420`, `tauri://localhost`, `http://tauri.localhost`).
- JSON from the API: camelCase, enums as snake_case strings (`not_started`), dates as `YYYY-MM-DD` strings. **Never** pass a date-only string to `new Date(...)` (parses as UTC midnight → shows the previous day in Brazil). Use `core/util/dates.ts`.
- "Today" sent to the API is always the Mac's local calendar date (`todayIso()`), never derived from UTC.
- Errors: the API answers `{ error, message }`; show `message` (already pt-BR). Status 0 / network failure → "Sem conexão com o servidor." Every successful save shows a toast; no silent saves.
- Visual tokens (spec "Identidade visual", copy values verbatim): `--ghoulish-ale #E7BC89`, `--vampire-venom #6B0302`, `--pumpkin-poison #C55B31`, `--eye-of-the-wolf #DA8825`, `--haunted-forest #675B31`, `--forest-deep #3E3820`, `--paper #F7E7CC`. Desk = `libris-ledger-bg.svg` tiled over `--forest-deep` with overlay `rgba(20,16,8,.45)`. Ribbon on `--paper`, icons `--haunted-forest`, active item `--vampire-venom`. Text directly on the desk in `--ghoulish-ale`; text inside fichas in venom/ink. Aleo for titles/book names/stamps; Yantramanav 300/400 for body. No drop caps. Fichas: hard offset shadow, no blur. Port measurements and details from `telas-v5.html` 1:1 rather than reinventing them.
- Fonts bundled via `@fontsource` (no Google Fonts request at runtime).
- Tauri window: `dragDropEnabled: false` (otherwise the webview swallows file drops and the cover drop zone never sees them). Identifier `app.billfolder.libris`, product name `Libris`.

## Review Focus

1. **Date-only strings shifting a day** — `"2026-10-16"` must display as `16/10/2026` regardless of the Mac's time zone. Pinned by `formatIsoDate_doesNotShiftAcrossTimeZones` (Task 2).
2. **Access token expiring mid-session with several requests in flight** — exactly one `/v1/auth/refresh` call, all requests retried with the new token; refresh failure logs out to `/login`. Pinned by `refreshesOnceForConcurrent401s` and `refreshFailure_logsOutAndRedirects` (Task 3).
3. **API down / no network** — screens show "Sem conexão com o servidor." instead of an empty list or a frozen spinner. Pinned by `toUserMessage_status0_isNoConnection` (Task 2) and `acervo_showsErrorWhenListFails` (Task 4).
4. **Page out of range when logging** — "parei na 600" on a 512-page book shows the API message inline and keeps the typed value. Pinned by `logPanel_showsApiMessageOnPageOutOfRange` (Task 5).
5. **Cover dropped onto the window** — the drop zone receives the file (Tauri `dragDropEnabled: false`) and uploads it. Pinned by `coverDrop_uploadsDroppedFile` (Task 7) plus the manual check in Task 10.

---

### Task 1: Scaffold, visual foundation and app shell  *(Checkpoint 1)*

**Files:**
- Create (generated): `~/Repos/LibrisApp` via `npx @angular/cli@22.2 new libris-app --directory LibrisApp --style=scss --routing --ssr=false --zoneless --skip-git=false`, then `npx tauri init` (adds `src-tauri/`)
- Create: `src/environments/environment.ts`, `src/environments/environment.development.ts` (`ng generate environments`)
- Create: `src/styles/_tokens.scss`, `src/styles/_base.scss`, modify `src/styles.scss`
- Create: `public/libris-ledger-bg.svg` (copy from `~/Repos/Libris/docs/design/`)
- Create: `src/app/shared/ui/ribbon/ribbon.ts` (+ `.html`, `.scss`)
- Modify: `src/app/app.ts`, `src/app/app.html`, `src/app/app.scss`, `src/app/app.routes.ts`, `src/app/app.config.ts`
- Modify: `src-tauri/tauri.conf.json`, `package.json` (scripts)
- Create: `docs/` with copies of the spec, this plan and `docs/design/*` from the Libris repo; `README.md`
- Test: `src/app/app.spec.ts`, `src/app/shared/ui/ribbon/ribbon.spec.ts`

**Interfaces:**
- Produces: `environment.apiBaseUrl: string`; CSS custom properties from Global Constraints on `:root`; `.desk` (background + overlay), `.ficha` (paper + hard shadow) global classes; `<lb-ribbon>` with `items: RibbonItem[]` input (`{ label: string; icon: 'book' | 'bookmark' | 'chart' | 'plus' | 'exit'; link?: string; action?: () => void }`) rendering `routerLink` + `routerLinkActive="on"`; shell layout `ribbon | <router-outlet>` on the desk; placeholder routes `acervo`, `wishlist`, `numeros`, `registrar` (empty components replaced by later tasks), `''` → `acervo`.
- Component selector prefix: `lb`.

- [ ] **Step 1: Scaffold.** Run the `ng new` above from `~/Repos`; `cd LibrisApp`; `npm i -D @tauri-apps/cli@2.12` and `npm i @fontsource/aleo@5.3 @fontsource/yantramanav@5.3`; `npx tauri init` with app name `Libris`, window title `Libris`, frontend dist `../dist/libris-app/browser`, dev URL `http://localhost:1420`, dev command `npm run start -- --port 1420`, build command `npm run build`. Add scripts `"tauri": "tauri"`. In `tauri.conf.json`: `identifier: "app.billfolder.libris"`, main window `width 1280, height 820, minWidth 1000, minHeight 680, dragDropEnabled: false`.
- [ ] **Step 2: Verify the toolchain** — `npm test -- --watch=false` → the generated spec passes; `npm run tauri dev` opens a native window showing the Angular welcome page. Close it.
- [ ] **Step 3: Write failing tests**
  - `app.spec.ts` → `rendersRibbonAndOutlet`: shell renders `lb-ribbon` with labels `Acervo`, `Wishlist`, `Números`, `Registrar`, `Sair` and a `router-outlet`.
  - `ribbon.spec.ts` → `marksActiveRoute`: with router at `/wishlist`, the Wishlist item has class `on`; `callsActionItem`: clicking an item with `action` calls it and does not navigate.
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: the two new specs FAIL.
- [ ] **Step 5: Implement** tokens, fonts (`@use` the `@fontsource` CSS for Aleo 400/600/700/800 and Yantramanav 300/400/500 in `styles.scss`), `.desk`/`.ficha`, ribbon (port the ribbon markup/CSS of `telas-v5.html`: 64px wide, V tip via `clip-path`, inline SVG icons) and the shell. `Sair` gets a no-op action until Task 3. `environment.development.ts` → `http://localhost:5090`; `environment.ts` → `https://libris-api.billfolder.app`; `angular.json` file replacements as generated.
- [ ] **Step 6: Run** `npm test -- --watch=false && npm run build` — Expected: all PASS, build OK. Then `npm run tauri dev` and compare the shell with `telas-v5.html` (ribbon light, desk dark with overlay); screenshot for the checkpoint.
- [ ] **Step 7: Commit** — scaffold on `main` (`chore: projeto Angular + Tauri`), then `git switch -c feat/app-v1` and commit the rest (`feat(shell): fita lateral, mesa e tokens visuais`).

---

### Task 2: Core — API models, errors, dates, toasts  *(Checkpoint 1)*

**Files:**
- Create: `src/app/core/api/api-config.ts`, `src/app/core/api/models.ts`
- Create: `src/app/core/util/dates.ts`, `src/app/core/util/api-error.ts`
- Create: `src/app/core/toast/toast.service.ts`, `src/app/core/toast/toast-outlet.ts` (+ html/scss)
- Modify: `app.config.ts` (provide `provideHttpClient(withInterceptors([]))`, `provideRouter(routes, withComponentInputBinding())`), `app.html` (toast outlet)
- Test: `src/app/core/util/dates.spec.ts`, `src/app/core/util/api-error.spec.ts`, `src/app/core/toast/toast.service.spec.ts`

**Interfaces:**
- Produces:
  - `API_BASE_URL: InjectionToken<string>` (provided from `environment.apiBaseUrl`); `apiUrl(path: string): string` helper via `inject(API_BASE_URL)`; `coverSrc(coverUrl: string | null, base: string): string | null` (prefixes the API's relative `/v1/covers/{id}`).
  - `models.ts` — the API contract, verbatim:
    ```ts
    export type BookOwnership = 'owned' | 'wishlist';
    export type BookStatus = 'not_started' | 'reading' | 'read' | 'abandoned';
    export interface GenreRef { id: string; name: string }
    export interface Genre { id: string; name: string; bookCount: number }
    export interface Book {
      id: string; title: string; authors: string[]; isbn: string | null; totalPages: number | null;
      publisher: string | null; publishedYear: number | null; synopsis: string | null;
      coverId: string | null; coverUrl: string | null; genres: GenreRef[];
      ownership: BookOwnership; status: BookStatus | null;
      startedAt: string | null; finishedAt: string | null; addedAt: string;
      currentPage: number; percent: number;
    }
    export interface BookFields {
      title: string; authors: string[]; isbn: string | null; totalPages: number | null; publisher: string | null;
      publishedYear: number | null; synopsis: string | null; coverId: string | null; genres: string[];
      startedAt: string | null; finishedAt: string | null;
    }
    export interface CreateBookRequest extends BookFields { ownership: BookOwnership; status: BookStatus | null; today: string }
    export type UpdateBookRequest = BookFields;
    export interface BookListQuery { ownership?: BookOwnership; status?: BookStatus; genreId?: string; q?: string }
    export interface ReadingLog { id: string; date: string; page: number; fromPage: number; pagesRead: number }
    export interface ReadingLogResult { book: Book; logs: ReadingLog[] }
    export interface Cover { coverId: string; url: string }
    export interface IsbnLookup {
      isbn: string; title: string; authors: string[]; totalPages: number | null; publisher: string | null;
      publishedYear: number | null; synopsis: string | null; coverUrl: string | null; suggestedGenres: string[]; source: string;
    }
    export interface MonthStats { month: number; pagesRead: number; booksFinished: number }
    export interface YearStats { year: number; booksFinished: number; pagesRead: number; readingNow: number; months: MonthStats[] }
    export interface FinishedBook { bookId: string; title: string; authors: string[]; finishedAt: string }
    export interface ProgressedBook { bookId: string; title: string; pagesRead: number }
    export interface MonthDetail { year: number; month: number; pagesRead: number; finished: FinishedBook[]; progressed: ProgressedBook[] }
    export interface User { id: string; email: string; displayName: string }
    export interface AuthResponse { accessToken: string; refreshToken: string; accessTokenExpiresAt: string; refreshTokenExpiresAt: string; user: User }
    export interface ApiErrorBody { error: string; message: string }
    ```
  - `dates.ts`: `todayIso(now?: Date): string` (local calendar date, `YYYY-MM-DD`); `formatIsoDate(iso: string): string` (`dd/MM/yyyy`, string manipulation only); `formatShortDate(iso: string): string` (`dd/MM`); `monthName(month: number): string` (`janeiro`…, pt-BR); `monthShort(month: number): string` (`jan`…).
  - `api-error.ts`: `toUserMessage(error: unknown): string` and `apiErrorCode(error: unknown): string | null`.
  - `ToastService`: `success(message: string)`, `error(message: string)`, `toasts: Signal<Toast[]>` (`{ id: number; kind: 'success' | 'error'; message: string }`), auto-dismiss after 3500 ms (success) / 6000 ms (error).

- [ ] **Step 1: Write failing tests**
  - `dates.spec.ts`: `todayIso_usesLocalCalendarDate` (`new Date(2026, 9, 16, 23, 30)` → `"2026-10-16"`; `new Date(2026, 0, 5, 0, 10)` → `"2026-01-05"`); `formatIsoDate_doesNotShiftAcrossTimeZones` (`"2026-10-16"` → `"16/10/2026"`, `"2024-03-01"` → `"01/03/2024"`); `formatShortDate` (`"2026-10-02"` → `"02/10"`); `monthName(10)` → `"outubro"`; `monthShort(3)` → `"mar"`.
  - `api-error.spec.ts`: `toUserMessage_apiBody_returnsMessage` (`HttpErrorResponse` status 409 with `{ error: 'isbn_already_registered', message: 'Esse ISBN já está cadastrado: X.' }` → that message); `toUserMessage_status0_isNoConnection` → `"Sem conexão com o servidor."`; `toUserMessage_unknown_isGeneric` (500 without body → `"Algo deu errado. Tente de novo."`); `apiErrorCode` returns `'isbn_already_registered'` / `null`.
  - `toast.service.spec.ts` (fake timers): `success_addsThenDismisses` (present, gone after 3500 ms); `error_staysLonger` (present at 3500, gone at 6000).
- [ ] **Step 2: Run** `npm test -- --watch=false` — Expected: FAIL (modules missing).
- [ ] **Step 3: Implement** the files above. Toast outlet: fixed bottom-right stack, `.ficha` look, `role="status"`.
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): contrato da API, datas, erros e toasts`

---

### Task 3: Auth — login, tokens, refresh, guard, logout  *(Checkpoint 1)*

**Files:**
- Create: `src/app/core/auth/token-storage.ts`, `src/app/core/auth/auth.service.ts`, `src/app/core/auth/auth.interceptor.ts`, `src/app/core/auth/auth.guard.ts`
- Create: `src/app/features/login/login.page.ts` (+ html/scss)
- Modify: `app.routes.ts` (`login` public; everything else under a parent route with `canActivate: [authGuard]`), `app.config.ts` (register interceptor), `app.ts` (ribbon `Sair` → `auth.logout()`; shell hides ribbon on `/login`)
- Test: `auth.service.spec.ts`, `auth.interceptor.spec.ts`, `auth.guard.spec.ts`, `login.page.spec.ts`

**Interfaces:**
- Consumes: `models.ts`, `API_BASE_URL`, `toUserMessage` (Task 2).
- Produces:
  - `TokenStorage`: `read(): StoredSession | null`, `write(session: StoredSession)`, `clear()`; `StoredSession = { accessToken; refreshToken; user: User }`; key `libris.session` in `localStorage` (single-user desktop app; the webview's storage is per-app and survives restarts).
  - `AuthService` (root): `user: Signal<User | null>`, `isLoggedIn: Signal<boolean>`, `login(email, password): Observable<User>`, `refresh(): Observable<string>` (new access token; **single-flight**: concurrent callers share one request), `logout(): void` (best-effort `POST /v1/auth/logout`, clears storage, navigates to `/login`), `accessToken(): string | null`.
  - `authInterceptor: HttpInterceptorFn` — adds `Authorization: Bearer` to requests whose URL starts with `API_BASE_URL`, except `/v1/auth/*`; on 401 calls `refresh()` once and retries; if refresh fails → `logout()` and rethrow.
  - `authGuard: CanActivateFn` → `/login` when logged out.
  - Login page: ficha centered on the desk, title "Libris" in Aleo, fields E-mail / Senha (ruled-line style from the mock's form), button "Entrar"; shows `toUserMessage` for 401 ("E-mail ou senha incorretos." comes from the API); on success navigates to `/acervo`.

- [ ] **Step 1: Write failing tests** (HttpTestingController, router testing):
  - `auth.service.spec.ts`: `login_storesSessionAndUser`; `refresh_rotatesTokens` (POST `/v1/auth/refresh` with `{ refreshToken }`, storage updated); `logout_clearsAndNavigates` (logout request sent, storage empty, router at `/login` even if the logout request fails).
  - `auth.interceptor.spec.ts`: `addsBearerToApiCalls`; `skipsAuthEndpointsAndOtherHosts` (no header on `/v1/auth/login` nor on `https://covers.openlibrary.org/...`); `refreshesOnceForConcurrent401s` (two requests 401 → exactly one refresh request → both retried with the new token and succeed); `refreshFailure_logsOutAndRedirects`.
  - `auth.guard.spec.ts`: `redirectsToLoginWhenLoggedOut`, `allowsWhenLoggedIn`.
  - `login.page.spec.ts`: `submit_callsLoginAndNavigates`; `submit_showsApiMessageOn401`; `submit_disabledWhileInvalid` (empty or malformed e-mail).
- [ ] **Step 2: Run** `npm test -- --watch=false` — Expected: FAIL.
- [ ] **Step 3: Implement.** Single-flight refresh: keep the in-flight `Observable` (`shareReplay(1)`), reset it on completion/error. Logged-in state hydrates from `TokenStorage` at startup.
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS. Manual: start the local API (`~/Repos/Libris`, `docker compose -f docker-compose.dev.yml up -d && dotnet run --project src/Libris.Api --launch-profile http`), create a local account (`curl POST /v1/auth/signup`, signup is on in the dev profile), log in through `npm run tauri dev`, reload the window → still logged in; `Sair` → back to login.
- [ ] **Step 5: Commit** `feat(auth): login, renovação de token e saída` — **Checkpoint 1: stop for the user's review.**

---

### Task 4: Acervo  *(Checkpoint 2)*

**Files:**
- Create: `src/app/core/api/books.api.ts`, `src/app/core/api/genres.api.ts`
- Create: `src/app/shared/ui/{stamp,book-card,progress-bar,book-cover,page-heading}/…`
- Create: `src/app/features/acervo/acervo.page.ts` (+ html/scss)
- Test: `books.api.spec.ts`, `genres.api.spec.ts`, `book-card.spec.ts`, `acervo.page.spec.ts`

**Interfaces:**
- Consumes: models, `apiUrl`, `coverSrc`, `toUserMessage`, `ToastService`.
- Produces:
  - `BooksApi` (root): `list(query: BookListQuery): Observable<Book[]>` (omits empty params), `get(id)`, `create(req: CreateBookRequest)`, `update(id, req: UpdateBookRequest)`, `remove(id): Observable<void>`, `changeStatus(id, status: BookStatus, today: string)` (PATCH `/status`), `acquire(id, today: string, totalPages: number | null)`.
  - `GenresApi` (root): `list(): Observable<Genre[]>`, `create(name)`, `rename(id, name)`, `remove(id)`.
  - UI: `<lb-stamp [label] [active] [tone]>` (button; tones `venom | pumpkin | forest | ink`), `<lb-book-card [book]>` (cover, title, author(s) joined with ", ", "pág. X de Y · Z%", progress bar, genre tags; emits nothing — the page wraps it in a `routerLink`), `<lb-progress-bar [percent]>`, `<lb-book-cover [src] [title]>` (img or a colored placeholder with the title, like the mock), `<lb-page-heading [title] [subtitle]>`.
  - `STATUS_LABELS: Record<BookStatus, string>` = `not_started: 'Não iniciado'`, `reading: 'Lendo'`, `read: 'Lido'`, `abandoned: 'Abandonado'` (in `shared/ui/stamp/status-labels.ts`; Tasks 5 and 8 reuse it).
  - Route `acervo` → `AcervoPage`. Query params `status`, `genre`, `q` mirror the filters (so returning from a Ficha keeps them).
- Behavior (spec "Telas › Acervo"): heading "Acervo de leituras", subtitle "N livros no acervo · M em leitura" (counts from an unfiltered list); stamps Todos · Não iniciado · Lendo · Lido · Abandonado (single choice, default Todos); genre select (Gênero: todos + user's genres); search box (title/author), debounced 300 ms; filters combine; grid of cards ordered as the API returns; empty state "Nenhum livro por aqui ainda." with a link to Registrar (or "Nenhum livro com esses filtros." when filters are active).

- [ ] **Step 1: Write failing tests**
  - `books.api.spec.ts`: `list_sendsOnlySetParams` (`{ status: 'reading' }` → `GET /v1/books?status=reading`); `changeStatus_patchesWithToday`; `acquire_postsTodayAndPages`.
  - `genres.api.spec.ts`: `list_getsGenres`.
  - `book-card.spec.ts`: `showsProgressLine` ("pág. 140 de 512 · 27%"); `joinsAuthors`; `usesPlaceholderWithoutCover`.
  - `acervo.page.spec.ts`: `loadsOwnedBooksOnInit`; `stampFilter_requestsStatus`; `search_isDebounced` (fake timers: two keystrokes → one request); `subtitle_countsAllAndReading`; `acervo_showsErrorWhenListFails` (status 0 → "Sem conexão com o servidor." visible, no spinner); `emptyState_linksToRegistrar`.
- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implement**, porting card/stamp/filter styling from `telas-v5.html`.
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS. Manual with local API + a few books created via curl: filters, search with accents ("assombracao"), navigation to `/acervo/:id` (404 page placeholder until Task 5).
- [ ] **Step 5: Commit** `feat(acervo): lista com filtros por situação, gênero e busca`

---

### Task 5: Ficha do livro + registro de leitura  *(Checkpoint 2)*

**Files:**
- Create: `src/app/core/api/reading.api.ts`
- Create: `src/app/features/ficha/ficha.page.ts` (+ html/scss), `src/app/features/ficha/reading-log-panel.ts` (+ html/scss), `src/app/features/ficha/edit-dates-dialog.ts`
- Create: `src/app/shared/ui/confirm-dialog/confirm-dialog.ts` (native `<dialog>`)
- Modify: `app.routes.ts` (`acervo/:id` → `FichaPage`, input `id`)
- Test: `reading.api.spec.ts`, `ficha.page.spec.ts`, `reading-log-panel.spec.ts`

**Interfaces:**
- Consumes: `BooksApi`, `STATUS_LABELS`, `<lb-stamp>`, `<lb-book-cover>`, `<lb-progress-bar>`, dates, toasts.
- Produces:
  - `ReadingApi` (root): `list(bookId): Observable<ReadingLog[]>`, `create(bookId, { date, page }): Observable<ReadingLogResult>`, `update(bookId, logId, { date, page })`, `remove(bookId, logId)` (all three return `ReadingLogResult`).
  - `ConfirmDialog.open(message: string, confirmLabel: string): Promise<boolean>` (service + component pair under `shared/ui/confirm-dialog`).
  - `FichaPage` holds `book: WritableSignal<Book | null>`; the panel emits `(bookChange)` with the `Book` from each `ReadingLogResult`, so status/progress/dates update without a refetch.
- Behavior (spec "Telas › Ficha"): two sheets like `telas-v5.html`.
  - **Left**: big cover, "Ficha", title, "de {autores}", "{editora}, {ano} · {N} págs." (omit missing parts), genre tags, synopsis, progress line + bar, status stamps (click → `changeStatus` with `todayIso()`, toast "Situação atualizada."), "Início: dd/MM/yyyy · Término: —" with "✎ editar datas" (dialog with two date inputs, saves via `update` sending the full `UpdateBookRequest` built from the book), actions "Editar livro" (→ `/livros/:id/editar`) and "Apagar livro" (confirm → `remove` → toast "Livro apagado." → `/acervo`).
  - **Right** (owned books only): "Registrar leitura": "Parei na página" (number) + "Data" (`type=date`, default `todayIso()`), button "Anotar"; on success: toast "Leitura anotada.", page field cleared, date kept. "Histórico": rows `dd/MM · pág. {fromPage} → {page} · +{pagesRead}` (fromPage 0 shows "início"), each with edit (inline: same two fields) and delete (confirm).
  - Wishlist books: the right sheet is replaced in Task 8; here it simply isn't rendered when `ownership === 'wishlist'`.
  - 404 → "Livro não encontrado." with a link back to the Acervo.

- [ ] **Step 1: Write failing tests**
  - `reading.api.spec.ts`: `create_postsDateAndPage`; `remove_returnsResult`.
  - `ficha.page.spec.ts`: `rendersBookDetails` (title, authors, "512 págs.", dates formatted); `stampClick_changesStatusWithToday` (fake `todayIso` via `vi.setSystemTime(new Date(2026, 9, 16, 12))` → PATCH body `{ status: 'read', today: '2026-10-16' }`, stamps update from the response); `deleteBook_confirmsThenNavigates` (cancel → no request; confirm → DELETE, navigate `/acervo`); `wishlistBook_hidesLogPanel`; `notFound_showsMessage`.
  - `reading-log-panel.spec.ts`: `submit_postsAndEmitsBook` (emits the returned book; history re-rendered from `logs`); `history_showsFromToAndPages` ("02/10 · pág. início → 20 · +20" and "06/10 · pág. 20 → 51 · +31"); `logPanel_showsApiMessageOnPageOutOfRange` (400 `{ error: 'page_out_of_range', message: 'A página precisa estar entre 1 e 512.' }` → message shown under the field, typed value kept, no toast); `deleteLog_confirmsAndEmits`.
- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS. Manual against local API: log pages until the last page → stamp turns "Lido" with today's date.
- [ ] **Step 5: Commit** `feat(ficha): ficha do livro, situação e registro de leitura` — **Checkpoint 2: stop for the user's review.**

---

### Task 6: Registrar / Editar livro  *(Checkpoint 3)*

**Files:**
- Create: `src/app/features/book-form/book-form.page.ts` (+ html/scss), `src/app/features/book-form/genre-chips.ts`, `src/app/features/book-form/authors-field.ts`
- Create: `src/app/shared/ui/ruled-field/ruled-field.scss` (global-ish partial for the "datilografado" look) or a `lb-ruled-field` wrapper component — implementer's choice, must match the mock's thin ruled lines.
- Modify: `app.routes.ts` (`registrar` → create mode; `livros/:id/editar` → edit mode)
- Test: `book-form.page.spec.ts`, `genre-chips.spec.ts`

**Interfaces:**
- Consumes: `BooksApi`, `GenresApi`, models, dates, toasts.
- Produces:
  - `<lb-genre-chips [(names)] [suggestions]>`: chips for chosen names; text input with autocomplete from `GenresApi.list()` (case-insensitive prefix match), Enter/comma adds, × removes; `suggestions` render as dashed chips with ✓ (accept → moves into `names`) and × (dismiss). Duplicates ignored case-insensitively. Max 10.
  - `<lb-authors-field [(authors)]>`: one line per author, "+ autor", min 1, max 10.
  - `BookFormPage` exposes for Task 7: `form: FormGroup<BookFormControls>`, `applyIsbnLookup(lookup: IsbnLookup): void`, `coverId: WritableSignal<string | null>`, `pendingCoverUrl: WritableSignal<string | null>` (external cover from ISBN, uploaded via `from-url` on save).
- Behavior (spec "Telas › Registrar / Editar"):
  - Title "Registrar livro" / "Editar livro". Fields: ISBN (Task 7 adds "Buscar"), Título, Autor(es), Páginas totais, Editora, Ano, Sinopse (textarea), Gêneros, capa area (Task 7).
  - Create only: **Destino** Acervo (default) / Wishlist; when Acervo, **Situação inicial** stamps (default Não iniciado); **Datas opcionais**: Lendo/Abandonado show "Início"; Lido shows "Início" and "Término"; blank by default; hidden for Não iniciado and Wishlist.
  - Edit: no destination/status; shows Início/Término only when the book is owned and not `not_started`.
  - Client validation mirrors the API (title required ≤ 300; 1–10 authors; pages required for Acervo, 1–20000; year 1400–2100; término ≥ início; dates ≤ today). Submit disabled while invalid.
  - Save → `create`/`update` with `today: todayIso()` on create → toast "Livro cadastrado." / "Livro atualizado." → navigate to the Ficha (`/acervo/:id`) — or `/wishlist` for a wishlist book. API errors (`isbn_already_registered`, …) → message shown above the button, form kept.
  - Button label "Cadastrar obra" (create) / "Salvar" (edit).

- [ ] **Step 1: Write failing tests**
  - `genre-chips.spec.ts`: `enterAddsChip_ignoresDuplicateCase` ("Terror" then "terror" → one chip); `autocompleteFromExistingGenres`; `acceptSuggestion_movesIntoNames`; `dismissSuggestion_removesIt`.
  - `book-form.page.spec.ts`: `create_sendsTodayAndDefaults` (minimal valid form → POST body has `ownership: 'owned'`, `status: 'not_started'`, `startedAt: null`, `today: '2026-10-16'`, `genres: []`); `create_readWithPastDates_sendsThem` (Lido + 01/03/2024 & 20/03/2024); `datesHidden_forNotStartedAndWishlist`; `wishlist_doesNotRequirePages`; `finishedBeforeStarted_isInvalid`; `edit_loadsBookAndPutsFullRequest` (PUT body keeps genres as names and `coverId`); `apiConflict_showsMessageAndKeepsForm`; `afterCreate_navigatesToFicha`.
- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implement** with typed reactive forms.
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS. Manual: register an old book as Lido in March 2024 → Ficha shows the dates; edit it.
- [ ] **Step 5: Commit** `feat(registrar): cadastro e edição de livro`

---

### Task 7: ISBN lookup and covers  *(Checkpoint 3)*

**Files:**
- Create: `src/app/core/api/isbn.api.ts`, `src/app/core/api/covers.api.ts`
- Create: `src/app/features/book-form/cover-drop.ts` (+ html/scss)
- Modify: `book-form.page.ts/.html` (ISBN "Buscar", cover area, save flow)
- Test: `isbn.api.spec.ts`, `covers.api.spec.ts`, `cover-drop.spec.ts`, extend `book-form.page.spec.ts`

**Interfaces:**
- Consumes: `BookFormPage.applyIsbnLookup`, `coverId`, `pendingCoverUrl` (Task 6).
- Produces:
  - `IsbnApi.lookup(isbn: string): Observable<IsbnLookup>` (`GET /v1/isbn/{isbn}`, isbn URL-encoded as typed).
  - `CoversApi.upload(file: File): Observable<Cover>` (multipart field `file`); `CoversApi.fromUrl(url: string): Observable<Cover>` (`POST /v1/covers/from-url`).
  - `<lb-cover-drop [previewSrc] (fileSelected)>`: dashed area "Arraste a capa aqui ou clique para escolher" (accept `image/jpeg,image/png,image/webp`), handles `dragover`/`drop` and a hidden file input; shows the preview image when `previewSrc` is set; "Trocar capa" / "Remover capa".
- Behavior:
  - "Buscar" next to ISBN (enabled when ≥ 10 chars): spinner on the button; success → fills **only empty** fields (never overwrites what the user typed) with title, authors, pages, publisher, year, synopsis; `suggestedGenres` go to the chips' suggestions; `coverUrl` → preview + `pendingCoverUrl` (unless a cover was already chosen); toast-free inline note "Dados encontrados em {Open Library|Google Books}." Errors inline: `isbn_not_found` → "Não achei esse ISBN. Pode preencher à mão."; `isbn_lookup_unavailable` / status 0 → API/connection message; `invalid_isbn` → API message.
  - Dropped/chosen file → upload immediately (`CoversApi.upload`), preview from the response URL (`coverSrc`), sets `coverId`, clears `pendingCoverUrl`. Errors (`invalid_image`, `image_too_large`) inline under the area.
  - Save: if `pendingCoverUrl` and no `coverId` → `fromUrl` first, then create/update with that `coverId`; if `fromUrl` fails, save without cover and toast "Livro salvo, mas não consegui baixar a capa."
  - "Remover capa" → `coverId = null` (the API deletes the old file on update).

- [ ] **Step 1: Write failing tests**
  - `isbn.api.spec.ts`: `lookup_getsByIsbn`; `covers.api.spec.ts`: `upload_sendsMultipartFile`, `fromUrl_postsUrl`.
  - `cover-drop.spec.ts`: `coverDrop_uploadsDroppedFile` (dispatch a `drop` `DragEvent` whose `dataTransfer` has a PNG `File` → `fileSelected` emitted with it); `click_opensFileInput`; `rejectsNonImageDrop` (`.txt` → nothing emitted, message "Use uma imagem JPEG, PNG ou WebP.").
  - `book-form.page.spec.ts` (new): `isbnLookup_fillsOnlyEmptyFields` (title typed "Meu título" stays; authors/pages filled); `isbnLookup_suggestionsGoToChips`; `isbnNotFound_showsHint`; `save_withPendingCoverUrl_downloadsThenCreates` (order: `from-url` request, then POST `/v1/books` with that `coverId`); `save_fromUrlFails_savesWithoutCoverAndWarns`; `droppedCover_uploadsAndSetsCoverId`.
- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS. Manual in `npm run tauri dev`: ISBN `9780156001311` fills the form; drag a JPEG from Finder onto the drop area → preview appears (this proves `dragDropEnabled: false` works).
- [ ] **Step 5: Commit** `feat(registrar): busca por ISBN e capas` — **Checkpoint 3: stop for the user's review.**

---

### Task 8: Wishlist and genre management  *(Checkpoint 4)*

**Files:**
- Create: `src/app/features/wishlist/wishlist.page.ts` (+ html/scss), `src/app/features/ficha/acquire-panel.ts`
- Create: `src/app/features/genres/genres-dialog.ts` (+ html/scss)
- Modify: `ficha.page.html` (right sheet → `lb-acquire-panel` for wishlist books), `acervo.page.html` (link "Gerenciar gêneros" next to the genre select)
- Test: `wishlist.page.spec.ts`, `acquire-panel.spec.ts`, `genres-dialog.spec.ts`

**Interfaces:**
- Consumes: `BooksApi`, `GenresApi`, `<lb-book-card>`, `ConfirmDialog`, toasts.
- Produces: route `wishlist`; `<lb-acquire-panel [book] (bookChange)>`; `GenresDialog.open(): Promise<boolean>` (true when something changed, so the Acervo reloads genres).
- Behavior:
  - Wishlist (spec "Telas › Wishlist"): heading "Wishlist", subtitle "N livros pra procurar"; same cards without progress/status line; search + genre filter; empty state "Nenhum livro na wishlist."
  - Acquire panel on a wishlist Ficha: text "Ainda não está no acervo."; button **"Comprei"**; if the book has no `totalPages`, a pages field appears and is required before confirming; success → toast "Livro movido para o acervo." → the Ficha re-renders as owned (log panel appears).
  - Genres dialog (spec "Genre": rename and delete): list with book counts, inline rename (409 → API message), delete with confirm "Apagar o gênero {nome}? Os livros continuam no acervo."
- [ ] **Step 1: Write failing tests** — `wishlist.page.spec.ts`: `loadsWishlistOnly` (`ownership=wishlist`); `cardsHideProgress`. `acquire-panel.spec.ts`: `comprei_withPages_acquiresWithToday`; `comprei_withoutPages_requiresField` (button disabled until a page count is typed; body carries it); `emitsOwnedBook`. `genres-dialog.spec.ts`: `rename_conflictShowsMessage`; `delete_confirmsThenRemoves`.
- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS.
- [ ] **Step 5: Commit** `feat(wishlist): wishlist, "Comprei" e gerenciar gêneros`

---

### Task 9: Números  *(Checkpoint 4)*

**Files:**
- Create: `src/app/core/api/stats.api.ts`
- Create: `src/app/features/numeros/numeros.page.ts` (+ html/scss), `src/app/features/numeros/month-bars.ts`
- Test: `stats.api.spec.ts`, `month-bars.spec.ts`, `numeros.page.spec.ts`

**Interfaces:**
- Produces: `StatsApi.year(year): Observable<YearStats>`, `StatsApi.month(year, month): Observable<MonthDetail>`; `<lb-month-bars [months] [selected] (select)>` (12 CSS bars, height = pagesRead / max, value label on top, `jan…dez` below, selected bar in venom, others pumpkin; zero months render as an empty slot; keyboard focusable buttons with `aria-label="{mês}: {N} páginas"`); route `numeros`, query param `ano`.
- Behavior (spec "Telas › Números"): heading "Números de {ano}" with ‹ › year navigation; three KPI fichas (terminados no ano · páginas no ano, formatted `5.842` pt-BR · em leitura agora); bars card "Páginas lidas por mês"; detail sheets "Terminados em {mês} · N" (title, autor, dd/MM) and "Andaram em {mês} · N págs." (title, +N). Default selected month: current month for the current year, December for past years, January for future years.
- [ ] **Step 1: Write failing tests** — `stats.api.spec.ts`: `year_getsByYear`, `month_getsByYearAndMonth`. `month-bars.spec.ts`: `scalesToMax` (max month 100%, half-value 50%); `allZero_rendersWithoutDivisionByZero`; `click_emitsMonth`. `numeros.page.spec.ts`: `currentYear_selectsCurrentMonth` (system time 2026-10-16 → month 10 requested); `pastYear_selectsDecember`; `formatsThousands` ("5.842"); `prevYear_navigates` (`ano=2025`).
- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implement**, porting the bar/KPI look from `telas-v5.html`.
- [ ] **Step 4: Run** `npm test -- --watch=false` — Expected: PASS.
- [ ] **Step 5: Commit** `feat(numeros): números do ano e detalhe do mês`

---

### Task 10: Release build of the Mac app  *(Checkpoint 4)*

**Files:**
- Modify: `src-tauri/tauri.conf.json` (CSP, bundle), `src-tauri/icons/*` (generated), `README.md`
- Create: `app-icon.png` (1024×1024, a book on `--paper` with `--vampire-venom`, made with an inline SVG rendered to PNG — implementer's choice of tool already on the machine, e.g. `sips`/`qlmanage`; ask the user if they'd rather supply one)

**Interfaces:** none new.

- [ ] **Step 1: CSP** in `tauri.conf.json` → `app.security.csp`: `default-src 'self'; img-src 'self' data: https://libris-api.billfolder.app http://localhost:5090; connect-src 'self' https://libris-api.billfolder.app http://localhost:5090 ws://localhost:1420; style-src 'self' 'unsafe-inline'; font-src 'self'`. Covers from external hosts are never shown directly (they are proxied through `from-url` before saving), but the ISBN preview is — add `https://covers.openlibrary.org https://*.archive.org https://books.google.com https://books.googleusercontent.com` to `img-src`.
- [ ] **Step 2: Icons** — `npx tauri icon app-icon.png`.
- [ ] **Step 3: Build** — `npm run tauri build`. Expected: `src-tauri/target/release/bundle/macos/Libris.app` and a `.dmg`.
- [ ] **Step 4: Verify against production** — open `Libris.app`; log in with the real account; register a book by ISBN with cover, log a reading, check Números; drag a cover from Finder; quit and reopen → still logged in. Run `npm test -- --watch=false` once more.
- [ ] **Step 5: README** — how to run dev (local API + `npm run tauri dev`), how to build, where the `.dmg` lands, how to point at production.
- [ ] **Step 6: Commit** `build: ícone, CSP e build de release do app` — **Checkpoint 4: stop for the user's review**, then the final whole-branch review.
