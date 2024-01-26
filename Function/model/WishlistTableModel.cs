using Azure;
using Azure.Data.Tables;

namespace AzureFunctionExample.Model;
public class WishlistTableModel : ITableEntity
{
    // wont use; set to empty string -> ""
    public string PartitionKey { get; set; } = string.Empty;

    //game unique Id
    public required string RowKey { get; set; }

    // define last insert / update date
    public DateTimeOffset? Timestamp { get; set; }

    // used for cache validation; can be ingored for our use case
    public ETag ETag { get; set; }

    public required string Name { get; set; }

    public required string Url { get; set; }

    public required int ReleaseDate { get; set; }
}
