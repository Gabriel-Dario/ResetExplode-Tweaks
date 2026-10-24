namespace ResetExplode;

/// <summary>Shared, testable backup ordering; system adapters supply the actual read/write.</summary>
public static class TweakValueBackup
{
    public static void Write(IDictionary<string,string?> backup, string key, Func<string?> read,
        Action<string?> write, Action persist, string? value)
    {
        if (!backup.ContainsKey(key)) {
            backup[key]=read();
            try { persist(); }
            catch { backup.Remove(key); throw; }
        }
        write(value);
    }
    public static void Restore(IDictionary<string,string?> backup, string key, Func<string?> read, Action<string?> write)
    {
        if (!backup.TryGetValue(key,out string? original)) return; // this sub-setting was never changed
        write(original);
        if (read()!=original) throw new InvalidOperationException("Restauração não confirmada: "+key);
    }
}
