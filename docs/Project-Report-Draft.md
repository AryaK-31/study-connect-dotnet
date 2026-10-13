# StudyConnect – Project Report (draft)

> **Draft to adapt.** Replace the bracketed placeholders, add your group's real contributions and export to PDF.
> The assignment asks for 1,500–2,000 words; this draft is about 1,300 words, leaving room for your own reflections and screenshots.

**Subject:** 31927 / 32998 Applications Development with .NET – Assignment 2
**Team:** [Name, student ID] · [Name, student ID] · [Name, student ID] - Lab [xx]

## 1. Project idea

StudyConnect is a web application that connects students who want to study together and, above all, helps them form **assignment teams within the same subject and the same lab**. Students describe their interests, weekly free time, preferred team role, confidence level and target grade. A matching engine then ranks every available classmate in the same lab and proposes complete teams that balance shared interests, overlapping availability and complementary skills. Once a team is formed, members get a shared workspace with a team chat, a free-time heat-map and a downloadable roster.

## 2. Motivation

Group assignments in subjects such as 31927 require two or three students who attend the same lab. In practice, students find partners by messaging classmates one by one, by asking in crowded lab sessions, or by being placed with whoever is left. Students who are new, shy, part-time or studying online are disadvantaged, and teams are often formed on convenience rather than on compatible goals, schedules and skills, a common source of conflict later in the semester.

StudyConnect centralises this process. Instead of searching individually, a student opens the Team Finder, sees everyone in their lab who is still looking for a team, understands *why* each person is a good match, and can create a team and send invitations in one click. The rule that teams must come from a single lab is enforced by the system rather than checked manually.

## 3. Key features

**Accounts and profiles.** Registration with strong-password rules and a live strength meter, cookie-based login, password reset by e-mail (in development the e-mail is saved as an HTML file and the link is shown on screen), profile editing with interest tags, a 7 × 3 weekly availability grid and account deletion.

**Subjects and labs.** Students enrol in a subject, select the lab they attend, and record their preferred role, skill level, target grade and a short pitch.

**Team Finder.** For the selected subject the Team Finder shows (1) suggested complete teams, (2) all available students ranked by compatibility and (3) open teams in the same lab. Filters include "same lab only", "only students without a team", role, free-text search and a minimum-match slider. Each student card explains its score with plain-language reasons and an expandable per-criterion breakdown.

**Teams.** Students can create teams, invite classmates, request to join open teams, accept or decline invitations, remove members and leave (leadership passes automatically to the longest-standing member). Each team has a real-time group chat, a health panel showing average compatibility, roles covered and missing, a heat-map of common free time, and a CSV roster export.

**Connections and messaging** (from the original app). Students send connection requests; once accepted they can chat in real time with unread counters, read receipts, day separators and a right-click context menu.

**Study sessions** (from the original app). Students create sessions with time, duration, format, location, capacity and optional subject; others join or leave, and organisers are notified.

**Notifications and insights.** A notification bell and live toasts announce requests, invitations and team changes. The Insights screen shows interactive charts of popular interests, lab demand, preferred roles and free time, plus *study communities* discovered with machine learning.

## 4. Technical design

The solution follows a three-layer architecture:

* **StudyConnect.Core** – entities, enums, service interfaces, the matching engine and the machine-learning algorithms. It has no external dependencies, which makes the algorithms easy to unit test.
* **StudyConnect.Infrastructure** – Entity Framework Core 9 with SQLite, service implementations, e-mail senders and the real-time hub.
* **StudyConnect.Web** – an ASP.NET Core Blazor Web App. Account pages use static server rendering so they can write the authentication cookie; all other pages are interactive server components connected through SignalR.

**Matching engine.** Compatibility is a weighted average of six criteria implemented as subclasses of the abstract `MatchCriterion` class (same lab, shared interests via Jaccard similarity, availability overlap, complementary roles, goal alignment and skill balance). Because the engine depends only on the `IMatchCriterion` interface, new criteria can be added without changing it.

**Team builder.** The builder shortlists the twelve most compatible candidates (using a bounded min-heap), enumerates every combination of the required size with a custom `yield return` iterator, and scores each team as 70 % average pairwise compatibility, 15 % role coverage and 15 % common free time. Pair scores are memoised in a dictionary, so a team of three requires at most C(12, 2) = 66 evaluations.

**Machine learning.** Each student's interests are converted into TF-IDF vectors (rare interests weigh more), and a from-scratch K-Means implementation with k-means++ initialisation groups them into communities; each community is labelled with the highest-weighted terms of its centroid. The number of clusters follows the rule of thumb k ≈ √(n/2).

**Real-time updates.** Services publish events to an in-memory publish/subscribe hub. Each open page subscribes with a delegate and re-renders over its SignalR circuit, which replaces Socket.io from the original version.

**Security.** Passwords are hashed with PBKDF2-SHA256 and a random salt; reset tokens are random, stored only as SHA-256 hashes and expire after 30 minutes; the authentication cookie is HTTP-only and SameSite=Lax, and is revalidated so deleted accounts are signed out; forms are protected by antiforgery tokens, and return URLs are checked to prevent open redirects.

**.NET concepts demonstrated.** Interfaces and dependency injection; polymorphism through inheritance, overriding and overloading; generics (`Result<T>`, `TopBy<T>`, `Combinations<T>`); LINQ with lambda expressions and an explicit anonymous method; extension methods; custom enumerators and an indexer; delegates and events; file I/O (JSON seed files, e-mail outbox, CSV export); Entity Framework Core with LINQ; and NUnit tests (64 test cases, including integration tests on an in-memory SQLite database).

## 5. Usage instructions

1. Open `StudyConnect.sln` in Visual Studio 2022 (17.12 or newer) and run **StudyConnect.Web**.
2. Log in as `alice@uts.edu.au` with password `Demo@1234`.
3. Open **Team Finder**, review the suggested teams for 31927 Lab 02 and click **Create team & invite**.
4. In a private browser window, log in as `bob@uts.edu.au`, open **My Teams** and accept the invitation; both windows update instantly. Try the team chat.
5. Explore **Messages**, **Sessions**, **Connections** and **Insights**.

## 6. Contribution of team members

| Member | Contribution |
|---|---|
| [Name] | [e.g. Team Finder, matching engine and team builder, NUnit tests] |
| [Name] | [e.g. Blazor UI, real-time messaging, notifications] |
| [Name] | [e.g. EF Core data model, authentication, insights and K-Means, report] |

## 7. Future work

* Import enrolments and lab lists directly from the university timetable.
* Learn criterion weights from past teams' outcomes (supervised learning).
* Calendar integration for study sessions, and peer feedback after the assignment.

## References

* Microsoft. *ASP.NET Core Blazor documentation.* https://learn.microsoft.com/aspnet/core/blazor
* Microsoft. *Entity Framework Core documentation.* https://learn.microsoft.com/ef/core
* Arthur, D. & Vassilvitskii, S. (2007). *k-means++: The Advantages of Careful Seeding.* Proceedings of the 18th ACM-SIAM Symposium on Discrete Algorithms, 1027–1035.
* Salton, G. & Buckley, C. (1988). *Term-weighting approaches in automatic text retrieval.* Information Processing & Management, 24(5), 513–523.
* NUnit. *NUnit documentation.* https://docs.nunit.org
