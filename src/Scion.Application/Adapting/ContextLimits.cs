using System.Text;

namespace Scion.Application.Adapting;

/// <summary>
/// How much of a target project goes into a request. A file over a limit is never cut: it is not sent, and so it is not
/// listed either. Folder names count toward the total.
/// </summary>
public sealed record ContextLimits(int MaxFileBytes, int MaxTotalBytes)
{
    public static readonly ContextLimits Default = new(32 * 1024, 128 * 1024);

    /// <summary>
    /// Keeps what fits, in the order given: a file that does not fit is skipped and later ones may still fit. What is
    /// skipped is reported with the reason, so it can be shown; it is not part of what is kept.
    /// </summary>
    /// <param name="folderNamesLabel">The label the folder names have among the shared items (used when they are left out).</param>
    internal Fitted Fit(IEnumerable<SharedFile> files, IReadOnlyList<string> folderNames, string folderNamesLabel)
    {
        var total = 0;
        var kept = new List<SharedFile>();
        var omitted = new List<OmittedItem>();

        foreach (var file in files)
        {
            if (file.Bytes > MaxFileBytes)
            {
                omitted.Add(new OmittedItem(file.Label, new OmittedReason.OverFileLimit(MaxFileBytes)));
            }
            else if (total + file.Bytes <= MaxTotalBytes)
            {
                kept.Add(file);
                total += file.Bytes;
            }
            else
            {
                omitted.Add(new OmittedItem(file.Label, new OmittedReason.TotalLimitReached()));
            }
        }

        if (folderNames.Count == 0)
        {
            return new Fitted(kept, folderNames, omitted);
        }

        var folderNamesBytes = Encoding.UTF8.GetByteCount(FolderNamesText.Of(folderNames));
        if (folderNamesBytes > MaxFileBytes)
        {
            omitted.Add(new OmittedItem(folderNamesLabel, new OmittedReason.OverFileLimit(MaxFileBytes)));

            return new Fitted(kept, [], omitted);
        }

        if (total + folderNamesBytes > MaxTotalBytes)
        {
            omitted.Add(new OmittedItem(folderNamesLabel, new OmittedReason.TotalLimitReached()));

            return new Fitted(kept, [], omitted);
        }

        return new Fitted(kept, folderNames, omitted);
    }

    internal sealed record Fitted(
        IReadOnlyList<SharedFile> Files, IReadOnlyList<string> FolderNames, IReadOnlyList<OmittedItem> Omitted);
}
