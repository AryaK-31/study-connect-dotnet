# StudyConnect

StudyConnect is a .NET 9 web app for students looking for study partners and assignment teammates. It matches students enrolled in the same subject and lab using their interests, availability, goals, and preferred roles.

---

## Quick start

1. Open `StudyConnect.sln` in Visual Studio 2022 (17.12 or later).
2. Set `StudyConnect.Web` as the startup project and run it.
3. Sign in with `alice@uts.edu.au` and password `Demo@1234`. The app seeds 30 demo students on first run.

The app creates a local SQLite database in `src/StudyConnect.Web/App_Data/`. See `ReadMe.txt` for command-line instructions and demo accounts.

---

## Solution structure

```
StudyConnect.sln
├── src/
│   ├── StudyConnect.Core            Domain layer (no external packages)
│   │   ├── Entities/                EF Core entity classes (User, Subject, LabSlot, Team, ...)
│   │   ├── Enums/                   TimeBlock, TeamRole, SkillLevel, TargetGrade, ...
│   │   ├── Abstractions/            Service interfaces (IAuthService, ITeamService, IRealtimeNotifier, ...)
│   │   ├── Matching/                Compatibility engine, match criteria, team builder
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

## Tests

```
dotnet test
```

Tests cover password handling, matching and team building, K-Means and TF-IDF, and service flows for accounts, connections, messaging, teams, and data seeding.


## Configuration

`src/StudyConnect.Web/appsettings.json`

* `ConnectionStrings:StudyConnect` – SQLite file (relative paths are resolved from the web project folder).
* `Database:SeedDemoData` / `Database:ResetOnStartup` – demo data control.
* `Email` – leave `SmtpHost` empty to write e-mails to `App_Data/outbox`; fill it in to send real e-mails.
