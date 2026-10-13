# StudyConnect (.NET 9 edition)

StudyConnect helps university students find study partners and **assignment team mates from the same subject and the same lab**.

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

## Screenshots

Real screenshots of the running application are in [`docs/screenshots`](docs/screenshots) (dashboard, Team Finder, team workspace with live chat, messages with context menu, insights with K-Means communities, profile setup, mobile and dark mode). They are handy for the project report.

## Tests

```
dotnet test
```

The tests cover the password policy and hasher, extension methods, the combinatorics iterator, the compatibility engine and team builder, K-Means / TF-IDF, and integration flows (register → login → reset password, connect → message, team invitations with lab/size rules, leader hand-over, Team Finder lab filtering, JSON seeding from disk).


## Configuration

`src/StudyConnect.Web/appsettings.json`

* `ConnectionStrings:StudyConnect` – SQLite file (relative paths are resolved from the web project folder).
* `Database:SeedDemoData` / `Database:ResetOnStartup` – demo data control.
* `Email` – leave `SmtpHost` empty to write e-mails to `App_Data/outbox`; fill it in to send real e-mails.
