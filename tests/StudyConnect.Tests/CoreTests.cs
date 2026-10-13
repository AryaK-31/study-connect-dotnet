using StudyConnect.Core.Common;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Extensions;
using StudyConnect.Core.Matching;
using StudyConnect.Core.Security;
using StudyConnect.Core.Validation;

namespace StudyConnect.Tests;

[TestFixture]
public class PasswordPolicyTests
{
    private readonly IPasswordPolicy _policy = new PasswordPolicy();

    [Test]
    public void StrongPassword_IsValid()
    {
        PasswordValidationResult result = _policy.Validate("Demo@1234");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Strength, Is.EqualTo(5));
        });
    }

    [TestCase("short1!A", true)]
    [TestCase("alllowercase1!", false)]
    [TestCase("ALLUPPERCASE1!", false)]
    [TestCase("NoDigitsHere!", false)]
    [TestCase("NoSpecial123", false)]
    [TestCase("Sh0rt!", false)]
    public void Validate_AppliesEveryRule(string password, bool expected)
    {
        Assert.That(_policy.Validate(password).IsValid, Is.EqualTo(expected));
    }

    [Test]
    public void MissingPassword_ReportsRequired()
    {
        PasswordValidationResult result = _policy.Validate(null);
        Assert.That(result.Errors, Does.Contain("Password is required"));
    }

    [Test]
    public void CustomRequirements_AreRespected()
    {
        var relaxed = new PasswordPolicy(new PasswordRequirements(4, false, false, false, false));
        Assert.That(relaxed.Validate("abcd").IsValid, Is.True);
    }
}

[TestFixture]
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher(1_000);

    [Test]
    public void Hash_ThenVerify_Succeeds()
    {
        string hash = _hasher.Hash("Demo@1234");
        Assert.That(_hasher.Verify("Demo@1234", hash), Is.True);
    }

    [Test]
    public void Verify_WithWrongPassword_Fails()
    {
        string hash = _hasher.Hash("Demo@1234");
        Assert.That(_hasher.Verify("demo@1234", hash), Is.False);
    }

    [Test]
    public void SamePassword_ProducesDifferentHashes_BecauseOfRandomSalt()
    {
        Assert.That(_hasher.Hash("Demo@1234"), Is.Not.EqualTo(_hasher.Hash("Demo@1234")));
    }

    [Test]
    public void Verify_WithCorruptHash_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_hasher.Verify("x", "not-a-hash"), Is.False);
            Assert.That(_hasher.Verify("x", "PBKDF2$1000$@@@$@@@"), Is.False);
        });
    }
}

[TestFixture]
public class ExtensionMethodTests
{
    [TestCase("  Machine   LEARNING ", "machine learning")]
    [TestCase("C#", "c#")]
    [TestCase("", "")]
    public void NormalizeTag_TrimsCollapsesAndLowercases(string input, string expected)
    {
        Assert.That(input.NormalizeTag(), Is.EqualTo(expected));
    }

    [TestCase("machine learning", "Machine Learning")]
    [TestCase("ai", "AI")]
    [TestCase("ui/ux design", "UI/UX Design")]
    [TestCase(".net", ".NET")]
    public void ToTitleCase_KeepsAcronyms(string input, string expected)
    {
        Assert.That(input.ToTitleCase(), Is.EqualTo(expected));
    }

    [TestCase("Alice Johnson", "AJ")]
    [TestCase("Cher", "C")]
    [TestCase("  ", "?")]
    public void ToInitials_ReturnsUpToTwoLetters(string name, string expected)
    {
        Assert.That(name.ToInitials(), Is.EqualTo(expected));
    }

    [Test]
    public void ToRelativeTime_DescribesElapsedTime()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(now.AddSeconds(-10).ToRelativeTime(now), Is.EqualTo("just now"));
            Assert.That(now.AddMinutes(-5).ToRelativeTime(now), Is.EqualTo("5m ago"));
            Assert.That(now.AddHours(-3).ToRelativeTime(now), Is.EqualTo("3h ago"));
            Assert.That(now.AddDays(-1).AddHours(-1).ToRelativeTime(now), Is.EqualTo("yesterday"));
        });
    }

    [Test]
    public void ToDisplayName_UsesDescriptionOrSplitsPascalCase()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TeamRole.Design.ToDisplayName(), Is.EqualTo("UI/UX Design"));
            Assert.That(TargetGrade.HighDistinction.ToDisplayName(), Is.EqualTo("High Distinction"));
            Assert.That(SkillLevel.Advanced.ToDisplayName(), Is.EqualTo("Advanced"));
        });
    }

    [Test]
    public void JaccardSimilarity_ComputesIntersectionOverUnion()
    {
        var a = new[] { "a", "b", "c" };
        var b = new[] { "b", "c", "d" };
        Assert.That(a.JaccardSimilarity(b), Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void TopBy_ReturnsHighestScoresInDescendingOrder()
    {
        int[] numbers = { 5, 1, 9, 3, 7 };
        IReadOnlyList<int> top = numbers.TopBy(3, n => n);
        Assert.That(top, Is.EqualTo(new[] { 9, 7, 5 }));
    }
}

[TestFixture]
public class CommonTypeTests
{
    [Test]
    public void ConversationKey_IsTheSameForBothParticipants()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConversationKey.For(7, 3), Is.EqualTo("3_7"));
            Assert.That(ConversationKey.For(3, 7), Is.EqualTo(ConversationKey.For(7, 3)));
            Assert.That(ConversationKey.OtherParticipant("3_7", 3), Is.EqualTo(7));
            Assert.That(ConversationKey.OtherParticipant("3_7", 9), Is.Null);
        });
    }

    [Test]
    public void Combinations_ProducesEveryUniqueSubset()
    {
        string[] items = { "A", "B", "C", "D" };
        List<string> combos = Combinatorics.Combinations(items, 2).Select(c => string.Concat(c)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(combos, Has.Count.EqualTo(Combinatorics.Choose(4, 2)));
            Assert.That(combos, Is.EquivalentTo(new[] { "AB", "AC", "AD", "BC", "BD", "CD" }));
        });
    }

    [Test]
    public void Combinations_HandlesEdgeCases()
    {
        int[] items = { 1, 2, 3 };
        Assert.Multiple(() =>
        {
            Assert.That(Combinatorics.Combinations(items, 0).Count(), Is.EqualTo(1));
            Assert.That(Combinatorics.Combinations(items, 4), Is.Empty);
            Assert.That(Combinatorics.Choose(12, 2), Is.EqualTo(66));
        });
    }

    [Test]
    public void WeeklyAvailability_IndexerAndEnumeratorAgree()
    {
        var grid = new WeeklyAvailability();
        grid[DayOfWeek.Monday, TimeBlock.Morning] = true;
        grid.Toggle(DayOfWeek.Friday, TimeBlock.Evening);
        grid.Toggle(DayOfWeek.Sunday, TimeBlock.Afternoon);

        List<AvailabilitySlot> slots = grid.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(grid.Count, Is.EqualTo(3));
            Assert.That(slots[0], Is.EqualTo(new AvailabilitySlot(DayOfWeek.Monday, TimeBlock.Morning)));
            Assert.That(slots[^1].Day, Is.EqualTo(DayOfWeek.Sunday), "Sunday is listed last (week starts on Monday)");
        });
    }

    [Test]
    public void AvailabilitySlot_ParsesSeedFormat()
    {
        bool parsed = AvailabilitySlot.TryParse("wednesday:evening", out AvailabilitySlot slot);
        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(slot, Is.EqualTo(new AvailabilitySlot(DayOfWeek.Wednesday, TimeBlock.Evening)));
            Assert.That(AvailabilitySlot.TryParse("Someday:Never", out _), Is.False);
        });
    }

    [Test]
    public void Result_CarriesValueOrError()
    {
        Result<string> ok = Result<string>.Success("hi");
        Result<string> failed = Result<string>.Failure("nope");

        Assert.Multiple(() =>
        {
            Assert.That(ok.Succeeded, Is.True);
            Assert.That(ok.Value, Is.EqualTo("hi"));
            Assert.That(failed.Failed, Is.True);
            Assert.That(failed.Error, Is.EqualTo("nope"));
        });
    }
}
