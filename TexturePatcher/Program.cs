const string Usage = @"
PngPatcher - patch Unity Texture2D raw data directly inside an AssetBundle

Commands:
  info <image>
  list <bundle>
  inspect <bundle>
  analyze <bundle>
  dumpfx <bundle> [output_txt]
  patch <bundle> <texture_name> <image> [output_bundle]
  patchbg <bundle> <png|.trombackground|.tromb> [output_bundle]
  hide <bundle> <keep_gameobject> [output_bundle]

Examples:
  PngPatcher.exe list taps
  PngPatcher.exe inspect godsave_patched
  PngPatcher.exe analyze godsave_patched
  PngPatcher.exe patch taps taps_bg_01 bg.png taps_patched
  PngPatcher.exe patchbg godsave_patched bg.png godsave_final
  PngPatcher.exe patchbg godsave_patched bg.trombackground godsave_final
  PngPatcher.exe patchbg godsave_patched bg.tromb godsave_final
";

if (args.Length == 0)
{
    Console.WriteLine(Usage);
    return 0;
}

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "info":
            if (args.Length < 2)
                throw new ArgumentException("info ต้องการ <image>");

            InfoCommand.PrintImageInfo(args[1]);
            break;

        case "list":
            if (args.Length < 2)
                throw new ArgumentException("list ต้องการ <bundle>");

            ListCommand.ListTextures(args[1]);
            break;

        case "inspect":
            if (args.Length < 2)
                throw new ArgumentException("inspect ต้องการ <bundle>");

            InspectCommand.InspectBackground(args[1]);
            break;

        case "analyze":
            if (args.Length < 2)
                throw new ArgumentException("analyze ต้องการ <bundle>");

            AnalyzeCommand.AnalyzeBackground(args[1]);
            break;

        case "dumpfx":
            if (args.Length < 2)
                throw new ArgumentException("dumpfx ต้องการ <bundle> [output_txt]");

            AnalyzeCommand.DumpEffects(
                args[1],
                args.Length >= 3 ? args[2] : null);
            break;

        case "patch":
            if (args.Length < 4)
                throw new ArgumentException(
                    "patch ต้องการ <bundle> <texture_name> <image> [output_bundle]");

            PatchCommand.PatchBundle(
                args[1],
                args[2],
                args[3],
                args.Length >= 5 ? args[4] : args[1] + ".patched");
            break;

        case "patchbg":
            if (args.Length < 3)
                throw new ArgumentException(
                    "patchbg ต้องการ <bundle> <png|.trombackground|.tromb> [output_bundle]");

            PatchBgCommand.PatchBackground(
                args[1],
                args[2],
                args.Length >= 4 ? args[3] : args[1] + ".patched");
            break;

        case "hide":
            if (args.Length < 3)
                throw new ArgumentException(
                    "hide ต้องการ <bundle> <keep_gameobject> [output_bundle]");

            HideCommand.HideOthers(
                args[1],
                args[2],
                args.Length >= 4 ? args[3] : args[1] + ".hidden");
            break;

        default:
            Console.WriteLine(Usage);
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[ERROR] {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

return 0;
