using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace SonarEchoGuard {
public sealed class SessionRow{public uint Pid;public string Process,Instance;public bool Muted,Discord;}
public sealed class EndpointRow{public string Id,Name;public bool Target;public List<SessionRow> Sessions=new List<SessionRow>();}
public sealed class ScanResult{
    public string Timestamp=DateTime.UtcNow.ToString("o");public int Changed,DiscordSessions,TargetEndpoints;
    public List<string> Errors=new List<string>();public List<EndpointRow> Endpoints=new List<EndpointRow>();
}
public sealed class AudioGuard {
    private readonly HashSet<string> changed=new HashSet<string>(StringComparer.Ordinal);
    private readonly Func<uint,bool> testProcess;
    public AudioGuard(){}
    internal AudioGuard(Func<uint,bool> testProcess){this.testProcess=testProcess;}
    public static bool IsTarget(string name){var n=(name??"").ToLowerInvariant();return n.Contains("steelseries")&&n.Contains("sonar")&&n.Contains("microphone");}
    public static bool IsDiscordName(string name){
        string n=System.IO.Path.GetFileNameWithoutExtension(name??"");
        foreach(string candidate in new[]{"Discord","DiscordCanary","DiscordPTB","DiscordDevelopment"})if(String.Equals(n,candidate,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
    private static HashSet<uint> Processes(){
        var set=new HashSet<uint>();foreach(string name in new[]{"Discord","DiscordCanary","DiscordPTB","DiscordDevelopment"})
            foreach(var process in Process.GetProcessesByName(name))using(process){try{set.Add((uint)process.Id);}catch{}}
        return set;
    }
    public ScanResult Scan(bool enforce,bool restore,bool fullDiagnostics){
        var result=new ScanResult();var processes=Processes();
        try{using(var apartment=new ComScope())using(var enumerator=NativeCall.Devices())using(var devices=NativeCall.Playback(enumerator)){
            uint total=NativeCall.Size(devices);
            for(uint i=0;i<total;i++){
                try{using(var device=NativeCall.At(devices,i)){
                    var row=new EndpointRow{Id=NativeCall.ReadString(device,5),Name=NativeCall.DeviceName(device)};row.Target=IsTarget(row.Name);
                    result.Endpoints.Add(row);if(row.Target)result.TargetEndpoints++;
                    if(!row.Target&&!fullDiagnostics)continue;
                    using(var manager=NativeCall.Open(device,NativeCall.Manager))using(var sessions=NativeCall.Sessions(manager)){
                        uint count=NativeCall.Size(sessions);
                        for(uint j=0;j<count;j++)try{
                            using(var session=NativeCall.SessionAt(sessions,j))using(var info=NativeCall.Extended(session))using(var volume=NativeCall.SessionVolume(session)){
                                uint pid=NativeCall.ProcessId(info);bool discord=testProcess!=null?testProcess(pid):processes.Contains(pid);
                                var entry=new SessionRow{Pid=pid,Process="",Instance=NativeCall.Instance(info),Discord=discord,Muted=NativeCall.Muted(volume)};
                                try{using(var p=Process.GetProcessById((int)pid))entry.Process=p.ProcessName;}catch{}
                                row.Sessions.Add(entry);if(!row.Target||!discord)continue;result.DiscordSessions++;
                                string key=row.Id+"\n"+entry.Instance;
                                if(restore&&changed.Contains(key)){
                                    if(entry.Muted){NativeCall.Mute(volume,false);result.Changed++;}changed.Remove(key);entry.Muted=false;
                                }else if(enforce&&!entry.Muted){
                                    NativeCall.Mute(volume,true);if(!NativeCall.Muted(volume))throw new InvalidOperationException("Session mute was not retained");
                                    changed.Add(key);entry.Muted=true;result.Changed++;
                                }
                            }
                        }catch(Exception ex){result.Errors.Add(row.Name+": "+ex.Message);}
                    }
                }}catch(Exception ex){result.Errors.Add(ex.Message);}
            }
        }}catch(Exception ex){result.Errors.Add(ex.Message);}
        return result;
    }
}
}
