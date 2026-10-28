using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Audio;
using Game.UI;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Moves the devices the player pulls. The engine takes a device to a position at once, so each physics frame this asks for a
/// short step along the way: the motion shows, and the engine stops it at the first thing in its way, in any section. A
/// device that reaches the end of its way or is stopped thuds in the other sections that show it.
/// Decision note: 2026-10-26-device-translation
/// </summary>
/// <param name="sectionOf">The section that shows what a contact is against, null when none does.</param>
/// <param name="currentSection">The section the player is in.</param>
/// <param name="say">Called with what the player says when a device is stopped by something in its way.</param>
internal sealed class DeviceDrive(World world, IReadOnlyList<SectionScene> scenes, LensOverlay overlay,
    Region3D region, Func<IReadOnlyList<Contact>, string?> sectionOf, Func<string> currentSection, Action<string> say)
{
    // A device takes this long to travel the whole way between its limits.
    private const double TravelSeconds = 1.2;
    private const double Same = 1e-9;

    private readonly Dictionary<EntityId, double> targets = [];
    // Devices that something stopped, and the end each was heading for.
    private readonly Dictionary<EntityId, double> stalled = [];
    // Stalled devices that were pulled again and have not yet moved: if they cannot move the same way, they go the other way.
    private readonly HashSet<EntityId> retrying = [];
    private readonly Dictionary<(EntityId, string), SectionSound> thuds = [];

    internal bool IsDevice(EntityId id) => State(id).Definition is DeviceDefinition;

    /// <summary>
    /// Sets a device moving to the far end of its way, the one away from where it started. A device that is there, or already
    /// heading there, goes back to where it started. A device that something stopped goes on the way it was going, and the
    /// other way if it cannot move that way.
    /// </summary>
    internal void Pull(EntityId device)
    {
        var state = State(device);
        var (limits, initial) = Track(state);
        var far = FarEnd(limits, initial);
        if (stalled.Remove(device, out var heading))
        {
            targets[device] = heading;
            retrying.Add(device);
            return;
        }
        heading = targets.GetValueOrDefault(device, state.DevicePosition!.Value);
        targets[device] = Math.Abs(heading - far) < Same ? initial : far;
    }

    internal void Step(double delta)
    {
        foreach (var (device, target) in targets.ToArray())
        {
            var state = State(device);
            var (limits, initial) = Track(state);
            var position = state.DevicePosition!.Value;
            var reach = (limits.Max - limits.Min) / TravelSeconds * delta;
            var next = position + Math.Clamp(target - position, -reach, reach);
            var result = world.DriveDevice(device, next);
            var moved = result is DriveDeviceResult.Driven step && Math.Abs(step.Position - position) > Same;
            if (retrying.Contains(device) && !moved)
            {
                // It cannot go on: it goes the other way, unless it is already at the other end.
                var other = Math.Abs(target - FarEnd(limits, initial)) < Same ? initial : FarEnd(limits, initial);
                if (Math.Abs(other - position) > Same)
                {
                    targets[device] = other;
                    retrying.Remove(device);
                    continue;
                }
            }
            if (moved) retrying.Remove(device);
            switch (result)
            {
                case DriveDeviceResult.Driven { Contacts.Count: > 0 } stopped:
                    Stop(device, stopped.Contacts, target);
                    break;
                case DriveDeviceResult.Driven driven when Math.Abs(driven.Position - target) < Same:
                    targets.Remove(device);
                    Thud(device);
                    break;
                case DriveDeviceResult.Rejected rejected:
                    Stop(device, rejected.Failure.Contacts, target);
                    break;
            }
        }
    }

    // The end of a device's way that is farther from where it starts.
    private static double FarEnd(Interval limits, double initial) =>
        Math.Abs(limits.Max - initial) > Math.Abs(limits.Min - initial) ? limits.Max : limits.Min;

    // The limits of a device's way, and where it starts.
    private static (Interval Limits, double Initial) Track(EntityState device) => ((DeviceDefinition)device.Definition).Motion switch
    {
        DeviceRotation rotation => (rotation.Limits, rotation.Initial),
        DeviceTranslation slide => (slide.Limits, slide.Initial),
        _ => throw new NotSupportedException("Unknown device motion.")
    };

    // The device is stopped where it is on its way to `heading`, and the player sees, hears and is told why.
    private void Stop(EntityId device, IReadOnlyList<Contact> contacts, double heading)
    {
        targets.Remove(device);
        retrying.Remove(device);
        stalled[device] = heading;
        overlay.MarkContacts(contacts);
        say(RefusalLines.ForDevice(sectionOf(contacts), currentSection()));
        Thud(device);
    }

    // A muffled knock from every other section where the device has a shape, as loud as the section is near.
    private void Thud(EntityId device)
    {
        foreach (var scene in scenes)
        {
            if (scene.Id.Value == currentSection() || world.GetSliceBounds(device, scene.Id) is not SliceBoundsResult.Bounds box) continue;
            if (!thuds.TryGetValue((device, scene.Id.Value), out var thud))
            {
                thud = new SectionSound { Stream = GD.Load<AudioStream>("res://audio/thud.wav") };
                region.AddChild(thud);
                thud.Configure(region, scene.Id.Value);
                thuds.Add((device, scene.Id.Value), thud);
            }
            thud.GlobalPosition = new((float)(box.Min.X + box.Max.X) / 2, (float)(box.Min.Y + box.Max.Y) / 2, (float)(box.Min.Z + box.Max.Z) / 2);
            thud.Play();
        }
    }

    private EntityState State(EntityId id) => ((EntityReadResult.Found)world.ReadEntity(id)).Entity;
}
