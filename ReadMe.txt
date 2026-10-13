STUDYCONNECT (.NET 9) - HOW TO RUN
===================================

Requirements
------------
* Visual Studio 2022 (version 17.12 or newer) with the "ASP.NET and web development" workload
* .NET 9 SDK (installed with Visual Studio 17.12+)
* Internet access the FIRST time you build (Visual Studio downloads the NuGet packages
  Microsoft.EntityFrameworkCore.Sqlite, NUnit, NUnit3TestAdapter and Microsoft.NET.Test.Sdk)
* No database server is needed - the app uses a local SQLite file that is created automatically.

Run in Visual Studio
--------------------
1. Unzip the folder and double-click StudyConnect.sln.
2. Make sure "StudyConnect.Web" is the start-up project (right-click > Set as Startup Project).
3. Press F5 (or Ctrl+F5). The browser opens at http://localhost:5080
   (choose the "StudyConnect (https)" profile in the toolbar if you prefer HTTPS).
4. On first start the database App_Data/studyconnect.db is created and filled with demo data.

Run from the command line
-------------------------
    dotnet run --project src/StudyConnect.Web
    then open http://localhost:5080

Demo accounts (all use the password  Demo@1234 )
-------------------------------------------------
    alice@uts.edu.au    - enrolled in 31927 Lab 02, has messages, invitations and requests waiting
    bob@uts.edu.au      - Alice's connection, same lab
    diana@uts.edu.au    - UI/UX designer in Lab 02
    george@uts.edu.au   - leader of the open team "LINQ Legends" (Lab 02)
    noah@uts.edu.au     - member of the full team "Null Pointers" (Lab 01)
    (30 demo students in total - see src/StudyConnect.Web/Data/Seed/users.json)

Suggested demo path
-------------------
1. Log in as alice@uts.edu.au -> Dashboard (similar students, sessions, profile completeness).
2. Team Finder -> Suggested teams for 31927 Lab 02, change filters / minimum-match slider,
   open "Why this score?" on a student card, click "Create team & invite".
3. Open a second browser (or a private window), log in as bob@uts.edu.au and accept the
   invitation on "My Teams" - Alice's screen updates in real time. Try the team chat.
4. Messages -> chat with Bob in real time (right-click a conversation for the context menu).
5. Insights -> charts and K-Means "study communities".
6. Forgot password -> in development the reset link is shown on screen and the e-mail is
   saved as an HTML file in src/StudyConnect.Web/App_Data/outbox.

Run the unit tests
------------------
Visual Studio: Test > Run All Tests (Test Explorer).
Command line:  dotnet test

Reset the demo data
-------------------
Stop the app and delete the folder src/StudyConnect.Web/App_Data
(or set "Database:ResetOnStartup" to true in appsettings.json for one run).

Optional: real e-mails
----------------------
Fill in the "Email" section of src/StudyConnect.Web/appsettings.json (e.g. Gmail SMTP:
SmtpHost smtp.gmail.com, port 587, your address and an app password).

Troubleshooting
---------------
* "The current .NET SDK does not support targeting .NET 9.0" -> update Visual Studio 2022 to 17.12+.
* NuGet restore errors -> make sure you are online, then Build > Rebuild Solution.
* Port already in use -> change applicationUrl in src/StudyConnect.Web/Properties/launchSettings.json.
* After pulling a newer version, if pages fail with database errors, delete App_Data (see above)
  so the database is recreated with the latest schema.
