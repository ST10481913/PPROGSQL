# RaceDay (Part 2 API + Part 3 MVC web app)

ASP.NET Core 8 Web API for the RaceDay event system (Run / Walk / Cycle events), using Entity Framework Core (Code-First) on SQL Server, JWT bearer authentication with role-based access, Swagger, xUnit tests and GitHub Actions CI.

**Student:** Tumelo Mahlogonolo Emerald Mohale (ST10481913)

## Build status
![CI](docs/ci-green-build.png)
<!-- Replace with a screenshot of your green GitHub Actions run, saved as docs/ci-green-build.png -->

## Demo video
YouTube (unlisted): <PASTE LINK HERE>

## Roles
| Role | Can do |
|---|---|
| **Organiser** | Create, update, delete and list their own events; add/edit/delete categories; view all enrolments for their events; capture and correct race results. |
| **Participant** | Browse events and categories; enrol in a category (bib number is generated); view/cancel own enrolments; view own results. |

Both roles can register, log in, and view/update their own profile. Anyone (even logged out) can view events, categories and the public leaderboard.

**How roles are managed:** the user picks a role at registration, but registering as an *Organiser* requires an invite code (`Registration:OrganiserInviteCode` in `appsettings.json`), so nobody can simply promote themselves. The role is stored in the database and embedded in the signed JWT; endpoints are protected with `[Authorize(Roles = "...")]`. Ownership is also checked, so an Organiser can only change *their own* events.

**Authentication / session approach:** on login the API issues a signed JWT access token (30 min) plus a refresh token (7 days, stored in `RefreshTokens`). The token carries the user's id and role, so the authenticated state and role are maintained on every request. This is the approach in the Part 1 endpoint plan. Passwords are hashed with BCrypt and never stored in plain text.

## Setup
Prerequisites: .NET 8 SDK (or newer) and SQL Server LocalDB (installed with Visual Studio) or any SQL Server.

1. Open `RaceDay.sln` in Visual Studio.
2. Set `RaceDay.Api` as the startup project and press **F5**.
3. The database `RaceDayDB` is created automatically from the EF Core model on first start.
4. Swagger opens at `http://localhost:5080/swagger`.

Change the connection string in `RaceDay.Api/appsettings.json` if you don't use LocalDB. Change `Jwt:Key` before deploying anywhere real.
To reset the database, delete `RaceDayDB` in SQL Server Object Explorer and run again.

To test protected endpoints: call `POST /api/auth/login`, copy the `accessToken`, click **Authorize** and paste it.
To register an Organiser in Swagger, send `"role": "Organiser"` and `"organiserCode": "RACEDAY-ORGANISER-2026"`.

## Run the tests
```bash
dotnet test
```
Tests use `WebApplicationFactory` with an in-memory EF database, so no SQL Server is required (this is also what runs in CI).

## Endpoints
Implemented exactly as in the Part 1 plan.

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `/login`, `/refresh`, `/logout` |
| Profile | `GET/PUT /api/users/me`, `PUT /api/users/me/password` |
| Events | `GET /api/events` (filter: `type`, `date`, `province`), `GET /api/events/{id}`, `GET /api/events/mine`, `POST`, `PUT /{id}`, `DELETE /{id}`, `GET /{id}/weather` |
| Categories | `GET/POST /api/events/{eventId}/categories`, `PUT/DELETE /api/categories/{id}` |
| Enrolments | `POST /api/categories/{id}/enrol`, `GET /api/enrolments/mine`, `DELETE /api/enrolments/{id}`, `GET /api/events/{eventId}/enrolments` |
| Results | `POST /api/enrolments/{id}/result`, `PUT /api/results/{id}`, `GET /api/events/{eventId}/results`, `GET /api/results/mine` |

## Project structure
```
RaceDay.Api/
  Controllers/   one controller per area, XML docs feed Swagger
  Models/        EF Core entities + enums (match the Part 1 ERD / SQL script)
  Data/          RaceDayDbContext (Fluent API config)
  Dtos/          request/response DTOs + mapping
  Services/      TokenService (JWT/refresh), WeatherService (Open-Meteo)
RaceDay.Tests/   xUnit integration tests (auth, events, categories, enrolments, results, role rules)
.github/workflows/ci.yml   build + test on every push
```

---

# Part 3: RaceDay MVC web app

`RaceDay.Web` is an ASP.NET Core 8 MVC front end that consumes the Part 2 API for **every** data operation. It has no database access and no duplicated business logic. Azure Blob Storage was added **to the API** (the MVC app never talks to Azure; it uploads files to the API).

## Demo video
YouTube (unlisted): <PASTE LINK HERE>

## How it fits together
```
Browser  ->  RaceDay.Web (MVC, session)  ->  RaceDay.Api (JWT, EF Core)  ->  SQL Server
                                                       \->  Azure Blob Storage (banners, profile pictures)
```
- **Session-based authentication:** on login the MVC app calls `POST /api/auth/login` and stores the JWT, refresh token, role, name and avatar in the server-side **ASP.NET Core session**. The state and role are available on every page. If the API answers 401 the app silently uses the refresh token once; if that fails the session is cleared and the user is sent to the login page.
- **Protected pages:** `[SessionAuthorize]` (a filter in `Services/`) sends unauthenticated users to the login page and users with the wrong role to an *Access denied* page. The API still enforces roles and ownership itself.
- **Logout** revokes the refresh token via `POST /api/auth/logout`, clears the session and redirects to the login page.
- **Errors:** every API failure is turned into a friendly message (`Services/ApiClient.cs`); the user never sees raw errors or stack traces. Forms are validated server-side (and client-side with jQuery validation).

## Project structure
```
RaceDay.Web/
  Controllers/   Home (public), Account (login/register/profile), Participant, Organiser
  Models/        ApiModels (API contracts), ViewModels (forms + page models)
  Services/      ApiClient (typed HttpClient + token refresh), session helpers, SessionAuthorize, Format helpers
  Views/         Razor views, Bootstrap 5 + RaceDay theme (wwwroot/css/site.css)
  Dockerfile     multi-stage, non-root image
docker-compose.yml   SQL Server + Azurite + API + MVC in one command
```

## What each role sees
| Role | Pages |
|---|---|
| **Organiser** | Dashboard (events, enrolments per event, upcoming dates), My Events (create / edit / delete + banner upload), Categories (add / edit / delete), Enrolments (participants, category, status), Results entry (finish time, positions, DNF/DQ), Profile |
| **Participant** | Home (upcoming events + filters), Event details (info, categories, weather, leaderboard), Enrol, My Enrolments (cancel), My Results (e.g. "47th out of 312 finishers"), Profile (edit details + profile picture) |

Enrolment/race statuses are colour coded: **green** Confirmed/Finished, **amber** Pending/DNF, **red** Cancelled/DQ.

## Run it

### Option A: everything in Docker (one command)
```bash
docker compose up --build
```
Then open **http://localhost:5090** (MVC app) or http://localhost:5080/swagger (API).
Register a Participant, or an Organiser with the invite code `RACEDAY-ORGANISER-2026`.
(The SQL Server image is x86-64; on Apple Silicon enable Rosetta/emulation in Docker Desktop.)

### Option B: MVC app in Docker, API running on your machine
1. Start the API so it is reachable from Docker. In Visual Studio pick the **http-all-interfaces** launch profile, or:
   ```bash
   cd RaceDay.Api && dotnet run --launch-profile http-all-interfaces
   ```
2. Start Azurite for local Blob Storage (or put a real Azure connection string in `BlobStorage:ConnectionString`):
   ```bash
   docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
   ```
3. Build and run the MVC container (**single run command**):
   ```bash
   docker build -t raceday-web ./RaceDay.Web
   docker run --rm -p 5090:8080 --add-host=host.docker.internal:host-gateway -e Api__BaseUrl=http://host.docker.internal:5080/ raceday-web
   ```
   Open **http://localhost:5090**. The containerised app behaves identically to running locally; the only difference is `Api__BaseUrl`.

### Option C: without Docker
Run `RaceDay.Api` (port 5080) and `RaceDay.Web` (port 5090) from Visual Studio (multiple startup projects) or `dotnet run` in each folder. `RaceDay.Web/appsettings.json` -> `Api:BaseUrl` points at the API.

## Azure Blob Storage (in the API)
- `POST /api/events/{id}/banner` (Organiser, owner only) and `POST /api/users/me/picture` (any user) accept a multipart `file` (JPG/PNG/WebP/GIF, max 5 MB), store it in the `raceday-images` container and save the public URL in `Event.BannerUrl` / `User.ProfilePictureUrl`.
- Configure in `RaceDay.Api/appsettings.json` -> `BlobStorage`: `ConnectionString` (default `UseDevelopmentStorage=true` for Azurite), `ContainerName`, and optional `PublicBaseUrl` (used when the browser reaches storage on a different address than the API, as in Docker Compose).
- For real Azure use `dotnet user-secrets` (don't commit the key): `dotnet user-secrets set "BlobStorage:ConnectionString" "<your connection string>"`.
- The banner is shown on the event cards, the event detail page and the organiser's events list; the profile picture in the navigation bar and profile page.

## Database note (Part 3 schema change)
Part 3 adds two columns (`Events.BannerUrl`, `Users.ProfilePictureUrl`). The API uses `EnsureCreated`, which does not alter existing tables, so **delete the `RaceDayDB` database once** (see Setup above) and start the API again to recreate it.

## Part 3 API changes
| Change | Why |
|---|---|
| `POST /api/events/{id}/banner`, `POST /api/users/me/picture` | Blob Storage uploads |
| `BannerUrl` on `EventDto`, `ProfilePictureUrl` on `UserDto` | Show images in the MVC app |
| `PUT /api/events/{id}` accepts an optional `venue` | Organisers can edit an event's location |
| New tests in `UploadAndVenueTests.cs` | Upload validation, ownership, venue update |

## Part 3 requirement checklist
- [x] MVC app consumes the Part 2 API, no database access or duplicated logic in the MVC project
- [x] Register / log in via the API; session keeps state and role; role-specific navigation; protected pages; logout
- [x] Organiser: dashboard, events CRUD (name, description, date, location, distance, type), banner upload, categories, enrolments, results entry
- [x] Participant: home with filters, event detail + categories, enrol with category, my enrolments with status, My Results (name, date, category, time, position of N finishers), profile + picture
- [x] Azure Blob Storage inside the API; MVC never contacts Azure directly
- [x] Dockerfile + README run command; containerised app behaves like the local one
- [x] Bootstrap, RaceDay theme, colour-coded statuses, friendly validation and error handling, consistent layout
- [x] GitHub Actions builds the MVC project (and the Docker image)
- [ ] Push to a clearly named branch with at least 20 meaningful commits (**you**)
- [ ] Record the unlisted YouTube video and paste the link above (**you**)
