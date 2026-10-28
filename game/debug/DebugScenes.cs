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
            "grazer-demo" => "res://tests/grazer_demo.tscn",
            "home-morning" => "res://flow/home_morning.tscn",
            "home-evening" => "res://flow/home_evening_debug.tscn",
            "math-class" => "res://flow/math_class.tscn",
            "school-road" => "res://flow/school_road.tscn",
            "home-road" => "res://flow/home_road.tscn",
            "recess" => "res://flow/recess.tscn",
            "duty" => "res://flow/duty.tscn",
            "cat-room" => "res://flow/cat_room_debug.tscn",
            _ => null
        };
#else
        return null;
#endif
    }
}
