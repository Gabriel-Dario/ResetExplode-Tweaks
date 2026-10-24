using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using ResetExplode;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var app=new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/ResetExplode;component/Theme.xaml",UriKind.Relative)});
        var project=new DirectoryInfo(AppContext.BaseDirectory);
        while(!File.Exists(Path.Combine(project.FullName,"ResetExplode.csproj"))) project=project.Parent??throw new DirectoryNotFoundException("Project root not found");
        var root=project.FullName;
        // Fixed fixture, no hardware changes or sensor driver startup.
        var h=new AdvisorHardware {Cpu="AMD Ryzen 7 7700X",Gpus=new[]{"NVIDIA GeForce RTX 4070"},SelectedGpu="NVIDIA GeForce RTX 4070",Board="ASUS TUF GAMING B650",Bios="Exemplo",WindowsBuild=22631,RamGb=32,ConfiguredMemoryMhz=6000,Portable=false,OnAc=true,SystemDriveFreePct=40};
        if(args.Contains("--inventory")) {
            h=AdvisorHardwareCollector.Collect();
            File.WriteAllText(Path.Combine(root,"work","inventory-smoke.json"),JsonSerializer.Serialize(h,new JsonSerializerOptions {WriteIndented=true}));
        }
        var view=new PerformanceAdvisorView();
        view.LoadPreview(h);
        view.Measure(new Size(1050,900));view.Arrange(new Rect(0,0,1050,900));view.UpdateLayout();
        var cards=(StackPanel)view.FindName("Cards");
        if(cards.Children.Count<10) throw new Exception("Recommendation cards missing");
        if(((Button)view.FindName("MeasureButton")).IsEnabled) throw new Exception("Preview attempted a live measurement");
        var image=new RenderTargetBitmap(1050,900,96,96,PixelFormats.Pbgra32);image.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var file=File.Create(Path.Combine(root,"work","advisor-preview.png"))) encoder.Save(file);
        var radios=Children(view).OfType<RadioButton>().ToArray();
        var balanced=radios.Single(r=>r.Tag?.ToString()=="Equilibrio"); balanced.IsChecked=true;
        if(!((TextBlock)view.FindName("RecommendationsTitle")).Text.Contains("EQUILIBRIO")) throw new Exception("Profile selection did not refresh recommendations");
        var window=new MainWindow(preview:true);
        var page=(PerformanceAdvisorView)window.FindName("PageAdvisor");
        page.LoadPreview(h);
        ((RadioButton)window.FindName("NavAdvisor")).IsChecked=true;
        if(page.Visibility!=Visibility.Visible || ((FrameworkElement)window.FindName("PageDash")).Visibility!=Visibility.Collapsed) throw new Exception("Main window navigation failed");
        var catalog=(TweakCatalogView)window.FindName("PageTweaks");
        catalog.LoadPreview(WindowsTweaks.All());
        ((RadioButton)window.FindName("NavTweaks")).IsChecked=true;
        if(catalog.Visibility!=Visibility.Visible || page.Visibility!=Visibility.Collapsed) throw new Exception("Tweak catalog navigation failed");
        var items=(ItemsControl)catalog.FindName("CatalogItems");
        if(items.Items.Count!=36) throw new Exception("Expected 36 executable tweaks in the UI");
        ((TextBox)catalog.FindName("SearchBox")).Text="suspensao seletiva";
        if(items.Items.Count!=1) throw new Exception("Accent-insensitive tweak search failed");
        ((TextBox)catalog.FindName("SearchBox")).Text="";
        var categories=(ComboBox)catalog.FindName("CategoryBox");categories.SelectedItem="Energia";
        if(items.Items.Count!=9) throw new Exception("Energy filter should expose 8 new actions and the original plan action");
        categories.SelectedIndex=0;
        if(((Button)catalog.FindName("ApplySelectedButton")).IsEnabled) throw new Exception("Catalog preview permits mutation");
        var surface=(FrameworkElement)window.Content;
        window.Content=null; // offscreen rendering without an HWND or live hardware startup
        surface.Measure(new Size(1320,840));surface.Arrange(new Rect(0,0,1320,840));surface.UpdateLayout();
        var fullImage=new RenderTargetBitmap(1320,840,96,96,PixelFormats.Pbgra32);fullImage.Render(surface);
        var pixels=new byte[1320*840*4];fullImage.CopyPixels(pixels,1320*4,0);
        if(pixels.Where((_,i)=>i%4!=3).Count(v=>v>30)<5000) throw new Exception("Main window rendered a blank image");
        var fullEncoder=new PngBitmapEncoder();fullEncoder.Frames.Add(BitmapFrame.Create(fullImage));
        using(var file=File.Create(Path.Combine(root,"work","tweaks-main-window.png"))) fullEncoder.Save(file);
        // Exercise live capability/state reads only. No Write/Apply/Restore call is permitted here.
        int supported=0;
        foreach(var tweak in TweakCatalog.Create(new WindowsTweakStore(),Environment.OSVersion.Version.Build))
            if(tweak.Supported()) { tweak.Refresh();supported++; }
        Console.WriteLine($"PASS native read-only capability probes: {supported}/28 new actions available on this PC");
        Console.WriteLine("PASS 36-action WPF catalog, navigation, text/category filters and disabled preview actions");
        window.Close();
        Console.WriteLine("PASS main window construction and advisor navigation without driver/sensor startup");
        Console.WriteLine($"PASS WPF load, {cards.Children.Count} cards, preview safety, profile interaction, PNG rendering" );
        app.Shutdown();
    }
    static IEnumerable<DependencyObject> Children(DependencyObject node) {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) {
            var child=VisualTreeHelper.GetChild(node,i);yield return child;
            foreach(var next in Children(child)) yield return next;
        }
    }
}
