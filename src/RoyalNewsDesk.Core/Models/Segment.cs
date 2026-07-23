namespace RoyalNewsDesk.Core.Models;

/// <summary>
/// One story in an episode. The body holds the raw text as the editor shows
/// it, including [PAUSE] lines; the produce-time parser turns it into
/// paragraphs and sentences.
/// </summary>
public sealed class Segment
{
    public required string Id { get; set; }

    /// <summary>Shown in the lower third. Null for the untitled opening segment.</summary>
    public string? Headline { get; set; }

    public string Body { get; set; } = "";

    /// <summary>Image file name inside the episode's images folder, or null.</summary>
    public string? ImageFile { get; set; }
}
