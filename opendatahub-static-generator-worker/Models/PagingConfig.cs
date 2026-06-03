namespace GeneratorWorker.Models;

public class PagingConfig
{
    public bool Enabled { get; set; } = false;

    /// <summary>Query parameter name used to request a specific page (e.g. "pagenumber", "page", "offset").</summary>
    public string PageQueryParam { get; set; } = "pagenumber";

    /// <summary>Query parameter name for page size. Omit to not send a page-size parameter.</summary>
    public string? PageSizeQueryParam { get; set; }

    /// <summary>Number of items per page sent to the remote API.</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>Value of PageQueryParam for the first request (usually 1 or 0).</summary>
    public int StartPage { get; set; } = 1;

    /// <summary>
    /// Dot-notation path to the array of items inside the response object.
    /// Leave empty when the response root is already a JSON array.
    /// Supports nested paths like "data.results".
    /// </summary>
    public string? DataPath { get; set; }

    /// <summary>
    /// Dot-notation path to the total number of pages in the response (e.g. "TotalPages").
    /// Paging stops when currentPage >= TotalPages.
    /// </summary>
    public string? TotalPagesPath { get; set; }

    /// <summary>
    /// Dot-notation path to the total item count in the response (e.g. "TotalCount", "meta.total").
    /// Paging stops when accumulated items >= TotalCount. Use TotalPagesPath instead when the API
    /// exposes a page count rather than an item count.
    /// </summary>
    public string? TotalCountPath { get; set; }

    /// <summary>
    /// Dot-notation path to a boolean field that signals more pages are available
    /// (e.g. "hasMore", "meta.hasNextPage"). Used when neither TotalPagesPath nor TotalCountPath
    /// is available.
    /// </summary>
    public string? HasMorePath { get; set; }
}
