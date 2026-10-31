using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Flow;
using Game.Interaction;
using Game.Player;
using Game.Regions;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// Scenes 1 and 3 to 7, each finished by the quickest moves that its own checks use: props are pressed without walking to them,
// and the school scenes are driven through the engine as the keys do. Scenes 1, 4, 5, 6 and 7 are started again on the way.
public partial class PrologueChecks
{
    private async Task PlayMorning()
    {
        await Arrive(1);
        Check(!Player.Hud.NotebookAvailable, "the notebook cannot be opened in the morning");
        var morning = (HomeMorning)prologue.Scene!;
        var begin = Player.Position;
        Prop("Money").Interact();
        // The checkpoint that the flow made starts the scene again, with the money back on the table and her where she began.
        Player.Position += new Vector3(-2, 0, 0);
        await Again(() => morning.GetChildren().OfType<SceneCheckpoint>().Single().Restart(), 1);
        Check(!GodotObject.IsInstanceValid(morning), "the scene that was started again is freed");
        morning = (HomeMorning)prologue.Scene!;
        Check(!morning.MoneyTaken && Prop("Money").Visible && Player.Position.DistanceTo(begin) < .01f, "the money is on the table again and she stands where she began");
        foreach (var name in new[] { "Clock", "Sink", "Money", "Backpack", "Note", "HomeDoors/FrontDoor/Leaf" }) Prop(name).Interact();
        Check(Region.Props("Ⅰ")!.GetNode<Door3D>("HomeDoors/FrontDoor").State == Door3D.DoorState.Open,
            "the door is open once the alarm is off, the teeth brushed and the bag and the money taken");
        // Put down on the sidewalk by the bus stop, which calls the bus.
        Player.Position = new(83.4f, -.1f, 74.3f);
        var bus = Region.Props("Ⅰ")!.GetNode<Bus3D>("Bus");
        await Until(() => bus.State == Bus3D.BusState.Waiting, 60, "the bus must come and open its doors");
        bus.GetChildren().OfType<Interactable3D>().First().Interact();
        // A line that is still being said when the scene finishes is said before the screen fades: the scene leaves the tree
        // after the line's 8 s and the fade, not when it finishes, about 6 s after she boards.
        speed = 1;
        ulong left = 0;
        prologue.Scene!.TreeExited += () => left = Time.GetTicksMsec();
        Player.Hud.ClearText();
        var said = Time.GetTicksMsec();
        Player.Hud.Say("", "车开动了。", 8);
        await Until(() => left != 0, 20, "the scene must leave");
        Check(left - said > 8300, $"the line is said before the scene leaves; it left after {left - said} ms");
        speed = FastSpeed;
    }

    // The school region is loaded once, for scenes 3 to 5.
    private async Task<Region3D> PlayMathClass()
    {
        await Arrive(3);
        var region = Region;
        school.Room = SchoolRoom.Of(region);
        Check(Player.Seated, "she starts the math class in her seat");
        await Until(() => region.PrismEnabled, 150, "the lecture must end");
        foreach (var entity in new[] { "clockII", "deskII49", "chimneyII1" }) Player.EmitSignal(FirstPersonPlayer.SignalName.LensMarked, entity);
        return region;
    }

    private async Task<Region3D> PlayRecess(Region3D schoolRegion)
    {
        await Arrive(4, 1);
        Check(Region == schoolRegion, "scene 4 plays in the region of scene 3");
        CheckKeptScenesIdle(4);
        await Until(() => school.Room.Classmates.All(person => !person.Visible), 40, "the class must leave");
        await school.DoTheTricks();
        await Until(() => Player.Hud.UnlockedPages.Contains(3), 10, "the pages of the tricks must be drawn");
        // Backspace starts the scene again in a school region loaded anew, with the notebook as it was at the start.
        await Again(() => Press(Key.Backspace), 4, 1);
        Check(Region != schoolRegion && !GodotObject.IsInstanceValid(schoolRegion), "the school region is loaded anew");
        school.Room = SchoolRoom.Of(Region);
        await Until(() => school.Room.Classmates.All(person => !person.Visible), 40, "the class must leave again");
        await school.DoTheTricks();
        await Until(() => !Player.Seated, 30, "she must be free to go to the window");
        await school.Turn(new StandUp(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil), new("Ⅱ")));
        return Region;
    }

    private async Task<Region3D> PlayDuty(Region3D schoolRegion)
    {
        await Arrive(5, 1, 2, 3);
        // Scene 4 was started again, so the node that holds the region is the new one: it must not keep the pencil on the sill.
        CheckKeptScenesIdle(5);
        var room = school.Room;
        Check(Region == schoolRegion && room.IsUpright(SchoolRoom.WindowPencil), "scene 5 plays in the same region, and the pencil that scene 4 stood in the rail still stands");
        await Until(() => room.Broom.CollisionLayer != 0, 40, "the chores must start");
        room.Broom.Interact();
        // Backspace starts the scene again in a school region loaded anew, where the pencil stands in the rail as it did.
        await Again(() => Press(Key.Backspace), 5, 1, 2, 3);
        Check(Region != schoolRegion && !GodotObject.IsInstanceValid(schoolRegion), "the school region is loaded anew");
        school.Room = room = SchoolRoom.Of(Region);
        await Until(() => room.IsUpright(SchoolRoom.WindowPencil), 10, "the pencil must stand in the rail again");
        await Until(() => room.Broom.CollisionLayer != 0, 40, "the chores must start again");
        room.Broom.Interact();
        room.Chalkboard.Interact();
        foreach (var row in room.RowBoxes) row.Interact();
        await school.Turn(new LayDown(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil)));
        foreach (var window in SchoolRoom.Windows) Region.World!.DriveDevice(new(window), .69);
        await Until(() => Player.Hud.UnlockedPages.Contains(4), 30, "the page of the glowing things must be drawn");
        room.DoorLock.Interact();
        return Region;
    }

    private async Task PlayHomeRoad(Region3D schoolRegion)
    {
        await Arrive(6, 1, 2, 3, 4);
        var road = (HomeRoad)prologue.Scene!;
        Check(!GodotObject.IsInstanceValid(schoolRegion), "the school region is gone");
        // Scene 6 has 15 mao of its own; it is given the two yuan that scene 1 left her with.
        Check(road.Wallet.Mao == 20 && !Player.Hud.NotebookAvailable, "she comes to the kiosk with the two yuan of scene 1, and the notebook is still shut");
        Prop("IcePop").Interact();
        Check(road.Wallet.Mao == 17, "an ice pop costs 3 mao");
        // Backspace starts the scene again with the money she had at its start.
        await Again(() => Press(Key.Backspace), 6, 1, 2, 3, 4);
        road = (HomeRoad)prologue.Scene!;
        Check(road.Wallet.Mao == 20, "she has the two yuan again");
        Prop("IcePop").Interact();
        Prop("Sausage").Interact();
        Check(road.Wallet.Mao == 12, "an ice pop and a sausage are paid from the two yuan");
        var cat = Region.Character("cat");
        cat.GetNode<Interactable3D>("Pickup").Interact();
        Check(cat.GetNode<Cat>("Cat").Held, "she has the cat in her arms at the door");
        Prop("HomeDoor").Interact();
    }

    private async Task PlayHomeEvening()
    {
        await Arrive(7, 1, 2, 3, 4);
        var evening = (HomeEvening)prologue.Scene!;
        Check(Player.Hud.NotebookAvailable, "the notebook can be opened from here");
        Check(evening.CatBody.GetNode<Cat>("Cat").Held && Player.Position.X < 1.8f && Player.Position.Z is > 6.4f and < 7.35f,
            "she is in the entrance hall just inside the front door with the cat in her arms");
        await Again(() => Press(Key.Backspace), 7, 1, 2, 3, 4);
        evening = (HomeEvening)prologue.Scene!;
        Check(evening.CatBody.GetNode<Cat>("Cat").Held, "the cat is in her arms again");
        foreach (var name in new[] { "Note", "HairClip", "Tv", "Stove" }) Prop(name).Interact();
        await Until(() => evening.Heated, 6, "the meal must be hot");
        Prop("Sausage").Interact();
        await Until(() => evening.CatFed, 20, "the cat must come for its half");
        Prop("Desk").Interact();
    }
}
