namespace Game.Debugging;

/// <summary>Explicit debug launch entries; each scene owns its opening state.</summary>
public static class DebugScenes
{
    public static string? Find(string id)
    {
#if DEBUG
        return id switch
        {
            "systems-demo" => "res://tests/systems_demo.tscn",
            "seam-demo" => "res://tests/seam_demo.tscn",
            "item-test" => "res://regions/item_test.tscn",
            _ => null
        };
#else
        return null;
#endif
    }
}
