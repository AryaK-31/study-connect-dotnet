using System.ComponentModel;

namespace StudyConnect.Core.Enums;

/// <summary>Part of the day a student is free to study.</summary>
public enum TimeBlock
{
    [Description("Morning (8am–12pm)")]
    Morning = 0,

    [Description("Afternoon (12pm–5pm)")]
    Afternoon = 1,

    [Description("Evening (5pm–10pm)")]
    Evening = 2
}

/// <summary>Lifecycle of a peer-to-peer connection request.</summary>
public enum ConnectionStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

/// <summary>
/// The relationship between the signed-in student and another student,
/// as seen from the signed-in student's point of view (drives which button the UI shows).
/// </summary>
public enum PeerStatus
{
    None = 0,
    Connected = 1,
    RequestSent = 2,
    RequestReceived = 3
}

/// <summary>The role a student would like to play inside an assignment team.</summary>
public enum TeamRole
{
    [Description("Full-stack")]
    FullStack = 0,

    [Description("Front-end / UI")]
    Frontend = 1,

    [Description("Back-end / API")]
    Backend = 2,

    [Description("Database")]
    Database = 3,

    [Description("Testing / QA")]
    Testing = 4,

    [Description("UI/UX Design")]
    Design = 5,

    [Description("Documentation & Report")]
    Documentation = 6,

    [Description("Project Manager")]
    ProjectManager = 7
}

/// <summary>Self-assessed programming confidence for a subject.</summary>
public enum SkillLevel
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2
}

/// <summary>The grade a student is aiming for. Teams work best when goals are aligned.</summary>
public enum TargetGrade
{
    Pass = 0,
    Credit = 1,
    Distinction = 2,

    [Description("High Distinction")]
    HighDistinction = 3
}

/// <summary>Where a study session takes place.</summary>
public enum SessionMode
{
    [Description("On campus")]
    OnCampus = 0,

    Online = 1,

    Hybrid = 2
}

/// <summary>Distinguishes a team inviting a student from a student asking to join a team.</summary>
public enum InvitationKind
{
    [Description("Invitation")]
    Invite = 0,

    [Description("Join request")]
    JoinRequest = 1
}

/// <summary>Lifecycle of a team invitation / join request.</summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3
}

/// <summary>Category of an in-app notification (used to pick an icon in the UI).</summary>
public enum NotificationType
{
    System = 0,
    ConnectionRequest = 1,
    ConnectionAccepted = 2,
    SessionJoined = 3,
    SessionCancelled = 4,
    TeamInvite = 5,
    TeamJoinRequest = 6,
    TeamUpdate = 7
}

/// <summary>Kinds of real-time events pushed to connected browsers.</summary>
public enum RealtimeEventType
{
    DirectMessage = 0,
    MessagesRead = 1,
    ConnectionChanged = 2,
    Notification = 3,
    TeamChanged = 4,
    TeamMessage = 5,
    SessionChanged = 6
}
