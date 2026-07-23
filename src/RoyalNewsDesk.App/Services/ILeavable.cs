namespace RoyalNewsDesk.App.Services;

/// <summary>Pages that must stop background work when the user navigates away.</summary>
public interface ILeavable
{
    void OnLeaving();
}
