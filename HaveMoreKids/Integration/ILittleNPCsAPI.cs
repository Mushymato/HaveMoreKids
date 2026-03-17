namespace HaveMoreKids.Integration;

public interface ILittleNPCsAPI
{
    /// <summary>
    /// Checks if child index is a valid LittleNPC index.
    /// </summary>
    /// <param name="childIndex"></param>
    /// <returns></returns>
    bool IsValidLittleNPCIndex(int childIndex);

    /// <summary>
    /// The age in days when a child is replaced by a LittleNPC.
    /// </summary>
    int DaysAfterKidsGrowUp { get; }
}
