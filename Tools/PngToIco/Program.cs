// 将 PNG 转为 Win32 可用的多尺寸 ICO（16, 32, 48, 256）
// 用法: dotnet run -- <input.png> [output.ico]
using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

int[] sizes = [16, 32, 48, 256];
string input = args.Length > 0 ? args[0] : "logo.png";
string output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".ico");

if (!File.Exists(input))
{
    Console.Error.WriteLine($"File not found: {input}");
    Environment.Exit(1);
}

using var image = await Image.LoadAsync(input);
var pngs = new List<(byte[] Data, int Width, int Height)>();

foreach (int size in sizes)
{
    using var resized = image.Clone(ctx => ctx.Resize(size, size));
    var ms = new MemoryStream();
    resized.SaveAsPng(ms);
    pngs.Add((ms.ToArray(), resized.Width, resized.Height));
}

await using var fs = File.Create(output);
await using var w = new BinaryWriter(fs);

w.Write((byte)0);
w.Write((byte)0);
w.Write((short)1);
w.Write((short)pngs.Count);

long offset = 6 + (16 * pngs.Count);

foreach (var png in pngs)
{
    w.Write((byte)(png.Width >= 256 ? 0 : png.Width));
    w.Write((byte)(png.Height >= 256 ? 0 : png.Height));
    w.Write((byte)0);
    w.Write((byte)0);
    w.Write((short)0);
    w.Write((short)32);
    w.Write((uint)png.Data.Length);
    w.Write((uint)offset);
    offset += png.Data.Length;
}

foreach (var png in pngs)
    w.Write(png.Data);

Console.WriteLine($"Written: {output}");
