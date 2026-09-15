using System.Drawing.Imaging;

// Render the checked-in SVG sources using the application's existing SVG renderer.
// / 使用应用已有的 SVG 渲染器，从版本控制中的矢量源生成多尺寸图标。
string assets = Path.GetFullPath(args.Single());
WriteIcon("remotehubstudio", [16, 20, 24, 32, 40, 48, 64, 128, 256]);
WriteIcon("remotehubstudio-tray", [16, 20, 24, 32, 40, 48, 64]);
foreach (int size in new[] { 256, 512 })
{
    using Bitmap bitmap = Render("remotehubstudio", size);
    bitmap.Save(Path.Combine(assets, $"remotehubstudio-{size}.png"), ImageFormat.Png);
}

Bitmap Render(string name, int size) =>
    AntdUI.SvgExtend.SvgToBmp(File.ReadAllText(Path.Combine(assets, name + ".svg")), size, size, null)
    ?? throw new InvalidOperationException($"Cannot render {name} at {size}px.");

void WriteIcon(string name, int[] sizes)
{
    byte[][] frames = sizes.Select(size =>
    {
        using Bitmap bitmap = Render(name, size);
        using MemoryStream buffer = new();
        bitmap.Save(buffer, ImageFormat.Png);
        return buffer.ToArray();
    }).ToArray();
    using BinaryWriter writer = new(File.Create(Path.Combine(assets, name + ".ico")));
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    int offset = 6 + 16 * sizes.Length;
    for (int index = 0; index < sizes.Length; index++)
    {
        writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
        writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(frames[index].Length);
        writer.Write(offset);
        offset += frames[index].Length;
    }
    foreach (byte[] frame in frames) writer.Write(frame);
}
