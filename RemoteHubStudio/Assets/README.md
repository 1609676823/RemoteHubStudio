# RemoteHubStudio icons

The blue terminal display and mint network nodes represent a remote connection hub.
The tray variant removes terminal details and thickens the strokes for small sizes
on both light and dark Windows taskbars. All artwork is original vector geometry.

- `remotehubstudio.svg`: editable application artwork.
- `remotehubstudio.ico`: 16, 20, 24, 32, 40, 48, 64, 128 and 256 px Windows application icon.
- `remotehubstudio-tray.svg` / `.ico`: simplified tray artwork, 16–64 px.
- `remotehubstudio-256.png` / `remotehubstudio-512.png`: transparent PNG exports.

Regenerate the binary exports from the repository root on Windows:

```powershell
dotnet run --project tools/IconGenerator -- RemoteHubStudio/Assets
```

The generator uses the same AntdUI SVG renderer as the app and needs no extra image library.
