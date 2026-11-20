using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SonarEchoGuard {
internal static class Program {
    public const string Name="Sonar Echo Guard";
    public const string StopEvent="Local\\SonarEchoGuard.Stop";
    public const string ShowEvent="Local\\SonarEchoGuard.Show";
    public static readonly string DataPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SonarEchoGuard");
    public static void WriteJson(string path,object value) {
        var json=new JavaScriptSerializer().Serialize(value);
        File.WriteAllText(path,json,System.Text.Encoding.UTF8);
    }
    internal static void RunMta(Action action) {
        Exception error=null;
        var thread=new Thread(delegate(){try{action();}catch(Exception ex){error=ex;}});
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); thread.Join();
        if(error!=null) throw new InvalidOperationException("Audio operation failed",error);
    }
    [STAThread] private static void Main(string[] args) {
        try {
            if(args.Length==2&&args[0]=="--render-preview") {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                MenuTests.Render(args[1]);return;
            }
            if(args.Length==1 && args[0]=="--stop") {
                try {using(var ev=EventWaitHandle.OpenExisting(StopEvent)) ev.Set();} catch(WaitHandleCannotBeOpenedException){}
                return;
            }
            if(args.Length==2 && (args[0]=="--diagnose" || args[0]=="--self-test")) {
                RunMta(delegate(){
                    if(args[0]=="--diagnose") WriteJson(args[1],new AudioGuard().Scan(false,false,true));
                    else {var tests=AudioTests.Run(); WriteJson(args[1],tests); if(!tests.Passed) Environment.ExitCode=1;}
                });
                return;
            }
            bool first;
            using(var mutex=new Mutex(true,"Local\\SonarEchoGuard.SingleInstance",out first)) {
                if(!first) {
                    if(Array.IndexOf(args,"--background")<0)try {using(var ev=EventWaitHandle.OpenExisting(ShowEvent)) ev.Set();} catch(WaitHandleCannotBeOpenedException){}
                    return;
                }
                Directory.CreateDirectory(DataPath);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp(Array.IndexOf(args,"--background")<0));
            }
        } catch(Exception ex) {
            Environment.ExitCode=1;
            if(args.Length==2) {
                try {WriteJson(args[0]=="--render-preview"?Path.Combine(args[1],"Preview Error.json"):args[1],new {Error=ex.ToString()});} catch{}
            } else MessageBox.Show(ex.Message,Name,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}
internal sealed class GuardWorker : IDisposable {
    private readonly ManualResetEvent stop=new ManualResetEvent(false);
    private readonly Thread thread;
    public volatile ScanResult Latest;
    public GuardWorker() {
        thread=new Thread(Loop); thread.IsBackground=true; thread.SetApartmentState(ApartmentState.MTA); thread.Start();
    }
    private void Loop() {
        var guard=new AudioGuard(); string previous=""; DateTime lastSaved=DateTime.MinValue;
        try {
            while(!stop.WaitOne(0)) {
                var result=guard.Scan(true,false,false); Latest=result;
                string state=result.TargetEndpoints+":"+result.DiscordSessions+":"+result.Changed+":"+String.Join(" | ",result.Errors);
                if(state!=previous) {Log(state); previous=state;}
                if((DateTime.UtcNow-lastSaved).TotalSeconds>=10) {
                    try {Program.WriteJson(Path.Combine(Program.DataPath,"status.json"),result);} catch{}
                    lastSaved=DateTime.UtcNow;
                }
                if(stop.WaitOne(1000)) break;
            }
        } catch(Exception ex) {Log("Worker error: "+ex.Message);}
        finally {
            var restored=guard.Scan(false,true,false); Latest=restored;
            try {Program.WriteJson(Path.Combine(Program.DataPath,"last-stop.json"),restored);} catch{}
            Log("Stopped. Restored "+restored.Changed+" session(s) changed by this instance.");
        }
    }
    private static void Log(string message) {
        try {
            string path=Path.Combine(Program.DataPath,"guard.log");
            if(File.Exists(path) && new FileInfo(path).Length>1024*1024) File.WriteAllText(path,"");
            File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);
        } catch{}
    }
    public void Dispose() {
        stop.Set(); if(thread.Join(10000)) stop.Dispose();
    }
}
}
