// Copyright (c) 2026 Ayrz. MIT licensed.
// Direct COM ABI implementation based on Microsoft Windows API documentation.
using System;
using System.Runtime.InteropServices;

namespace SonarEchoGuard {
internal sealed class ComScope : IDisposable {
    [DllImport("ole32.dll")]private static extern int CoInitializeEx(IntPtr reserved,uint mode);
    [DllImport("ole32.dll")]private static extern void CoUninitialize();
    public ComScope(){NativeCall.Check(CoInitializeEx(IntPtr.Zero,0));}
    public void Dispose(){CoUninitialize();}
}
internal sealed class ComPointer : IDisposable {
    public IntPtr Address{get;private set;}
    public ComPointer(IntPtr address){if(address==IntPtr.Zero)throw new InvalidOperationException("Null COM pointer");Address=address;}
    public T Method<T>(int slot) where T:class {
        if(Address==IntPtr.Zero)throw new ObjectDisposedException("COM pointer");
        var table=Marshal.ReadIntPtr(Address);var entry=Marshal.ReadIntPtr(table,slot*IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer(entry,typeof(T)) as T;
    }
    public ComPointer Query(Guid iid){IntPtr result;NativeCall.Check(Marshal.QueryInterface(Address,ref iid,out result));return new ComPointer(result);}
    public void Dispose(){var value=Address;Address=IntPtr.Zero;if(value!=IntPtr.Zero)Marshal.Release(value);}
}
internal static class NativeCall {
    public static void Check(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
    [DllImport("ole32.dll")]private static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,out IntPtr value);
    [DllImport("ole32.dll")]private static extern int PropVariantClear(ref NativeValue value);
    [StructLayout(LayoutKind.Sequential,Pack=4)]private struct Key{public Guid Format;public uint Property;}
    [StructLayout(LayoutKind.Explicit,Size=24)]private struct NativeValue{[FieldOffset(0)]public ushort Kind;[FieldOffset(8)]public IntPtr Text;}
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int Count(IntPtr self,out uint count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int Item(IntPtr self,uint index,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Enumerate(IntPtr self,int direction,uint states,out IntPtr collection);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Activate(IntPtr self,ref Guid iid,uint context,IntPtr parameters,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Store(IntPtr self,uint access,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Property(IntPtr self,ref Key key,out NativeValue value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Text(IntPtr self,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int Pointer(IntPtr self,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int Integer(IntPtr self,out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]internal delegate int SetBoolean(IntPtr self,int value,ref Guid context);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int InitAudio(IntPtr self,int mode,uint flags,long duration,long period,IntPtr format,ref Guid session);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Service(IntPtr self,ref Guid iid,out IntPtr value);
    public static readonly Guid Manager=new Guid("77aa99a0-1bd6-484f-8bc7-2c654c9a9b6f");
    private static readonly Guid Volume=new Guid("87ce5498-68d6-44e5-9215-6da47ef883d8");
    public static ComPointer Devices(){
        Guid clsid=new Guid("bcde0395-e52f-467c-8e3d-c4579291692e"),iid=new Guid("a95664d2-9614-4f35-a746-de8db63617e6");IntPtr result;
        Check(CoCreateInstance(ref clsid,IntPtr.Zero,23,ref iid,out result));return new ComPointer(result);
    }
    public static ComPointer Playback(ComPointer enumerator){IntPtr result;Check(enumerator.Method<Enumerate>(3)(enumerator.Address,0,1,out result));return new ComPointer(result);}
    public static uint Size(ComPointer collection){uint count;Check(collection.Method<Count>(3)(collection.Address,out count));return count;}
    public static ComPointer At(ComPointer collection,uint index){IntPtr value;Check(collection.Method<Item>(4)(collection.Address,index,out value));return new ComPointer(value);}
    public static string ReadString(ComPointer value,int slot){IntPtr text;Check(value.Method<Text>(slot)(value.Address,out text));try{return Marshal.PtrToStringUni(text)??"";}finally{Marshal.FreeCoTaskMem(text);}}
    public static ComPointer Open(ComPointer device,Guid iid){IntPtr value;Check(device.Method<Activate>(3)(device.Address,ref iid,23,IntPtr.Zero,out value));return new ComPointer(value);}
    public static string DeviceName(ComPointer device){
        IntPtr value;Check(device.Method<Store>(4)(device.Address,0,out value));
        using(var store=new ComPointer(value))foreach(uint property in new uint[]{14,2}){
            var key=new Key{Format=new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),Property=property};NativeValue result;
            int hr=store.Method<Property>(5)(store.Address,ref key,out result);if(hr<0)continue;
            try{if(result.Kind==31&&result.Text!=IntPtr.Zero){string name=Marshal.PtrToStringUni(result.Text);if(!String.IsNullOrWhiteSpace(name))return name;}}
            finally{PropVariantClear(ref result);}
        }
        return "Audio device";
    }
    public static ComPointer Sessions(ComPointer manager){IntPtr value;Check(manager.Method<Pointer>(5)(manager.Address,out value));return new ComPointer(value);}
    public static ComPointer SessionAt(ComPointer sessions,uint index){IntPtr value;Check(sessions.Method<Item>(4)(sessions.Address,index,out value));return new ComPointer(value);}
    public static ComPointer Extended(ComPointer session){return session.Query(new Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"));}
    public static uint ProcessId(ComPointer extended){uint pid;Check(extended.Method<Count>(14)(extended.Address,out pid));return pid;}
    public static string Instance(ComPointer extended){return ReadString(extended,13);}
    public static ComPointer SessionVolume(ComPointer session){return session.Query(Volume);}
    public static bool Muted(ComPointer volume){int result;Check(volume.Method<Integer>(6)(volume.Address,out result));return result!=0;}
    public static void Mute(ComPointer volume,bool muted){Guid context=new Guid("9d2d9104-ebbd-4abc-9f9e-e49855e57bdd");Check(volume.Method<SetBoolean>(5)(volume.Address,muted?1:0,ref context));}
    internal static NativeProbe SilentProbe(ComPointer device){
        var client=Open(device,new Guid("1cb9ad4c-dbfa-4c32-b178-c2f568a703b2"));IntPtr format=IntPtr.Zero;
        try{
            Check(client.Method<Pointer>(8)(client.Address,out format));Guid group=Guid.NewGuid();
            Check(client.Method<InitAudio>(3)(client.Address,0,0,10000000,0,format,ref group));IntPtr volume;Guid iid=Volume;
            Check(client.Method<Service>(14)(client.Address,ref iid,out volume));return new NativeProbe(client,new ComPointer(volume));
        }catch{client.Dispose();throw;}finally{if(format!=IntPtr.Zero)Marshal.FreeCoTaskMem(format);}
    }
}
internal sealed class NativeProbe:IDisposable{
    private readonly ComPointer client,volume;
    public NativeProbe(ComPointer client,ComPointer volume){this.client=client;this.volume=volume;}
    public void Mute(bool state){NativeCall.Mute(volume,state);}
    public bool IsMuted(){return NativeCall.Muted(volume);}
    public void Dispose(){volume.Dispose();client.Dispose();}
}
}
