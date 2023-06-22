﻿using System;
using System.Runtime.CompilerServices;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Glamourer.Events;
using Glamourer.Interop.Structs;
using Penumbra.GameData.Enums;

namespace Glamourer.Interop;

public class VisorService : IDisposable
{
    public readonly VisorStateChanged Event;

    public VisorService(VisorStateChanged visorStateChanged)
    {
        Event = visorStateChanged;
        SignatureHelper.Initialise(this);
        _setupVisorHook.Enable();
    }

    public void Dispose()
        => _setupVisorHook.Dispose();

    /// <summary> Obtain the current state of the Visor for the given draw object (true: toggled). </summary>
    public unsafe bool GetVisorState(Model characterBase)
        => characterBase.IsCharacterBase && characterBase.AsCharacterBase->VisorToggled;

    /// <summary> Manually set the state of the Visor for the given draw object. </summary>
    /// <param name="human"> The draw object. </param>
    /// <param name="on"> The desired state (true: toggled). </param>
    /// <returns> Whether the state was changed. </returns>
    public unsafe bool SetVisorState(Model human, bool on)
    {
        if (!human.IsHuman)
            return false;

        var oldState = GetVisorState(human);
        Glamourer.Log.Verbose($"[SetVisorState] Invoked manually on 0x{human.Address:X} switching from {oldState} to {on}.");
        if (oldState == on)
            return false;

        SetupVisorHook(human, human.GetArmor(EquipSlot.Head).Set.Value, on);
        return true;
    }

    private delegate void UpdateVisorDelegateInternal(nint humanPtr, ushort modelId, bool on);

    [Signature(global::Penumbra.GameData.Sigs.SetupVisor, DetourName = nameof(SetupVisorDetour))]
    private readonly Hook<UpdateVisorDelegateInternal> _setupVisorHook = null!;

    private void SetupVisorDetour(nint human, ushort modelId, bool on)
    {
        var originalOn   = on;
        // Invoke an event that can change the requested value
        // and also control whether the function should be called at all.
        Event.Invoke(human, ref on);

        Glamourer.Log.Excessive(
            $"[SetVisorState] Invoked from game on 0x{human:X} switching to {on} (original {originalOn}).");

        SetupVisorHook(human, modelId, on);
    }

    /// <summary>
    /// The SetupVisor function does not set the visor state for the draw object itself,
    /// it only sets the "visor is changing" state to false.
    /// So we wrap a manual change of that flag with the function call.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void SetupVisorHook(Model human, ushort modelId, bool on)
    {
        // TODO: use client structs.
        human.AsCharacterBase->UnkFlags_01 = (byte)(on
            ? human.AsCharacterBase->UnkFlags_01 | Offsets.DrawObjectVisorStateFlag
            : human.AsCharacterBase->UnkFlags_01 & ~Offsets.DrawObjectVisorStateFlag);
        _setupVisorHook.Original(human.Address, modelId, on);
    }
}
