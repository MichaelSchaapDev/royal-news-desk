namespace RoyalNewsDesk.App.Services;

/// <summary>Pages that persist their edits when the user navigates away.</summary>
public interface ISavable
{
    void Save();
}
