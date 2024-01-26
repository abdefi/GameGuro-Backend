namespace AzureFunctionExample.Model;
/// <summary>
/// Defines the model for a game in the wishlist.
/// </summary>
/// <param name="Id">Unique identifier of the game.</param>
/// <param name="Name">Name of the game</param>
/// <param name="Url">Url of the game in the website.</param>
/// <param name="ReleaseDate">Date of publication of the game.</param>
public record WishlistModel
(
    string Id,
    string Name,
    string Url,
    int ReleaseDate
)
{
    // helper method to transform a given WishlistTableModel to a WishlistModel
    public static WishlistModel FromWishlistTableModel(WishlistTableModel row)
    {
        return new
        (
            Id: row.RowKey,
            Name: row.Name,
            Url: row.Url,
            ReleaseDate: row.ReleaseDate
        );
    }
};
