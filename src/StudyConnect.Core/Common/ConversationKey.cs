using System.Globalization;

namespace StudyConnect.Core.Common;

/// <summary>
/// Builds a stable conversation identifier for two users. The smaller id always comes
/// first, so both participants share the same key (ported from the original Node.js
/// <c>makeConversationId</c> helper).
/// </summary>
public static class ConversationKey
{
    public static string For(int firstUserId, int secondUserId)
    {
        int low = Math.Min(firstUserId, secondUserId);
        int high = Math.Max(firstUserId, secondUserId);
        return string.Create(CultureInfo.InvariantCulture, $"{low}_{high}");
    }

    /// <summary>Given a key and one participant, returns the other participant's id.</summary>
    public static int? OtherParticipant(string key, int userId)
    {
        string[] parts = key.Split('_');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b))
        {
            return null;
        }

        if (a == userId)
        {
            return b;
        }

        return b == userId ? a : null;
    }
}
