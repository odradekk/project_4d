using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Audio;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Moves the devices the player pulls. The engine takes a device to a position at once, so each physics frame this asks for a
/// short step along the way: the motion shows, and the engine stops it at the first thing in its way, in any section. A
/// device that reaches the end of its way or is stopped thuds in the other sections that show it.
/// Decision note: 2026-10-26-device-translation
/// </summary>
internal sealed class DeviceDrive(World world, IReadOnlyList<SectionScene> scenes, DynamicSlices slices, LensOverlay overlay,
    PlayerActions hands, Region3D region, Action<Refusal> blocked)
{
    // A device takes this long to travel the whole way between its limits.
    private const double TravelSeconds = 1.2;
    private const double Same = 1e-9;

    private readonly Dictionary<EntityId, double> targets = [];
    private readonly Dictionary<(EntityId, string), SectionSound> thuds = [];

    internal bool IsDevice(EntityId id) => State(id).Definition is DeviceDefinition;

    /// <summary>
    /// Sets a device moving to the far end of its way, the one away from where it started; a device that is there, or that is
    /// already heading there, goes back to where it started.
    /// </summary>
    internal void Pull(EntityId device)
    {
        var state = State(device);
        var (limits, initial) = Track(state);
        var far = Math.Abs(limits.Max - initial) > Math.Abs(limits.Min - initial) ? limits.Max : limits.Min;
        var heading = targets.GetValueOrDefault(device, state.DevicePosition!.Value);
        targets[device] = Math.Abs(heading - far) < Same ? initial : far;
    }

    internal void Step(double delta)
    {
        foreach (var (device, target) in targets.ToArray())
        {
            var state = State(device);
            var limits = Track(state).Limits;
            var position = state.DevicePosition!.Value;
            var reach = (limits.Max - limits.Min) / TravelSeconds * delta;
            var next = position + Math.Clamp(target - position, -reach, reach);
            switch (world.DriveDevice(device, next))
            {
                case DriveDeviceResult.Driven { Contacts.Count: > 0 } stopped:
                    Stop(device, stopped.Contacts, FailureCode.TranslationBlocked);
                    Thud(device);
                    break;
                case DriveDeviceResult.Driven driven when Math.Abs(driven.Position - target) < Same:
                    targets.Remove(device);
                    Thud(device);
                    break;
                case DriveDeviceResult.Rejected rejected:
                    Stop(device, rejected.Failure.Contacts, rejected.Failure.Code);
                    break;
            }
        }
    }

    // The limits of a device's way, and where it starts.
    private static (Interval Limits, double Initial) Track(EntityState device) => ((DeviceDefinition)device.Definition).Motion switch
    {
        DeviceRotation rotation => (rotation.Limits, rotation.Initial),
        DeviceTranslation slide => (slide.Limits, slide.Initial),
        _ => throw new NotSupportedException("Unknown device motion.")
    };

    // The device is stopped where it is, and the player hears and sees why.
    private void Stop(EntityId device, IReadOnlyList<Contact> contacts, FailureCode code)
    {
        targets.Remove(device);
        overlay.MarkContacts(contacts);
        blocked(hands.Refuse(new Failure(code, "A device is in the way.", device, null, contacts)));
    }

    // A muffled knock from every other section where the device has a shape, as loud as the section is near.
    private void Thud(EntityId device)
    {
        foreach (var scene in scenes)
        {
            if (scene.Id.Value == hands.CurrentSection || slices.BoundsOf(scene.Id, device) is not { } box) continue;
            if (!thuds.TryGetValue((device, scene.Id.Value), out var thud))
            {
                thud = new SectionSound { Stream = GD.Load<AudioStream>("res://audio/thud.wav") };
                hands.AddChild(thud);
                thud.Configure(region, scene.Id.Value);
                thuds.Add((device, scene.Id.Value), thud);
            }
            thud.GlobalPosition = box.GetCenter();
            thud.Play();
        }
    }

    private EntityState State(EntityId id) => ((EntityReadResult.Found)world.ReadEntity(id)).Entity;
}
