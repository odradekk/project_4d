using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Flow;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// Scenes 1 to 7, each finished by the quickest moves that its own checks use: props are pressed without walking to them, and
// the school scenes are driven through the engine as the keys do. Scenes 2, 4, 5, 6 and 7 are started again on the way.
public partial class PrologueChecks
{
    private async Task PlayMorning()
    {
        await Arrive(1);
        Check(!Player.Hud.NotebookAvailable, "the notebook cannot be opened in the morning");
        foreach (var name in new[] { "Clock", "Sink", "Money", "Backpack", "Note", "FrontDoor" }) Prop(name).Interact();
        Check(((HomeMorning)prologue.Scene!).DoorOpen, "the door is open once the alarm is off, the teeth brushed and the bag and the money taken");
        Prop("StairDoor").Interact();
    }

    private async Task PlaySchoolRoad()
    {
        await Arrive(2);
        var road = (SchoolRoad)prologue.Scene!;
        Check(road.Wallet.Mao == 20 && Player.Position.Y > 2.9f, "the two yuan of the table are in her pocket, and she stands on the landing");
        var begin = Player.Position;
        Prop("BunStall").Interact();
        Check(road.Wallet.Mao == 15, "the bun costs 5 mao");
        // The checkpoint that the flow made starts the scene again, with the money she had at its start, where she began.
        Player.Position += new Vector3(3, 0, 0);
        await Again(() => road.GetChildren().OfType<SceneCheckpoint>().Single().Restart(), 2);
        Check(!GodotObject.IsInstanceValid(road), "the scene that was started again is freed");
        road = (SchoolRoad)prologue.Scene!;
        Check(road.Wallet.Mao == 20 && Apart(Player.Position, begin) < .01f, "she has the two yuan again and stands where she began");
        Prop("BunStall").Interact();
        // A mao more is spent on the way, so that scene 6 can tell the money it is given from the 15 mao it has of its own.
        Check(road.Wallet.Spend(1) && road.Wallet.Mao == 14, "she has 14 mao left");
        Region.Character("cat").GetNode<Interactable3D>("Feed").Interact();
        await Says("要迟到了", 30);
        await Until(() => Player.Hud.SubtitleText == "", 10, "she must have said it");
        // The line at the gate is said before the screen fades: the scene leaves the tree after its 2 s and the fade, not at once.
        speed = 1;
        ulong left = 0;
        road.TreeExited += () => left = Time.GetTicksMsec();
        var pressed = Time.GetTicksMsec();
        Prop("SchoolGate").Interact();
        await Until(() => left != 0, 20, "the scene must leave");
        Check(left - pressed > 2300, $"the line at the gate is said before the scene leaves; it left after {left - pressed} ms");
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
        Check(road.Wallet.Mao == 14 && !Player.Hud.NotebookAvailable, "she comes to the kiosk with the change of scene 2, and the notebook is still shut");
        Prop("IcePop").Interact();
        Check(road.Wallet.Mao == 11, "an ice pop costs 3 mao");
        // Backspace starts the scene again with the change she had at its start.
        await Again(() => Press(Key.Backspace), 6, 1, 2, 3, 4);
        road = (HomeRoad)prologue.Scene!;
        Check(road.Wallet.Mao == 14, "she has the change again");
        Prop("IcePop").Interact();
        Prop("Sausage").Interact();
        Check(road.Wallet.Mao == 6, "an ice pop and a sausage are paid from the change");
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
        Check(evening.CatBody.GetNode<Cat>("Cat").Held && Player.Position.X is > 3.2f and < 3.6f, "she is one step inside the front door with the cat in her arms");
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
