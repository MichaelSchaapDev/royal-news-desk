namespace RoyalNewsDesk.Core.Models;

/// <summary>One subtitle cue on the master timeline.</summary>
public sealed record SubtitleCue(TimeSpan Start, TimeSpan End, string Text);
