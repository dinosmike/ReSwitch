// Копирует ReSwitch\app.ico в указанный путь (по умолчанию — тот же путь в репозитории).
var path = args.Length > 0
    ? args[0]
    : Path.Combine(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
        "ReSwitch",
        "app.ico");

path = Path.GetFullPath(path);
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var src = Path.Combine(repoRoot, "ReSwitch", "app.ico");
if (!File.Exists(src))
{
    Console.Error.WriteLine($"Source not found: {src}");
    Environment.Exit(1);
}

Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.Copy(src, path, overwrite: true);
Console.WriteLine($"OK: {path}");
