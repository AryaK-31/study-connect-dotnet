# StudyConnect (.NET 9 edition)

StudyConnect helps university students find study partners and **assignment team mates from the same subject and the same lab**.
This solution is a complete C# / .NET 9 rewrite of the original Node.js (Express + Apollo GraphQL + Socket.io + MongoDB) and React application, extended with a centralised **Team Finder**.

> **The problem:** in a subject like *31927 Applications Development with .NET*, the group assignment needs 2–3 students who must all attend the same lab. Today each student messages classmates one by one to find a group.
> **The solution:** students enrol with their lab, role, goal and free time; StudyConnect ranks every available student in that lab with an explainable compatibility score and proposes complete teams in one click.

---

## Quick start

1. Open `StudyConnect.sln` in **Visual Studio 2022 (17.12+)**.
2. Set **StudyConnect.Web** as the start-up project and press **F5**.
3. Log in with **`alice@uts.edu.au` / `Demo@1234`** (30 demo students are seeded automatically).

No database server is needed; a SQLite file is created in `src/StudyConnect.Web/App_Data/` on first run. See `ReadMe.txt` for detailed steps and a demo script.

---

## Solution structure

```
StudyConnect.sln
├── src/
│   ├── StudyConnect.Core            Domain layer (no external packages)
│   │   ├── Entities/                EF Core entity classes (User, Subject, LabSlot, Team, ...)
│   │   ├── Enums/                   TimeBlock, TeamRole, SkillLevel, TargetGrade, ...
│   │   ├── Abstractions/            Service interfaces (IAuthService, ITeamService, IRealtimeNotifier, ...)
│   │   ├── Matching/                Compatibility engine, match criteria (polymorphism), team builder
│   │   ├── MachineLearning/         K-Means clustering, TF-IDF vectoriser, study-community analyser
│   │   ├── Common/                  Result<T>, Combinatorics iterator, WeeklyAvailability enumerator
│   │   ├── Extensions/              String / enum / date / sequence extension methods
│   │   ├── Security/                PBKDF2 password hasher, reset tokens
│   │   └── Validation/              Password policy, e-mail / phone validation
│   ├── StudyConnect.Infrastructure  Data & services
│   │   ├── Data/                    StudyConnectDbContext (EF Core + SQLite), JSON seeder
│   │   ├── Services/                Auth, Profile, Connection, Message, Session, Subject, Matchmaking, Team, Notification, Insights
│   │   ├── Email/                   SMTP sender and development "outbox" sender (template method)
│   │   └── Realtime/                In-memory publish/subscribe hub (delegates)
│   └── StudyConnect.Web             ASP.NET Core Blazor Web App (interactive server)
│       ├── Components/Account/      Login, Sign-up, Forgot / Reset password (static SSR + cookie auth)
│       ├── Components/Pages/        Dashboard, Team Finder, Teams, Team workspace, Sessions, Messages, Connections, Subjects, Profile, Insights
│       ├── Components/Shared/       Reusable UI: StudentCard, ScoreRing, BarChart, AvailabilityGrid, Modal, ConfirmDialog, Toasts ...
│       ├── Data/Seed/               Demo data as JSON files (read at start-up)
│       └── wwwroot/                 CSS (light + dark theme), JS interop helpers, SVG images
└── tests/
    └── StudyConnect.Tests           NUnit tests (unit + SQLite in-memory integration tests)
```

The layers depend inwards only (Web → Infrastructure → Core). The UI talks to **interfaces** and receives implementations through dependency injection, which keeps coupling low.

---

## Original app → .NET mapping

| Original (Node.js / React) | .NET 9 version |
|---|---|
| React + Vite SPA | Blazor Web App with interactive server components |
| Express + Apollo GraphQL resolvers | C# service classes behind interfaces (`Infrastructure/Services`) |
| MongoDB + Mongoose models | Entity Framework Core 9 + SQLite (`StudyConnectDbContext`) |
| JWT in `localStorage` | Secure HTTP-only cookie authentication, revalidated every 5 minutes |
| bcrypt | PBKDF2-SHA256 (`Pbkdf2PasswordHasher`) |
| Socket.io real-time chat | Blazor's SignalR circuit + `InMemoryRealtimeNotifier` pub/sub (delegates) |
| Nodemailer (Gmail) | `IEmailSender` → `SmtpEmailSender` or `FileOutboxEmailSender` |
| `database/*.json` + `seed.js` | `Data/Seed/*.json` + `DatabaseSeeder` |
| Theme context + `useMemo` | CSS variables + `localStorage` via JS interop |
| Error boundary / error toast | Blazor `ErrorBoundary` + `ToastService` + `AppComponentBase.RunAsync` |

Every original feature is kept: sign-up/login with password rules, forgot/reset password, profile with interests and availability, similar-student matching, connection requests (accept/decline), real-time direct messages with unread counters and read receipts, delete conversation, study sessions (create/join/leave/delete, organiser notified), delete account, dark/light theme.

## New features

* **Subjects & labs** – students enrol in a subject and choose their lab, preferred role, skill level, target grade and a short pitch.
* **Team Finder** – lab-aware, ranked list of available students with an explainable compatibility score (same lab, shared interests, overlapping free time, complementary roles, aligned goals, skill balance) and filters (role, search, minimum-match slider).
* **One-click team suggestions** – the `TeamBuilder` enumerates combinations of the best candidates and proposes complete teams scored on pairwise compatibility, role coverage and common free time.
* **Teams** – create teams, invite students, request to join open teams, accept/decline, leader hand-over, member removal, team chat, team-health panel with a free-time heat-map, CSV roster export.
* **Notifications** – bell menu + real-time toasts for connection requests, invitations, session joins and team updates.
* **Insights** – interactive charts (popular interests, lab demand, roles, free time per day) and **K-Means study communities**.

---

## Assignment requirements → where to find them

| Requirement | Where |
|---|---|
| GUI with ≥ 4 distinct, responsive screens | Dashboard, Team Finder, My Teams, Team workspace, Sessions, Messages, Connections, Subjects, Profile, Insights, Login/Sign-up/Reset (`Components/Pages`, `Components/Account`) |
| ≥ 6 UI element categories | buttons, headings, dropdowns (`select`), lists, **modals** (`Modal`, `ConfirmDialog`), **interactive bar graphs** (`BarChart`), **sliders** (min-match, capacity), **context menu** (right-click a conversation), tabs, toggles/switches, radio groups, check-boxes, chips, progress bars, score rings, avatars, images (SVG), date-time picker, datalist auto-complete, collapsible `details`, toasts |
| Communication between interfaces | query strings (`team-finder?subject=3`), route parameters (`/teams/{id}`, `/messages/{id}`), shared scoped services (`ToastService`), real-time events between users' screens |
| Polymorphism | `MatchCriterion` abstract class with six overriding criteria; `EmailSenderBase` → `SmtpEmailSender` / `FileOutboxEmailSender`; `RevalidatingUserAuthenticationStateProvider` and `UserValidationCookieEvents` override framework methods; constructor overloading (`CompatibilityEngine`, `PasswordPolicy`, `Pbkdf2PasswordHasher`); method overloading (`AppComponentBase.RunAsync` / `RunAsync<T>`, `IRealtimeNotifier.PublishAsync`) |
| Interfaces (≥ 2) | `IAuthService`, `ITeamService`, `IMatchmakingService`, `IMatchCriterion`, `ICompatibilityEngine`, `ITeamBuilder`, `IEmailSender`, `IRealtimeNotifier`, `IPasswordHasher`, `IPasswordPolicy`, ... |
| NUnit tests | `tests/StudyConnect.Tests` (64 test cases, unit + integration with in-memory SQLite) |
| Anonymous method + LINQ + lambda | everywhere in the services; an explicit `delegate (Enrollment e) { ... }` anonymous method is used as a LINQ predicate in `InsightsService` |
| Generics / generic collections | `Result<T>`, `Combinatorics.Combinations<T>`, `SequenceExtensions.TopBy<T>` (with `PriorityQueue<T, double>`), `JaccardSimilarity<T>`, `Dictionary`, `HashSet`, `List` ... |
| Enumerators | custom `IEnumerable`/`IEnumerator` in `WeeklyAvailability` and the `yield return` iterator in `Combinatorics`; plus 10 enums in `Core/Enums` |
| Properties / indexers | computed properties on entities (`Team.IsFull`, `StudySession.SpotsLeft`), `init` properties on `StudentProfile`, indexer `WeeklyAvailability[day, block]` |
| Extension methods | `Core/Extensions/*` (`NormalizeTag`, `ToTitleCase`, `ToInitials`, `ToRelativeTime`, `ToDisplayName`, ...), `DependencyInjection.AddStudyConnectInfrastructure` |
| Delegates / events | `RealtimeEventHandler` delegate, `ToastService.Changed` event, `Func<>` rules in `PasswordPolicy` |
| File reading/writing | JSON seed files read by `DatabaseSeeder`; development e-mails written as HTML files by `FileOutboxEmailSender`; CSV roster export |
| Database + Entity Framework + LINQ | EF Core 9 with SQLite, fluent configuration, LINQ queries, `ExecuteUpdateAsync`, transactions |
| Error handling & validation | DataAnnotations on every form, server-side validation in every service (`OperationResult`), `ErrorBoundary`, toasts, safe redirects, antiforgery |
| Algorithms / data structures | weighted multi-criteria scoring, combinatorial team search with memoisation, bounded min-heap top-k, Jaccard similarity, TF-IDF, cosine similarity |
| **Bonus:** ASP.NET / Blazor | whole UI is Blazor on ASP.NET Core |
| **Bonus:** EF + external DB with LINQ | EF Core + SQLite |
| **Bonus:** machine-learning algorithm | K-Means (k-means++) implemented from scratch on TF-IDF features (`Core/MachineLearning`) – no LLM API |

---

## Screenshots

Real screenshots of the running application are in [`docs/screenshots`](docs/screenshots) (dashboard, Team Finder, team workspace with live chat, messages with context menu, insights with K-Means communities, profile setup, mobile and dark mode). They are handy for the project report.

## Tests

```
dotnet test
```

The tests cover the password policy and hasher, extension methods, the combinatorics iterator, the compatibility engine and team builder, K-Means / TF-IDF, and integration flows (register → login → reset password, connect → message, team invitations with lab/size rules, leader hand-over, Team Finder lab filtering, JSON seeding from disk).

A GitHub Actions workflow (`.github/workflows/build-and-test.yml`) builds and tests the solution automatically when the folder is pushed to GitHub.

## Configuration

`src/StudyConnect.Web/appsettings.json`

* `ConnectionStrings:StudyConnect` – SQLite file (relative paths are resolved from the web project folder).
* `Database:SeedDemoData` / `Database:ResetOnStartup` – demo data control.
* `Email` – leave `SmtpHost` empty to write e-mails to `App_Data/outbox`; fill it in to send real e-mails.
