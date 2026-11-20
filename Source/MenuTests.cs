using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace SonarEchoGuard {
internal static class MenuTests {
    private static void Save(CompactMenu menu,string path){
        menu.Root.AutoClose=false;menu.Root.Show(Point.Empty);menu.Root.Location=new Point(-10000,-10000);Application.DoEvents();menu.Root.PerformLayout();
        using(var bitmap=new Bitmap(menu.Root.Width,menu.Root.Height)){menu.Root.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path,ImageFormat.Png);}
    }
    public static void Render(string output){
        Directory.CreateDirectory(output);var preference=Path.Combine(output,"test-preferences.json");var language=new UiLanguage(preference);language.Set("tr");
        bool enabled=true,startup=true;ScanResult status=null;Program.RunMta(delegate{status=new AudioGuard().Scan(false,false,true);});var checks=new List<string>();
        using(var menu=new CompactMenu(language,delegate{return enabled;},delegate(bool value){enabled=value;},delegate{return startup;},delegate(bool value){startup=value;},delegate{return status;},delegate{},delegate{})){
            if(menu.Protection.Text!="Koruma etkin")throw new Exception("Turkish menu text failed");checks.Add("Turkish menu labels");Save(menu,Path.Combine(output,"Menu-TR.png"));
            menu.Protection.PerformClick();if(enabled)throw new Exception("Protection toggle failed");checks.Add("Protection control calls controller");menu.Protection.PerformClick();if(!enabled)throw new Exception("Protection resume failed");checks.Add("Protection resumes");
            menu.Startup.PerformClick();if(startup)throw new Exception("Startup control failed");checks.Add("Startup controller updates without touching registry");
            menu.English.PerformClick();if(language.Code!="en"||menu.Protection.Text!="Protection enabled")throw new Exception("English translation failed");checks.Add("English menu labels");
            if(new UiLanguage(preference).Code!="en")throw new Exception("Language preference did not persist");checks.Add("Language selection survives reload");Save(menu,Path.Combine(output,"Menu-EN.png"));
            if(menu.Root.Width>380||menu.Root.Height>390)throw new Exception("Menu exceeds compact bounds");checks.Add("Compact menu bounds");
        }
        Program.WriteJson(Path.Combine(output,"Menu Test Results.json"),new{Passed=true,Checks=checks});
    }
}
}
