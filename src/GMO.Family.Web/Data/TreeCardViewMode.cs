namespace GMO.Family.Web.Data;

public enum TreeCardViewMode
{
    Compact = 0,
    Standard = 1,
    Large = 2,
    Details = 3,
    PhotoSmall = 4,
    PhotoMedium = 5,
    PhotoLarge = 6,
    PhotoExtraLarge = 7
}

public static class TreeCardViewModeExtensions
{
    public static bool IsPhotoOnly(this TreeCardViewMode mode) =>
        mode is TreeCardViewMode.PhotoSmall or TreeCardViewMode.PhotoMedium
            or TreeCardViewMode.PhotoLarge or TreeCardViewMode.PhotoExtraLarge;

    public static string ToCssClass(this TreeCardViewMode mode) => mode switch
    {
        TreeCardViewMode.PhotoSmall => "ft-view-photo-small",
        TreeCardViewMode.PhotoMedium => "ft-view-photo-medium",
        TreeCardViewMode.PhotoLarge => "ft-view-photo-large",
        TreeCardViewMode.PhotoExtraLarge => "ft-view-photo-xlarge",
        _ => "ft-view-" + mode.ToString().ToLowerInvariant()
    };

    public static string GetLabel(this TreeCardViewMode mode) => mode switch
    {
        TreeCardViewMode.Compact => "Compact",
        TreeCardViewMode.Standard => "Standard",
        TreeCardViewMode.Large => "Large",
        TreeCardViewMode.Details => "Details",
        TreeCardViewMode.PhotoSmall => "Small icons",
        TreeCardViewMode.PhotoMedium => "Medium icons",
        TreeCardViewMode.PhotoLarge => "Large icons",
        TreeCardViewMode.PhotoExtraLarge => "Extra large icons",
        _ => mode.ToString()
    };
}