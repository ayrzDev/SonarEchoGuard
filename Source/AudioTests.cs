using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace SonarEchoGuard {
public sealed class TestReport{public bool Passed;public List<string> Checks=new List<string>();public string Error;}
internal static class AudioTests {
    private static void Require(bool condition,string message,TestReport report){if(!condition)throw new InvalidOperationException(message);report.Checks.Add(message);}
    public static TestReport Run(){
        var report=new TestReport();
        try{
            Require(AudioGuard.IsDiscordName("Discord.exe")&&AudioGuard.IsDiscordName("DiscordPTB.exe")&&!AudioGuard.IsDiscordName("NotDiscord.exe"),"Exact process matching",report);
            Require(AudioGuard.IsTarget("SteelSeries Sonar - Microphone")&&!AudioGuard.IsTarget("SteelSeries Sonar - Chat")&&!AudioGuard.IsTarget("Arctis Nova 3P Wireless"),"Narrow playback target policy",report);
            using(var scope=new ComScope())using(var enumerator=NativeCall.Devices())using(var endpoints=NativeCall.Playback(enumerator)){
                NativeProbe target=null,chat=null;
                try{
                    for(uint i=0;i<NativeCall.Size(endpoints);i++)using(var endpoint=NativeCall.At(endpoints,i)){
                        string name=NativeCall.DeviceName(endpoint);
                        if(AudioGuard.IsTarget(name)&&target==null)target=NativeCall.SilentProbe(endpoint);
                        else if(name.IndexOf("Sonar - Chat",StringComparison.OrdinalIgnoreCase)>=0&&chat==null)chat=NativeCall.SilentProbe(endpoint);
                    }
                    Require(target!=null&&chat!=null,"Real Sonar playback endpoints opened through the new native backend",report);
                    target.Mute(false);chat.Mute(false);uint pid=(uint)Process.GetCurrentProcess().Id;
                    var guard=new AudioGuard(delegate(uint value){return value==pid;});
                    try{
                        var scan=guard.Scan(true,false,false);Require(scan.Errors.Count==0,"Native session enumeration and mute operations succeed",report);
                        Require(target.IsMuted(),"Targeted session is muted",report);Require(!chat.IsMuted(),"Chat session remains unchanged",report);
                        target.Mute(false);guard.Scan(true,false,false);Require(target.IsMuted(),"Reset mute is re-applied",report);
                    }finally{guard.Scan(false,true,false);}
                    Require(!target.IsMuted()&&!chat.IsMuted(),"Only owned changes are restored",report);
                    target.Mute(true);var preserve=new AudioGuard(delegate(uint value){return value==pid;});preserve.Scan(true,false,false);preserve.Scan(false,true,false);Require(target.IsMuted(),"Original user mute is preserved",report);
                    target.Mute(false);new AudioGuard(delegate(uint value){return false;}).Scan(true,false,false);Require(!target.IsMuted(),"Unrelated application is not changed",report);
                    target.Mute(true);chat.Mute(true);new AudioGuard().Scan(false,false,true);Require(target.IsMuted()&&chat.IsMuted(),"Diagnostics do not change mute states",report);
                    target.Mute(false);chat.Mute(false);
                }finally{if(chat!=null)chat.Dispose();if(target!=null)target.Dispose();}
            }
            report.Passed=true;
        }catch(Exception ex){report.Error=ex.ToString();}
        return report;
    }
}
}
