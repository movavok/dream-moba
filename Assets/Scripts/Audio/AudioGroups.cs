public static class AudioGroups
{
    public const string Global = "Global";

    public static string Hero(string heroId)
    {
        return Combine("Hero", heroId);
    }

    public static string Item(string itemId)
    {
        return Combine("Item", itemId);
    }

    public static string MinionBoss(string bossId)
    {
        return Combine("MinionBoss", bossId);
    }

    public static string Creep(string creepId)
    {
        return Combine("Creep", creepId);
    }

    private static string Combine(string prefix, string id)
    {
        return prefix + "/" +
            (string.IsNullOrEmpty(id) ? "Unknown" : id);
    }
}
