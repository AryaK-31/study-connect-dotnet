using Microsoft.EntityFrameworkCore;
using StudyConnect.Core.Common;
using StudyConnect.Core.Entities;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Models;
using StudyConnect.Infrastructure.Data;

namespace StudyConnect.Tests;

/// <summary>Integration tests that run the real services against an in-memory SQLite database.</summary>
[TestFixture]
public class AuthServiceTests
{
    private ServiceHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = new ServiceHarness();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task Register_ThenLogin_Succeeds()
    {
        Result<User> registered = await _harness.Auth.RegisterAsync(new RegistrationRequest("Test Student", "Test@Uni.edu", "Demo@1234", "Demo@1234", null));
        Result<User> login = await _harness.Auth.AuthenticateAsync("test@uni.edu", "Demo@1234");

        Assert.Multiple(() =>
        {
            Assert.That(registered.Succeeded, Is.True, registered.Error);
            Assert.That(registered.Value!.Email, Is.EqualTo("test@uni.edu"), "e-mails are stored in lower case");
            Assert.That(login.Succeeded, Is.True);
        });
    }

    [Test]
    public async Task Register_RejectsDuplicateEmailWeakPasswordAndMismatch()
    {
        await _harness.Auth.RegisterAsync(new RegistrationRequest("First", "dup@uni.edu", "Demo@1234", "Demo@1234", null));

        Result<User> duplicate = await _harness.Auth.RegisterAsync(new RegistrationRequest("Second", "DUP@uni.edu", "Demo@1234", "Demo@1234", null));
        Result<User> weak = await _harness.Auth.RegisterAsync(new RegistrationRequest("Weak", "weak@uni.edu", "password", "password", null));
        Result<User> mismatch = await _harness.Auth.RegisterAsync(new RegistrationRequest("Mismatch", "mm@uni.edu", "Demo@1234", "Demo@12345", null));
        Result<User> badEmail = await _harness.Auth.RegisterAsync(new RegistrationRequest("Bad", "not-an-email", "Demo@1234", "Demo@1234", null));

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Error, Is.EqualTo("Email already registered."));
            Assert.That(weak.Failed, Is.True);
            Assert.That(mismatch.Error, Is.EqualTo("Passwords do not match."));
            Assert.That(badEmail.Error, Is.EqualTo("Invalid email format."));
        });
    }

    [Test]
    public async Task Login_WithWrongPassword_Fails()
    {
        _harness.CreateStudent("Alice Johnson");
        Result<User> result = await _harness.Auth.AuthenticateAsync("alice.johnson@test.edu", "Wrong@1234");
        Assert.That(result.Error, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task PasswordReset_FullFlow_ChangesThePassword()
    {
        _harness.CreateStudent("Reset Me");

        PasswordResetRequestResult request = await _harness.Auth.RequestPasswordResetAsync("reset.me@test.edu", "https://localhost/account/reset-password");
        Assert.That(request.DevelopmentResetLink, Is.Not.Null, "development mode exposes the link");
        Assert.That(_harness.Email.Sent, Has.Count.EqualTo(1));

        string token = Uri.UnescapeDataString(request.DevelopmentResetLink!.Split("token=")[1]);
        OperationResult reset = await _harness.Auth.ResetPasswordAsync(token, "NewPass@99", "NewPass@99");
        OperationResult reused = await _harness.Auth.ResetPasswordAsync(token, "Other@999", "Other@999");

        Result<User> loginWithNewPassword = await _harness.Auth.AuthenticateAsync("reset.me@test.edu", "NewPass@99");

        Assert.Multiple(() =>
        {
            Assert.That(reset.Succeeded, Is.True, reset.Error);
            Assert.That(reused.Failed, Is.True, "a reset token can only be used once");
            Assert.That(loginWithNewPassword.Succeeded, Is.True);
        });
    }

    [Test]
    public async Task PasswordReset_ForUnknownEmail_DoesNotRevealAnything()
    {
        PasswordResetRequestResult result = await _harness.Auth.RequestPasswordResetAsync("nobody@test.edu", "https://localhost/reset");
        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.DevelopmentResetLink, Is.Null);
            Assert.That(_harness.Email.Sent, Is.Empty);
        });
    }
}

[TestFixture]
public class ConnectionAndMessageServiceTests
{
    private ServiceHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = new ServiceHarness();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task Messaging_RequiresAnAcceptedConnection()
    {
        int alice = _harness.CreateStudent("Alice");
        int bob = _harness.CreateStudent("Bob");

        Result<DirectMessage> beforeConnecting = await _harness.Messages.SendAsync(alice, bob, "Hi Bob!");

        await _harness.Connections.SendRequestAsync(alice, bob);
        Result<DirectMessage> whilePending = await _harness.Messages.SendAsync(alice, bob, "Hi Bob!");

        OperationResult accepted = await _harness.Connections.AcceptAsync(bob, alice);
        Result<DirectMessage> afterAccepting = await _harness.Messages.SendAsync(alice, bob, "Hi Bob!");

        Assert.Multiple(() =>
        {
            Assert.That(beforeConnecting.Failed, Is.True);
            Assert.That(whilePending.Failed, Is.True);
            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(afterAccepting.Succeeded, Is.True, afterAccepting.Error);
        });
    }

    [Test]
    public async Task Conversations_TrackUnreadMessages()
    {
        int alice = _harness.CreateStudent("Alice");
        int bob = _harness.CreateStudent("Bob");
        await _harness.Connections.SendRequestAsync(alice, bob);
        await _harness.Connections.AcceptAsync(bob, alice);

        await _harness.Messages.SendAsync(alice, bob, "Message 1");
        await _harness.Messages.SendAsync(alice, bob, "Message 2");

        int unreadBefore = await _harness.Messages.GetUnreadCountAsync(bob);
        IReadOnlyList<ConversationSummary> inbox = await _harness.Messages.GetConversationsAsync(bob);
        int marked = await _harness.Messages.MarkConversationReadAsync(bob, alice);
        int unreadAfter = await _harness.Messages.GetUnreadCountAsync(bob);

        Assert.Multiple(() =>
        {
            Assert.That(unreadBefore, Is.EqualTo(2));
            Assert.That(inbox.Single().LastMessage, Is.EqualTo("Message 2"));
            Assert.That(inbox.Single().UnreadCount, Is.EqualTo(2));
            Assert.That(marked, Is.EqualTo(2));
            Assert.That(unreadAfter, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SendingRequestBack_AcceptsTheExistingRequest()
    {
        int alice = _harness.CreateStudent("Alice");
        int bob = _harness.CreateStudent("Bob");

        await _harness.Connections.SendRequestAsync(alice, bob);
        await _harness.Connections.SendRequestAsync(bob, alice);

        Assert.That(await _harness.Connections.AreConnectedAsync(alice, bob), Is.True);
    }

    [Test]
    public async Task RealtimeSubscribers_ReceiveNewMessages()
    {
        int alice = _harness.CreateStudent("Alice");
        int bob = _harness.CreateStudent("Bob");
        await _harness.Connections.SendRequestAsync(alice, bob);
        await _harness.Connections.AcceptAsync(bob, alice);

        var received = new List<RealtimeEvent>();
        using IDisposable subscription = _harness.Realtime.Subscribe(bob, realtimeEvent =>
        {
            received.Add(realtimeEvent);
            return Task.CompletedTask;
        });

        await _harness.Messages.SendAsync(alice, bob, "Ping");

        Assert.Multiple(() =>
        {
            Assert.That(_harness.Realtime.IsOnline(bob), Is.True);
            Assert.That(received.Any(e => e.Type == RealtimeEventType.DirectMessage && e.FromUserId == alice), Is.True);
        });
    }
}

[TestFixture]
public class TeamServiceTests
{
    private ServiceHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = new ServiceHarness();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task FullTeamLifecycle_RespectsSameLabAndSizeRules()
    {
        (int subjectId, int lab1, int lab2) = _harness.CreateSubject(maxTeamSize: 3);
        int leader = _harness.CreateStudent("Leader", subjectId: subjectId, labId: lab1, role: TeamRole.Backend);
        int mate1 = _harness.CreateStudent("Mate One", subjectId: subjectId, labId: lab1, role: TeamRole.Frontend);
        int mate2 = _harness.CreateStudent("Mate Two", subjectId: subjectId, labId: lab1, role: TeamRole.Testing);
        int mate3 = _harness.CreateStudent("Mate Three", subjectId: subjectId, labId: lab1);
        int otherLab = _harness.CreateStudent("Other Lab", subjectId: subjectId, labId: lab2);

        Result<Team> created = await _harness.Teams.CreateTeamAsync(leader, new TeamCreateRequest(subjectId, "LINQ Legends", null, 3));
        Assert.That(created.Succeeded, Is.True, created.Error);
        int teamId = created.Value!.Id;

        OperationResult wrongLab = await _harness.Teams.InviteAsync(leader, teamId, otherLab, null);
        Assert.That(wrongLab.Error, Does.Contain("different lab"));

        Assert.That((await _harness.Teams.InviteAsync(leader, teamId, mate1, null)).Succeeded, Is.True);
        Assert.That((await _harness.Teams.InviteAsync(leader, teamId, mate2, null)).Succeeded, Is.True);
        Assert.That((await _harness.Teams.InviteAsync(leader, teamId, mate3, null)).Succeeded, Is.True);

        await AcceptInvitationAsync(mate1);
        await AcceptInvitationAsync(mate2);

        Team? team = await _harness.Teams.GetTeamAsync(teamId);
        IReadOnlyList<TeamInvitation> mate3Invites = await _harness.Teams.GetPendingInvitationsAsync(mate3);

        Assert.Multiple(() =>
        {
            Assert.That(team!.Members, Has.Count.EqualTo(3));
            Assert.That(team.IsFull, Is.True);
            Assert.That(mate3Invites, Is.Empty, "remaining invitations are cancelled once the team is full");
        });
    }

    [Test]
    public async Task TeamCanHaveSixMembersAndOnlyLeaderCanApproveJoinRequests()
    {
        (int subjectId, int lab1, _) = _harness.CreateSubject(maxTeamSize: 3);
        int leader = _harness.CreateStudent("Leader", new[] { "databases" }, subjectId, lab1);
        int member = _harness.CreateStudent("Member", new[] { "frontend" }, subjectId, lab1);
        int requester = _harness.CreateStudent("Requester", new[] { "pottery" }, subjectId, lab1);

        Result<Team> created = await _harness.Teams.CreateTeamAsync(leader, new TeamCreateRequest(subjectId, "Six Person Team", null, 6));
        Assert.That(created.Succeeded, Is.True, created.Error);

        OperationResult requested = await _harness.Teams.RequestToJoinAsync(requester, created.Value!.Id, null);
        IReadOnlyList<TeamInvitation> visibleToMember = await _harness.Teams.GetPendingInvitationsAsync(member);
        TeamInvitation request = (await _harness.Teams.GetPendingInvitationsAsync(leader)).Single();
        OperationResult memberApproval = await _harness.Teams.RespondToInvitationAsync(member, request.Id, accept: true);
        OperationResult leaderApproval = await _harness.Teams.RespondToInvitationAsync(leader, request.Id, accept: true);
        Team? updated = await _harness.Teams.GetTeamAsync(created.Value.Id);

        Assert.Multiple(() =>
        {
            Assert.That(requested.Succeeded, Is.True, requested.Error, "join requests do not require similar interests");
            Assert.That(visibleToMember, Is.Empty, "join requests are visible only to the team leader");
            Assert.That(memberApproval.Failed, Is.True);
            Assert.That(memberApproval.Error, Does.Contain("team leader"));
            Assert.That(leaderApproval.Succeeded, Is.True, leaderApproval.Error);
            Assert.That(updated!.MaxMembers, Is.EqualTo(6));
            Assert.That(updated.Members.Select(m => m.UserId), Does.Contain(requester));
        });
    }

    [Test]
    public async Task StudentCannotJoinTwoTeamsInTheSameSubject()
    {
        (int subjectId, int lab1, _) = _harness.CreateSubject();
        int a = _harness.CreateStudent("A", subjectId: subjectId, labId: lab1);
        int b = _harness.CreateStudent("B", subjectId: subjectId, labId: lab1);

        await _harness.Teams.CreateTeamAsync(a, new TeamCreateRequest(subjectId, "Team A", null, 3));
        Result<Team> teamB = await _harness.Teams.CreateTeamAsync(b, new TeamCreateRequest(subjectId, "Team B", null, 3));
        Result<Team> secondTeamForA = await _harness.Teams.CreateTeamAsync(a, new TeamCreateRequest(subjectId, "Another", null, 3));

        OperationResult inviteA = await _harness.Teams.InviteAsync(b, teamB.Value!.Id, a, null);

        Assert.Multiple(() =>
        {
            Assert.That(secondTeamForA.Failed, Is.True);
            Assert.That(inviteA.Error, Does.Contain("already in another team"));
        });
    }

    [Test]
    public async Task LeaderLeaving_TransfersLeadership()
    {
        (int subjectId, int lab1, _) = _harness.CreateSubject();
        int leader = _harness.CreateStudent("Leader", subjectId: subjectId, labId: lab1);
        int member = _harness.CreateStudent("Member", subjectId: subjectId, labId: lab1);

        Result<Team> created = await _harness.Teams.CreateTeamAsync(leader, new TeamCreateRequest(subjectId, "Handover", null, 2));
        int teamId = created.Value!.Id;
        await _harness.Teams.RequestToJoinAsync(member, teamId, "Let me in!");
        TeamInvitation request = (await _harness.Teams.GetPendingInvitationsAsync(leader)).Single();
        await _harness.Teams.RespondToInvitationAsync(leader, request.Id, accept: true);

        OperationResult left = await _harness.Teams.LeaveTeamAsync(leader, teamId);
        Team? team = await _harness.Teams.GetTeamAsync(teamId);

        Assert.Multiple(() =>
        {
            Assert.That(left.Succeeded, Is.True);
            Assert.That(team!.LeaderId, Is.EqualTo(member));
            Assert.That(team.Members.Select(m => m.UserId), Is.EqualTo(new[] { member }));
        });
    }

    [Test]
    public async Task TeamFinder_OnlySuggestsStudentsFromMyLab()
    {
        (int subjectId, int lab1, int lab2) = _harness.CreateSubject();
        int me = _harness.CreateStudent("Me", new[] { "c#", "ml" }, subjectId, lab1, TeamRole.Backend);
        _harness.CreateStudent("Same Lab A", new[] { "c#" }, subjectId, lab1, TeamRole.Frontend);
        _harness.CreateStudent("Same Lab B", new[] { "ml" }, subjectId, lab1, TeamRole.Testing);
        int otherLab = _harness.CreateStudent("Other Lab", new[] { "c#", "ml" }, subjectId, lab2, TeamRole.Frontend);

        TeamFinderResult result = await _harness.Matchmaking.FindTeammatesAsync(me, subjectId, new TeamFinderFilter());

        Assert.Multiple(() =>
        {
            Assert.That(result.Candidates, Has.Count.EqualTo(2));
            Assert.That(result.Candidates.Select(c => c.Profile.UserId), Does.Not.Contain(otherLab));
            Assert.That(result.Suggestions, Is.Not.Empty);
            Assert.That(result.Suggestions[0].Members, Has.Count.EqualTo(3));
            Assert.That(result.AvailableInMyLab, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task TeamFinder_SuggestsFiveMemberTeamsWhenSubjectMaximumIsThree()
    {
        (int subjectId, int lab1, _) = _harness.CreateSubject(maxTeamSize: 3);
        int me = _harness.CreateStudent("Me", subjectId: subjectId, labId: lab1);
        for (int index = 1; index <= 5; index++)
        {
            _harness.CreateStudent($"Student {index}", subjectId: subjectId, labId: lab1);
        }

        TeamFinderResult result = await _harness.Matchmaking.FindTeammatesAsync(
            me,
            subjectId,
            new TeamFinderFilter { TeamSize = 5 });

        Assert.That(result.Suggestions, Is.Not.Empty);
        Assert.That(result.Suggestions[0].Members, Has.Count.EqualTo(5));
    }

    private async Task AcceptInvitationAsync(int userId)
    {
        TeamInvitation invitation = (await _harness.Teams.GetPendingInvitationsAsync(userId)).Single(i => i.UserId == userId);
        OperationResult result = await _harness.Teams.RespondToInvitationAsync(userId, invitation.Id, accept: true);
        Assert.That(result.Succeeded, Is.True, result.Error);
    }
}

[TestFixture]
public class DatabaseSeederTests
{
    private string _seedDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _seedDirectory = Path.Combine(Path.GetTempPath(), "studyconnect-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_seedDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_seedDirectory))
        {
            Directory.Delete(_seedDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Seeder_ReadsJsonFilesFromDisk()
    {
        // File writing: create small seed files on disk...
        await File.WriteAllTextAsync(Path.Combine(_seedDirectory, "users.json"),
            """
            [
              { "name": "Seed One", "email": "one@seed.edu", "interests": ["C#", "AI"], "availability": ["Monday:Morning", "Bad:Value"] },
              { "name": "Seed Two", "email": "two@seed.edu", "interests": ["AI"], "availability": ["Friday:Evening"] }
            ]
            """);
        await File.WriteAllTextAsync(Path.Combine(_seedDirectory, "subjects.json"),
            """
            [ { "code": "31927", "name": ".NET", "labs": [ { "code": "Lab 01", "day": "Monday", "start": "09:00", "end": "11:00" } ] } ]
            """);
        await File.WriteAllTextAsync(Path.Combine(_seedDirectory, "enrollments.json"),
            """
            [ { "email": "one@seed.edu", "subjectCode": "31927", "labCode": "Lab 01", "role": "Backend", "skill": "Advanced", "goal": "HighDistinction" } ]
            """);

        // ...then let the seeder read them back (missing files are skipped gracefully).
        using var database = new TestDatabase();
        await using (StudyConnectDbContext db = database.CreateContext())
        {
            var seeder = new DatabaseSeeder(db, new Core.Security.Pbkdf2PasswordHasher(1_000), _seedDirectory, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            await seeder.SeedAsync();
        }

        await using StudyConnectDbContext check = database.CreateContext();
        List<User> users = check.Users.Include(u => u.Interests).Include(u => u.Availability).OrderBy(u => u.Email).ToList();
        Enrollment enrollment = check.Enrollments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(users, Has.Count.EqualTo(2));
            Assert.That(users[0].Interests.Select(i => i.Name), Is.EquivalentTo(new[] { "c#", "ai" }));
            Assert.That(users[0].Availability, Has.Count.EqualTo(1), "invalid availability entries are ignored");
            Assert.That(enrollment.Goal, Is.EqualTo(TargetGrade.HighDistinction));
            Assert.That(check.Subjects.Single().Code, Is.EqualTo("31927"));
        });
    }
}
