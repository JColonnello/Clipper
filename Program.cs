// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

static Task<int> CallProcess(string exe, out Process process, params string[] args)
{
    Process proc = process = new();
    proc.StartInfo.FileName = Path.Combine(AppContext.BaseDirectory, exe);
    proc.StartInfo.UseShellExecute = false;
    proc.StartInfo.RedirectStandardOutput = true;
    foreach (string arg in args)
        proc.StartInfo.ArgumentList.Add(arg);
    proc.Start();

    return proc.WaitForExitAsync().ContinueWith(_ => proc.ExitCode);
}
static Task<int> CallFFMpeg(out Process process, params string[] args) => CallProcess("ffmpeg.exe", out process, args);
static Task<int> CallFFProbe(out Process process, params string[] args) => CallProcess("ffprobe.exe", out process, args);

static async Task<TimeSpan> GetLength(string file)
{
    Task exited = CallFFProbe(out Process process, [.. "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -sexagesimal".Split(' '), file]);
    string str = await process.StandardOutput.ReadLineAsync() ?? "";
    if (!TimeSpan.TryParse(str, out TimeSpan duration))
        throw new FormatException("No se puede leer la duración del video");
    process.Dispose();
    return duration;
}


static bool ParseTimestamp(string timestamp, out TimeSpan result)
{
    if (TimeSpan.TryParseExact(timestamp, @"m\:ss", null, out result))
        return true;
    if (TimeSpan.TryParseExact(timestamp, @"m\:ss\.FFF", null, out result))
        return true;
    if (TimeSpan.TryParseExact(timestamp, @"h\:mm\:ss", null, out result))
        return true;
    if (TimeSpan.TryParseExact(timestamp, @"h\:mm\:ss\.FFF", null, out result))
        return true;
    return false;
}

static TimeSpan? ReadTimestamp(string message)
{
    for (; ; )
    {
        Console.Write(message + ": ");
        string? line = Console.ReadLine();
        if (line == "" || line is null)
            return null;
        if (ParseTimestamp(line, out TimeSpan time))
            return time;
        Console.Error.WriteLine("Escribí bien");
    }
}

static string FormatTimestamp(TimeSpan time) => time.ToString("hh\\:mm\\:ss");

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: .\\Clipper.exe <video>");
    Console.Error.WriteLine("Sino, arrastrá el video al Clipper.exe");
    Console.Error.WriteLine("Presiona una tecla para salir...");
    Console.ReadKey();
    return 1;
}
string inputVideo = args[0];
FileInfo inputFile = new(inputVideo);
if(!inputFile.Exists)
{
    Console.Error.WriteLine("El archivo de entrada no existe");
    return 1;
}
TimeSpan duration = await GetLength(inputVideo);

TimeSpan? start, end;
do
    start = ReadTimestamp("Tiempo de inicio");
while (start is not null && (start < TimeSpan.Zero || start > duration));
do
    end = ReadTimestamp("Tiempo de fin");
while (end is not null && (end < TimeSpan.Zero || end > duration || start > end));

Console.Write("Nombre del clip: ");
string name = Console.ReadLine() ?? "clip";
name += ".webm";

const double maxFile = 10 * 8 * 1000;
double kbit = maxFile / ((end ?? duration) - (start ?? TimeSpan.Zero)).TotalSeconds;

Process ffmpegProcess;
using Stream stdout = Console.OpenStandardOutput();

string[] startStr = start is not null ? ["-ss", FormatTimestamp(start.Value)] : [];
string[] endStr = end is not null ? ["-to", FormatTimestamp(end.Value)] : [];
Task exited = CallFFMpeg(out ffmpegProcess, ["-i", inputVideo, ..startStr, ..endStr, .. $"-c:v libvpx-vp9 -b:v {kbit}K -pass 1 -an -f null NUL".Split(' ')]);
await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(stdout);
await exited;
ffmpegProcess.Dispose();

exited = CallFFMpeg(out ffmpegProcess, ["-i", inputVideo, .. startStr, ..endStr, .. $"-c:v libvpx-vp9 -b:v {kbit}K -pass 2 -c:a libopus".Split(' '), name]);
await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(stdout);
await exited;
ffmpegProcess.Dispose();

return 0;
