using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SonarEchoGuard {
internal sealed class UiLanguage {
    private readonly string path;public string Code{get;private set;}
    public UiLanguage(string path){
        this.path=path;Code=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="tr"?"tr":"en";
        try{if(File.Exists(path)){var saved=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path));if(saved["Language"]=="tr"||saved["Language"]=="en")Code=saved["Language"];}}catch{}
    }
    public void Set(string value){if(value!="tr"&&value!="en")throw new ArgumentException("Unsupported language");Code=value;Directory.CreateDirectory(Path.GetDirectoryName(path));Program.WriteJson(path,new{Language=Code});}
    public string Text(string english,string turkish){return Code=="tr"?turkish:english;}
}
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer {
    internal static readonly Color Background=Color.FromArgb(31,31,31),Foreground=Color.FromArgb(235,235,235),Muted=Color.FromArgb(160,160,160),Accent=Color.FromArgb(102,218,184);
    public DarkMenuRenderer(){RoundedEdges=true;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Background);}
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){using(var b=new SolidBrush(Background))e.Graphics.FillRectangle(b,e.AffectedBounds);}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){
        if(e.Item.Selected&&e.Item.Enabled)using(var b=new SolidBrush(Color.FromArgb(54,54,54)))e.Graphics.FillRectangle(b,new Rectangle(4,0,e.Item.Width-8,e.Item.Height));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
        e.TextColor=(e.Item.Tag as string)=="status"?Accent:e.Item.Enabled?Foreground:(e.Item.Tag as string)=="header"?Foreground:Muted;
        TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,e.TextRectangle,e.TextColor,e.TextFormat);
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r=e.ImageRectangle;using(var pen=new Pen(Accent,1.8f)){e.Graphics.DrawLines(pen,new[]{new Point(r.Left+2,r.Top+7),new Point(r.Left+6,r.Top+11),new Point(r.Left+13,r.Top+3)});}
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(Color.FromArgb(68,68,68)))e.Graphics.DrawLine(pen,10,e.Item.Height/2,e.Item.Width-10,e.Item.Height/2);}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var pen=new Pen(Color.FromArgb(54,54,54)))e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){e.ArrowColor=Muted;base.OnRenderArrow(e);}
}
internal sealed class CompactMenu : IDisposable {
    public readonly ContextMenuStrip Root;
    public readonly ToolStripMenuItem Protection,Startup,English,Turkish,Diagnostics;
    private readonly ToolStripMenuItem status,languageItem,logs,exit;
    private readonly UiLanguage language;private readonly Func<bool> protection,startup;private readonly Func<ScanResult> latest;
    private readonly Action<bool> setProtection,setStartup;private readonly Action diagnose,quit;
    private bool updating;
    public CompactMenu(UiLanguage language,Func<bool> protection,Action<bool> setProtection,Func<bool> startup,Action<bool> setStartup,Func<ScanResult> latest,Action diagnose,Action quit){
        this.language=language;this.protection=protection;this.setProtection=setProtection;this.startup=startup;this.setStartup=setStartup;this.latest=latest;this.diagnose=diagnose;this.quit=quit;
        Root=new ContextMenuStrip{Renderer=new DarkMenuRenderer(),ShowImageMargin=false,ShowCheckMargin=true,BackColor=DarkMenuRenderer.Background,ForeColor=DarkMenuRenderer.Foreground,Font=new Font("Segoe UI",9.25f),Padding=new Padding(5,7,5,7),MinimumSize=new Size(268,0)};
        var title=new ToolStripMenuItem("Sonar Echo Guard"){Enabled=false,Tag="header",Font=new Font("Segoe UI",9.25f,FontStyle.Bold)};
        status=new ToolStripMenuItem{Enabled=false,Tag="status",Font=new Font("Segoe UI",8.5f)};
        Protection=new ToolStripMenuItem{CheckOnClick=true};Startup=new ToolStripMenuItem{CheckOnClick=true};languageItem=new ToolStripMenuItem("Language / Dil");
        English=new ToolStripMenuItem("English");Turkish=new ToolStripMenuItem("Türkçe");languageItem.DropDownItems.Add(English);languageItem.DropDownItems.Add(Turkish);
        languageItem.DropDown.Renderer=Root.Renderer;languageItem.DropDown.BackColor=DarkMenuRenderer.Background;languageItem.DropDown.ForeColor=DarkMenuRenderer.Foreground;
        Diagnostics=new ToolStripMenuItem();logs=new ToolStripMenuItem();exit=new ToolStripMenuItem();
        Root.Items.AddRange(new ToolStripItem[]{title,status,new ToolStripSeparator(),Protection,Startup,languageItem,new ToolStripSeparator(),Diagnostics,logs,new ToolStripSeparator(),exit});
        foreach(ToolStripItem item in Root.Items)item.Padding=new Padding(3,4,9,4);
        Protection.Click+=delegate{if(!updating){this.setProtection(Protection.Checked);Refresh();}};
        Startup.Click+=delegate{if(!updating){this.setStartup(Startup.Checked);Refresh();}};
        English.Click+=delegate{language.Set("en");Refresh();};Turkish.Click+=delegate{language.Set("tr");Refresh();};
        Diagnostics.Click+=delegate{this.diagnose();};logs.Click+=delegate{Process.Start("explorer.exe",Program.DataPath);};exit.Click+=delegate{this.quit();};
        Root.Opening+=delegate{Refresh();};Refresh();
    }
    public void Refresh(){
        updating=true;try{Protection.Checked=protection();Startup.Checked=startup();}finally{updating=false;}
        Protection.Text=language.Text("Protection enabled","Koruma etkin");Startup.Text=language.Text("Start with Windows","Windows ile başlat");
        Diagnostics.Text=language.Text("Audio diagnostics…","Ses tanılama…");logs.Text=language.Text("Open logs","Günlükleri aç");exit.Text=language.Text("Exit","Çıkış");
        English.Checked=language.Code=="en";Turkish.Checked=language.Code=="tr";
        var scan=latest();
        status.Text=!protection()?language.Text("Protection paused","Koruma duraklatıldı"):scan==null?language.Text("Starting…","Başlatılıyor…"):
            scan.Errors.Count>0?language.Text("Audio needs attention","Ses ayarlarını kontrol et"):
            scan.DiscordSessions>0?language.Text("Active · ","Etkin · ")+scan.DiscordSessions+language.Text(scan.DiscordSessions==1?" session":" sessions"," oturum korunuyor"):
            language.Text("Ready · waiting for Discord","Hazır · Discord bekleniyor");
    }
    public void Show(){Refresh();Root.Show(Cursor.Position);}
    public void Dispose(){Root.Dispose();}
}
internal sealed class DiagnosticWindow : Form {
    private readonly UiLanguage language;private readonly ListView list;private readonly Button refresh,close;
    public DiagnosticWindow(UiLanguage language){
        this.language=language;Size=new Size(590,350);MinimumSize=Size;MaximumSize=Size;StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        BackColor=DarkMenuRenderer.Background;ForeColor=DarkMenuRenderer.Foreground;Font=new Font("Segoe UI",9);
        list=new ListView{Location=new Point(12,12),Size=new Size(550,245),View=View.Details,FullRowSelect=true,GridLines=false,BackColor=Color.FromArgb(37,37,37),ForeColor=DarkMenuRenderer.Foreground,BorderStyle=BorderStyle.FixedSingle};
        list.Columns.Add("",360);list.Columns.Add("",65);list.Columns.Add("",115);Controls.Add(list);
        refresh=new Button{Location=new Point(350,270),Size=new Size(100,30),FlatStyle=FlatStyle.Flat};close=new Button{Location=new Point(462,270),Size=new Size(100,30),FlatStyle=FlatStyle.Flat};Controls.Add(refresh);Controls.Add(close);
        refresh.Click+=delegate{LoadRows();};close.Click+=delegate{Close();};ApplyLanguage();Shown+=delegate{LoadRows();};
    }
    public void ApplyLanguage(){Text=language.Text("Audio diagnostics","Ses tanılama");list.Columns[0].Text=language.Text("Output","Çıkış aygıtı");list.Columns[1].Text="Discord";list.Columns[2].Text=language.Text("State","Durum");refresh.Text=language.Text("Refresh","Yenile");close.Text=language.Text("Close","Kapat");}
    private void LoadRows(){
        ScanResult report=null;Program.RunMta(delegate{report=new AudioGuard().Scan(false,false,true);});Program.WriteJson(Path.Combine(Program.DataPath,"diagnostics.json"),report);
        list.BeginUpdate();list.Items.Clear();foreach(var endpoint in report.Endpoints){int count=0,muted=0;foreach(var session in endpoint.Sessions)if(session.Discord){count++;if(session.Muted)muted++;}
            var row=new ListViewItem(endpoint.Name);row.SubItems.Add(count.ToString());row.SubItems.Add(endpoint.Target?(count>0&&muted==count?language.Text("Muted","Susturuldu"):language.Text("Waiting","Bekleniyor")):language.Text("Unchanged","Değişmedi"));if(endpoint.Target)row.ForeColor=DarkMenuRenderer.Accent;list.Items.Add(row);}
        list.EndUpdate();
    }
}
internal sealed class TrayApp : ApplicationContext {
    private readonly NotifyIcon tray;private readonly CompactMenu menu;private readonly UiLanguage language;
    private readonly System.Windows.Forms.Timer timer;private readonly System.Threading.EventWaitHandle stopEvent,showEvent;
    private GuardWorker worker;private DiagnosticWindow diagnostics;private bool exiting;
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public TrayApp(bool show){
        language=new UiLanguage(Path.Combine(Program.DataPath,"preferences.json"));
        stopEvent=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,Program.StopEvent);
        showEvent=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,Program.ShowEvent);
        menu=new CompactMenu(language,delegate{return worker!=null;},SetProtection,Startup,SetStartup,delegate{return worker==null?null:worker.Latest;},ShowDiagnostics,delegate{ExitThread();});
        tray=new NotifyIcon{Icon=(Icon)SystemIcons.Shield.Clone(),Text=Program.Name,Visible=true,ContextMenuStrip=menu.Root};
        tray.MouseClick+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left)menu.Show();};worker=new GuardWorker();
        timer=new System.Windows.Forms.Timer{Interval=500};timer.Tick+=delegate{if(stopEvent.WaitOne(0)){ExitThread();return;}if(showEvent.WaitOne(0))menu.Show();if(menu.Root.Visible)menu.Refresh();};timer.Start();
        Application.ApplicationExit+=OnExit;if(show)menu.Show();
    }
    private bool Startup(){using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&key.GetValue("SonarEchoGuard")!=null;}
    private void SetStartup(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){if(enabled)key.SetValue("SonarEchoGuard","\""+Application.ExecutablePath+"\" --background");else key.DeleteValue("SonarEchoGuard",false);}}
    private void SetProtection(bool enabled){if(enabled&&worker==null)worker=new GuardWorker();else if(!enabled&&worker!=null){worker.Dispose();worker=null;}}
    private void ShowDiagnostics(){if(diagnostics==null||diagnostics.IsDisposed)diagnostics=new DiagnosticWindow(language);diagnostics.ApplyLanguage();diagnostics.Show();diagnostics.Activate();}
    private void OnExit(object sender,EventArgs e){Cleanup();}
    private void Cleanup(){if(exiting)return;exiting=true;timer.Stop();timer.Dispose();if(diagnostics!=null)diagnostics.Dispose();if(worker!=null){worker.Dispose();worker=null;}tray.Visible=false;tray.Icon.Dispose();tray.Dispose();menu.Dispose();stopEvent.Dispose();showEvent.Dispose();Application.ApplicationExit-=OnExit;}
    protected override void ExitThreadCore(){Cleanup();base.ExitThreadCore();}
}
}
