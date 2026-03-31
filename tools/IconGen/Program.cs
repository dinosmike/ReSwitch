using ReSwitch.Services;

// По умолчанию: ReSwitch\app.ico относительно корня репозитория (IconGen\bin\...\ → пять уровней вверх).
var path = args.Length > 0
    ? args[0]
    : Path.Combine(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
        "ReSwitch",
        "app.ico");

path = Path.GetFullPath(path);
AppIconFactory.SaveApplicationIconFile(path);
Console.WriteLine($"OK: {path}");
