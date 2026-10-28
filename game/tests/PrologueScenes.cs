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
// the school scenes are driven through the engine as the keys do.
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

    private async Task<Wallet> PlaySchoolRoad()
    {
        await Arrive(2);
        var road = (SchoolRoad)prologue.Scene!;
        Check(road.Wallet.Mao == 20 && Player.Position.Y > 2.9f, "the two yuan of the table are in her pocket, and she stands on the landing");
        // A checkpoint made by the flow puts her back where she began.
        var begin = Player.Position;
        Player.Position += new Vector3(3, 0, 0);
        Check(road.GetChildren().OfType<SceneCheckpoint>().Single().Restart() && Apart(Player.Position, begin) < .01f, "the checkpoint of scene 2 puts her back at the start");
        Prop("BunStall").Interact();
        Check(road.Wallet.Mao == 15, "the bun costs 5 mao");
        Region.Character("cat").GetNode<Interactable3D>("Feed").Interact();
        await Says("要迟到了", 30);
        Prop("SchoolGate").Interact();
        return road.Wallet;
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

    private async Task PlayRecess(Region3D schoolRegion)
    {
        await Arrive(4, 1);
        Check(Region == schoolRegion, "scene 4 plays in the region of scene 3");
        await Until(() => school.Room.Classmates.All(person => !person.Visible), 40, "the class must leave");
        await school.DoTheTricks();
        await Until(() => !Player.Seated, 30, "she must be free to go to the window");
        await school.Turn(new StandUp(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil), new("Ⅱ")));
    }

    private async Task PlayDuty(Region3D schoolRegion)
    {
        await Arrive(5, 1, 2, 3);
        var room = school.Room;
        Check(Region == schoolRegion && room.IsUpright(SchoolRoom.WindowPencil), "scene 5 plays in the same region, and the pencil that scene 4 stood in the rail still stands");
        await Until(() => room.Broom.CollisionLayer != 0, 40, "the chores must start");
        room.Broom.Interact();
        room.Chalkboard.Interact();
        foreach (var row in room.RowBoxes) row.Interact();
        await school.Turn(new LayDown(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil)));
        foreach (var window in SchoolRoom.Windows) Region.World!.DriveDevice(new(window), .69);
        await Until(() => Player.Hud.UnlockedPages.Contains(4), 30, "the page of the glowing things must be drawn");
        room.DoorLock.Interact();
    }

    private async Task PlayHomeRoad(Wallet wallet, Region3D schoolRegion)
    {
        await Arrive(6, 1, 2, 3, 4);
        var road = (HomeRoad)prologue.Scene!;
        Check(!GodotObject.IsInstanceValid(schoolRegion), "the school region is gone");
        Check(road.Wallet == wallet && wallet.Mao == 15 && !Player.Hud.NotebookAvailable, "she comes to the kiosk with the change of the bun, and the notebook is still shut");
        Prop("IcePop").Interact();
        Prop("Sausage").Interact();
        Check(wallet.Mao == 7, "an ice pop and a sausage are paid from the change");
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
        Check(Apart(evening.CatBody.Position, Player.Position) < 1.5f && !evening.CatBody.GetNode<Cat>("Cat").Held, "the cat she carried in stands beside her");
        foreach (var name in new[] { "Note", "HairClip", "Tv", "Stove" }) Prop(name).Interact();
        await Until(() => evening.Heated, 6, "the meal must be hot");
        Prop("Sausage").Interact();
        await Until(() => evening.CatFed, 20, "the cat must come for its half");
        Prop("Desk").Interact();
    }
}
