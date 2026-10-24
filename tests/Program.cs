using ResetExplode;

if(args.Contains("--exit-child")) { Console.Error.Write("expected failure");Environment.Exit(7); }
if(args.Contains("--timeout-child")) {Thread.Sleep(10000);return;}

int passed = 0;
void Check(string name, Action test) {
    try { test(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); Environment.ExitCode = 1; }
}
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

Check("partial restore preserves failed and unknown backups", () => {
    var settings = new AppSettings();
    settings.TweakBackup["success"] = "original";
    settings.TweakBackup["broken.a"] = "original-a";
    settings.TweakBackup["broken.b"] = "original-b";
    settings.TweakBackup["future"] = "unknown";
    var ok = new Tweak { Id = "success", Restore = _ => { } };
    var broken = new Tweak { Id = "broken", Restore = _ => throw new IOException("simulated access denied") };
    WindowsTweaks.RestoreAll(new[] { ok, broken }, settings);
    Assert(!settings.TweakBackup.ContainsKey("success"), "successful action still has backup");
    Assert(settings.TweakBackup.ContainsKey("broken.a") && settings.TweakBackup.ContainsKey("broken.b"), "failed action lost recovery state");
    Assert(settings.TweakBackup.ContainsKey("future"), "unrecognized backup was discarded");
});

Check("save failure preserves successful restore recovery copy", () => {
    var s = new AppSettings { FailSave = true }; s.TweakBackup["a"] = "old";
    Assert(!WindowsTweaks.RestoreOne(new Tweak { Id = "a" }, s), "failure reported as success");
    Assert(s.TweakBackup["a"] == "old", "save failure lost backup");
});
Check("apply verifies the observed result", () => {
    var s = new AppSettings();
    bool rejected = false;
    try { WindowsTweaks.Apply(new Tweak { Read = () => false }, true, s); } catch (IOException) { rejected = true; }
    Assert(rejected, "ignored setting reported as applied");
});
Check("unsupported action cannot call writer", () => {
    bool called = false;
    try { WindowsTweaks.Apply(new Tweak { Supported = () => false, Write = (_, _) => called = true }, true, new()); }
    catch (NotSupportedException) { }
    Assert(!called, "incompatible action wrote to system");
});
Check("battery and unknown power never recommend high performance", () => {
    foreach (var h in new[] { new AdvisorHardware { OnAc = false, Portable = true }, new AdvisorHardware() })
        Assert(!PerformanceAdvisor.Recommend(h, AdvisorProfile.Jogos, null).Any(r => r.Target == "power"), "unsafe power recommendation");
});
Check("Windows 10 excludes windowed optimization action", () => {
    Assert(!PerformanceAdvisor.Recommend(new() { WindowsBuild = 19045 }, AdvisorProfile.Jogos, null).Any(r => r.Id == "windowed"), "Win11-only feature offered");
    Assert(PerformanceAdvisor.Recommend(new() { WindowsBuild = 22631 }, AdvisorProfile.Jogos, null).Any(r => r.Id == "windowed"), "compatible feature missing");
});
Check("balanced profile does not force gaming or high power", () => {
    var actions = PerformanceAdvisor.Recommend(new() { WindowsBuild = 22631, OnAc = true, Portable = false }, AdvisorProfile.Equilibrio, null);
    Assert(!actions.Any(r => r.Kind == AdvisorActionKind.Tweak && r.Target is "power" or "gamemode" or "gamedvr"), "balanced profile forces gaming settings");
});
Check("missing sensors remain unknown", () => {
    var m = AdvisorMeasurement.From(new(), AdvisorProfile.Jogos, "test", Enumerable.Repeat(new AdvisorReading(null,null,null,null,null,null,false,null,null),30).ToArray(),30);
    Assert(m.Cpu == null && m.Gpu == null && m.Fps == null && m.CpuTempMax == null, "missing sensor turned into zero");
});
Check("mixed processes cannot produce comparable FPS", () => {
    var rows = Enumerable.Range(0,30).Select(i => new AdvisorReading(30,50,40,60,55,40,false,100,i<15?"game1":"game2")).ToArray();
    var m = AdvisorMeasurement.From(new(), AdvisorProfile.Jogos,"test",rows,30);
    Assert(m.Process == null && m.Fps == null, "mixed workloads aggregated");
});
Check("comparison rejects changed workload and pending restart", () => {
    var a = new AdvisorMeasurement { Samples=30,DurationSeconds=30,Hardware="pc",Workload="game 1080p",Profile=AdvisorProfile.Jogos,FrameSamples=30,Fps=100,Process="game",FpsVariationPct=1,OnAc=true };
    Assert(PerformanceAdvisor.Compare(a,a with {Workload="game 4k"}).StartsWith("Sessões diferentes"), "unmatched scenario compared");
    Assert(PerformanceAdvisor.Compare(a,a with {RestartPending=true}).StartsWith("Conclua"), "pending reboot ignored");
    Assert(PerformanceAdvisor.Compare(a,a with {Fps=101}).Contains("variação medida"), "noise called a gain");
});
Check("stale hardware measurements do not guide new hardware", () => {
    var r = PerformanceAdvisor.Recommend(new() { OnAc=true,Portable=false },AdvisorProfile.Jogos,new() {Hardware="different pc",Thermal=true,Ram=99});
    Assert(!r.Any(a=>a.Id=="thermal"||a.Id=="memory"),"stale data reused");
});
Check("existing and absent settings round-trip without losing first backup",()=>{
    foreach(string? original in new string?[]{"99",null}) {
        string? state=original; var backup=new Dictionary<string,string?>();
        int persists=0;
        void Save(){persists++;Assert(state==original,"write occurred before original state was persisted");}
        TweakValueBackup.Write(backup,"setting",()=>state,v=>state=v,Save,"1");
        TweakValueBackup.Write(backup,"setting",()=>state,v=>state=v,Save,"2");
        Assert(persists==1 && backup["setting"]==original,"first snapshot replaced by second change");
        TweakValueBackup.Restore(backup,"setting",()=>state,v=>state=v);
        Assert(state==original,"original value/existence not restored");
    }
});
Check("failed persistence prevents the system write",()=>{
    bool written=false;
    try {TweakValueBackup.Write(new Dictionary<string,string?>(),"key",()=>"original",_=>written=true,()=>throw new IOException("disk full"),"new");}
    catch(IOException) { }
    Assert(!written,"system changed without durable backup");
});
Check("retry after failed persistence must save before writing",()=>{
    var backup=new Dictionary<string,string?>();bool saved=false,wrote=false;
    try {TweakValueBackup.Write(backup,"key",()=>"original",_=>wrote=true,()=>throw new IOException(),"first");} catch(IOException) { }
    TweakValueBackup.Write(backup,"key",()=>"original",_=>{Assert(saved,"retry bypassed persistence");wrote=true;},()=>saved=true,"second");
    Assert(wrote && backup["key"]=="original","retry lost original");
});
Check("partial multi-value restore skips a value never written",()=>{
    var s=new AppSettings();s.TweakBackup["group.a"]="original";
    string? state="changed";
    var tweak=new Tweak {Id="group",Restore=settings=>{
        TweakValueBackup.Restore(settings.TweakBackup,"group.a",()=>state,v=>state=v);
        TweakValueBackup.Restore(settings.TweakBackup,"group.b",()=>null,_=>throw new Exception("untouched setting was written"));
    }};
    Assert(WindowsTweaks.RestoreOne(tweak,s) && state=="original" && s.TweakBackup.Count==0,"partial restore cannot complete");
});
Check("nonzero command exit cannot report success",()=>{
    bool rejected=false;
    try {CheckedSystemCommand.Run(Environment.ProcessPath!,"--exit-child");} catch(IOException) {rejected=true;}
    Assert(rejected,"failed command accepted");
});
Check("command timeout is bounded and rejected",()=>{
    var timer=System.Diagnostics.Stopwatch.StartNew();bool rejected=false;
    try {CheckedSystemCommand.Run(Environment.ProcessPath!,"--timeout-child",300);} catch(TimeoutException) {rejected=true;}
    Assert(rejected && timer.Elapsed.TotalSeconds<5,"timeout did not stop the child");
});
Check("expanded executable catalog has at least 35 distinct actions",()=>{
    var catalog=WindowsTweaks.All();
    Assert(catalog.Count>=35,"expected an executable catalog, found "+catalog.Count);
    Assert(catalog.Select(t=>t.Id).Distinct().Count()==catalog.Count,"duplicate action identifiers");
});
Check("every new catalog action applies toggles and restores its exact snapshot",()=>{
    var store=new FakeTweakStore();var catalog=TweakCatalog.Create(store,26100);
    foreach(var tweak in catalog) {
        var settings=new AppSettings();
        store.BeforeWrite=()=>Assert(settings.Saves>0,"system write preceded backup persistence: "+tweak.Id);
        WindowsTweaks.Apply(tweak,true,settings);
        string? first=settings.TweakBackup[tweak.Id];
        WindowsTweaks.Apply(tweak,false,settings);
        Assert(settings.TweakBackup[tweak.Id]==first,"toggle replaced original: "+tweak.Id);
        Assert(WindowsTweaks.RestoreOne(tweak,settings),"restore failed: "+tweak.Id);
        Assert(store.IsOriginal(tweak.Id,"plan-a"),"original state/existence lost: "+tweak.Id);
        Assert(settings.TweakBackup.Count==0,"backup not cleared after verified restoration");
    }
});
Check("new catalog cannot mutate when persistence fails",()=>{
    var store=new FakeTweakStore();var settings=new AppSettings {FailSave=true};
    foreach(var tweak in TweakCatalog.Create(store,26100)) {
        try {WindowsTweaks.Apply(tweak,true,settings);throw new Exception("persistence failure ignored");} catch(IOException) { }
    }
    Assert(store.Writes==0 && settings.TweakBackup.Count==0,"unprotected write or stale backup");
});
Check("restoring power settings targets original plan without activating it",()=>{
    var store=new FakeTweakStore();var settings=new AppSettings();
    var action=TweakCatalog.Create(store,26100).Single(t=>t.Id=="cpu-epp");
    WindowsTweaks.Apply(action,true,settings);store.Plan="plan-b";
    Assert(WindowsTweaks.RestoreOne(action,settings),"restore original power plan failed");
    Assert(store.Plan=="plan-b" && store.IsOriginal("cpu-epp","plan-a"),"restore activated or lost original plan");
    Assert(store.IsOriginal("cpu-epp","plan-b"),"unrelated power plan was changed");
});
Check("changing plans cannot overwrite a previous power snapshot",()=>{
    var store=new FakeTweakStore();var settings=new AppSettings();
    var action=TweakCatalog.Create(store,26100).Single(t=>t.Id=="cpu-epp");
    WindowsTweaks.Apply(action,true,settings);string? original=settings.TweakBackup[action.Id];
    store.Plan="plan-b";bool blocked=false;
    try {WindowsTweaks.Apply(action,true,settings);} catch(InvalidOperationException) {blocked=true;}
    Assert(blocked && store.Writes==1 && settings.TweakBackup[action.Id]==original,"plan snapshot lost or another plan mutated");
});
Check("new catalog unsupported and failed restorations preserve recovery",()=>{
    var store=new FakeTweakStore();var settings=new AppSettings();
    var action=TweakCatalog.Create(store,26100).First();
    WindowsTweaks.Apply(action,true,settings);store.FailWrite=true;
    Assert(!WindowsTweaks.RestoreOne(action,settings) && settings.TweakBackup.ContainsKey(action.Id),"failed restore destroyed backup");
    store.FailWrite=false;Assert(WindowsTweaks.RestoreOne(action,settings),"retry cannot restore");
    store.Supported=false;int before=store.Writes;
    try {WindowsTweaks.Apply(action,true,settings);throw new Exception("unsupported write allowed");} catch(NotSupportedException) { }
    Assert(store.Writes==before,"unsupported adapter was mutated");
});
Console.WriteLine($"{passed} checks passed");

namespace ResetExplode {
    public sealed class FakeTweakStore : ITweakStore {
        private readonly Dictionary<(string Id,string Scope),string?> _values=new();
        private readonly Dictionary<string,SettingKind> _kinds=new();
        public string Plan="plan-a";
        public bool Supported=true,FailWrite;
        public int Writes;
        public Action? BeforeWrite;
        public bool Supports(TweakSetting setting)=>Supported;
        public string Scope(TweakSetting setting)=>setting.Kind==SettingKind.PowerAc?Plan:"";
        private static string? Original(SettingKind kind)=>kind switch {SettingKind.RegistryDword=>null,SettingKind.PowerAc=>"67",SettingKind.UiNumber=>"145",_=>"1"};
        public string? Read(TweakSetting setting,string scope) { _kinds[setting.Id]=setting.Kind;return _values.TryGetValue((setting.Id,scope),out var value)?value:Original(setting.Kind); }
        public void Write(TweakSetting setting,string scope,string? value) {BeforeWrite?.Invoke();if(FailWrite) throw new IOException("simulated denied write");Writes++;_values[(setting.Id,scope)]=value;}
        public bool IsOriginal(string id,string scope) => !_values.TryGetValue((id,_kinds[id]==SettingKind.PowerAc?scope:""),out var value)||value==Original(_kinds[id]);
    }
    // Test adapter: no Windows registry or user settings are written.
    public sealed class AppSettings {
        public Dictionary<string, string?> TweakBackup { get; set; } = new();
        public int Saves { get; private set; }
        public bool FailSave { get; set; }
        public void Save() { if (FailSave) throw new IOException("simulated persistence failure"); Saves++; }
    }
}
