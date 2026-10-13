using StudyConnect.Core.Common;
using StudyConnect.Core.Enums;

namespace StudyConnect.Core.Entities;

/// <summary>
/// A connection between two students. The requester sends the request and the
/// addressee accepts or declines it. Accepted connections are mutual.
/// </summary>
public class Connection : EntityBase
{
    public int RequesterId { get; set; }

    public User Requester { get; set; } = null!;

    public int AddresseeId { get; set; }

    public User Addressee { get; set; } = null!;

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;

    public DateTime? RespondedAtUtc { get; set; }

    /// <summary>Returns the id of the participant that is not <paramref name="userId"/>.</summary>
    public int OtherUserId(int userId) => RequesterId == userId ? AddresseeId : RequesterId;
}

/// <summary>A private chat message between two connected students.</summary>
public class DirectMessage : EntityBase
{
    public int SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public int RecipientId { get; set; }

    public User Recipient { get; set; } = null!;

    /// <summary>Stable id shared by both participants, see <see cref="Common.ConversationKey"/>.</summary>
    public string ConversationKey { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool IsRead { get; set; }
}

/// <summary>An in-app notification shown in the bell menu.</summary>
public class Notification : EntityBase
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Optional relative URL opened when the notification is clicked.</summary>
    public string? Link { get; set; }

    public bool IsRead { get; set; }
}
