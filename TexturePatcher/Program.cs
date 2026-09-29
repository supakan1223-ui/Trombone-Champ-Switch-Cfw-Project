const string Usage = @"
TexturePatcher - patch Unity Texture2D raw data directly inside an AssetBundle

Commands:
  info <image>
  list <bundle>
  patch <bundle> <texture_name> <image> [output_bundle]
  inspect <bundle>
  analyze <bundle>
  analyze-bg-source <trombackground>
  patchbg <target_bundle> <source_trombackground> [output_bundle]
  hide <bundle> <keep_gameobject> [output_bundle]

Example:
  TexturePatcher.exe list taps
  TexturePatcher.exe patch taps taps_bg_01 bg.png taps_patched
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
            if (args.Length < 2) throw new ArgumentException("info ต้องการ <image>");
            InfoCommand.PrintImageInfo(args[1]);
            break;

        case "list":
            if (args.Length < 2) throw new ArgumentException("list ต้องการ <bundle>");
            ListCommand.ListTextures(args[1]);
            break;

        case "inspect":
            if (args.Length < 2) throw new ArgumentException("inspect ต้องการ <bundle>");
            InspectCommand.InspectBackground(args[1]);
            break;

        case "analyze":
            if (args.Length < 2) throw new ArgumentException("analyze ต้องการ <bundle>");
            AnalyzeCommand.AnalyzeBackground(args[1]);
            break;

        case "analyze-bg-source":
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: analyze-bg-source <trombackground>");
                return 0;
            }
            AnalyzeCommand.AnalyzeBackgroundSource(args[1]);
            break;

        case "patchbg":
            if (args.Length < 3)
                throw new ArgumentException("patchbg ต้องการ <target_bundle> <source_trombackground> [output_bundle]");
            PatchBgCommand.PatchBackground(args[1], args[2], args.Length >= 4 ? args[3] : args[1] + "_bgpatched");
            break;

        case "patch":
            if (args.Length < 4) throw new ArgumentException("patch ต้องการ <bundle> <texture_name> <image> [output_bundle]");
            PatchCommand.PatchBundle(args[1], args[2], args[3], args.Length >= 5 ? args[4] : args[1] + ".patched");
            break;

        case "hide":
            if (args.Length < 3) throw new ArgumentException("hide ต้องการ <bundle> <keep_gameobject> [output_bundle]");
            HideCommand.HideOthers(args[1], args[2], args.Length >= 4 ? args[3] : args[1] + ".hidden");
            break;

        default:
            Console.WriteLine(Usage);
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[ERROR] {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

return 0;
