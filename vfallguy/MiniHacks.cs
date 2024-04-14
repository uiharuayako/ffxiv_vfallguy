using System;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Hooking;

namespace vfallguy;

public unsafe class MiniHacks
{
    private delegate long NoMoveDelegate(
        long gameobj,
        float rot,
        float length,
        long a4,
        char a5,
        int a6);
    private static Hook<NoMoveDelegate>? antiKickHook;
    public static bool EnableAntiKick = false;
    private static long AntiKickFunc(long gameobj, float rot, float length, long a4, char a5, int a6)
    {            
        if (EnableAntiKick)
            length = 0.0f;
        return antiKickHook.Original(gameobj, rot, length, a4, a5, a6);
    }

    internal static void SetAntiKick(bool isenable)
    {
        if (isenable)
        {
            antiKickHook.Enable();
        }
        else
        {
            antiKickHook.Disable();
        }
    }


    internal static void Init()
    {
        try
        {
            antiKickHook = Service.Hook.HookFromAddress<NoMoveDelegate>(
                Service.SigScanner.ScanText("48 ?? ?? 48 89 70 ?? 57 48 ?? ?? ?? ?? ?? ?? 0f 29 70 ?? 0f"), AntiKickFunc);
        }
        catch (Exception ex)
        {
            Service.Log.Error($"HackHooks Init Error: {ex}");
        }
    }
    public static void Dispose()
    {
        MoveSpeed(1);
        antiKickHook?.Dispose();
    }
    public static void Disable()
    {
        MoveSpeed(1);
        antiKickHook?.Disable();
        EnableAntiKick = false;
    }
    
    private static Lazy<IntPtr> GetSpeedPtr = new(() =>
    {
        return Service.SigScanner.TryScanText(
            "f3 ?? ?? ?? ?? ?? ?? ?? e8 ?? ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 0f ?? ?? e8 ?? ?? ?? ?? f3 ?? ?? ?? ?? ?? ?? ?? f3 ?? ?? ?? ?? ?? ?? ?? f3 ?? ?? ?? f3",
            out var num)
            ? num
            : IntPtr.Zero;
    });
    
    private delegate IntPtr SetSpeedDelegate(byte i);
    
    internal static void MoveSpeed(float speedBase)
    {
        Service.ChatGui.Print($"当前移速{speedBase}倍");
        speedBase *= 6f;
        SafeMemory.Write(
            GetSpeedPtr.Value + new IntPtr(4) + Marshal.ReadInt32(GetSpeedPtr.Value + new IntPtr(4)) +
            new IntPtr(4) + new IntPtr(20),
            speedBase);
        SetMoveControlData(speedBase);
    }
    private static void SetMoveControlData(float speed) => SafeMemory.Write(setSpeed(1) + new IntPtr(8), speed);
    
    private static IntPtr SetSpeedFunPtr
    {
        get
        {
            return Service.SigScanner.TryScanText("E8 ?? ?? ?? ?? 48 ?? ?? 74 ?? 83 ?? ?? 75 ?? 0F ?? ?? ?? 66",
                out var num)
                ? num
                : IntPtr.Zero;
        }
    }

    private static SetSpeedDelegate setSpeed =
        (SetSpeedDelegate)Marshal.GetDelegateForFunctionPointer(SetSpeedFunPtr, typeof(SetSpeedDelegate));
    
}