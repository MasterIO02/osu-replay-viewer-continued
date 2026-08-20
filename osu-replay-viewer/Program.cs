using osu.Framework;
using osu.Framework.Platform;
using osu_replay_renderer_netcore.CLI;
using osu_replay_renderer_netcore.CustomHosts;
using osu_replay_renderer_netcore.Patching;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using osu_replay_renderer_netcore.Audio.Conversion;
using osu_replay_renderer_netcore.CustomHosts.CustomClocks;
using osu_replay_renderer_netcore.CustomHosts.Record;
using osu.Framework.Timing;

namespace osu_replay_renderer_netcore
{
    class Program
    {
        const string GAME_NAME = "osu_replay_viewer";
        const string OSU_GAME_NAME = "osu";

        static void Main(string[] args)
        {
            if (RealmLookup.TryHandleCommand(args, out int realmLookupExitCode))
            {
                Environment.ExitCode = realmLookupExitCode;
                return;
            }

            // Command tree
            OptionDescription alwaysYes;
            OptionDescription modOverride;
            OptionDescription query;
            OptionDescription osuGameName;
            OptionDescription orvConfigPath;

            OptionDescription generalHelp;
            OptionDescription generalList;
            OptionDescription generalView;

            OptionDescription recordMode;
            OptionDescription recordOutput;

            OptionDescription experimental;
            OptionDescription overrideOverlayOptions;
            OptionDescription applySkin;
            OptionDescription listSkins;
            OptionDescription beatmapImport;
            OptionDescription extendedImportInfo;
            OptionDescription preserveImportedFiles;
            OptionDescription dataPath;
            OptionDescription screenshot;
            OptionDescription screenshotOutput;

            CommandLineProcessor cli = new()
            {
                Options = new[]
                {
                    // General
                    alwaysYes = new()
                    {
                        Name = "Always Yes",
                        Description = "Always answer yes to all prompts. Similar to 'command | yes'",
                        DoubleDashes = new[] { "yes" }
                    },
                    modOverride = new()
                    {
                        Name = "Mod Override",
                        Description = "Override Mod(s). You can use 'no-mod' or 'acronyms:NM' to clear all mods",
                        DoubleDashes = new[] { "mod-override" },
                        SingleDash = new[] { "MOD" },
                        Parameters = new[] { "<Mod Name/acronyms:AC>" }
                    },
                    query = new()
                    {
                        Name = "Query",
                        Description = "Query data (Eg: find something in help index or query replays)",
                        DoubleDashes = new[] { "query" },
                        SingleDash = new[] { "q" },
                        Parameters = new[] { "Keyword" }
                    },
                    osuGameName = new()
                    {
                        Name = "osu!lazer mode",
                        Description = "Use osu!lazer data (songs, skins, replays), possible data loss!",
                        DoubleDashes = new[] { "osu-mode" },
                        SingleDash = new[] { "osu" }
                    },
                    beatmapImport = new()
                    {
                        Name = "Import beatmap",
                        Description = "Import beatmap from file",
                        DoubleDashes = new[] { "import-beatmap" },
                        SingleDash = new[] { "osz" },
                        Parameters = new[] { "path/to/File.osz" }
                    },
                    generalList = new()
                    {
                        Name = "List Replays",
                        Description = "List all local replays",
                        DoubleDashes = new[] { "list" },
                        SingleDash = new[] { "list", "l" }
                    },
                    generalView = new()
                    {
                        Name = "View Replay",
                        Description = "Select a replay to view. This options must be always present (excluding -list options)",
                        DoubleDashes = new[] { "view" },
                        SingleDash = new[] { "view", "i" },
                        Parameters = new[] { "Type (local/online/file/auto/md5)", "Score GUID/Beatmap ID (auto)/File.osr/MD5 hash" }
                    },
                    generalHelp = new()
                    {
                        Name = "Help Index",
                        Description = "View help with details",
                        DoubleDashes = new[] { "help" },
                        SingleDash = new[] { "h" }
                    },
                    orvConfigPath = new()
                    {
                        Name = "osu-replay-viewer config path",
                        Description = "Use config from file",
                        DoubleDashes = new[] { "config" },
                        SingleDash = new[] { "c" },
                        Parameters = new []{ "/path/to/config.json" },
                        ProcessedParameters = new [] { "osu-replay-viewer-config.json" }
                    },

                    // Record options
                    recordMode = new()
                    {
                        Name = "Record Mode",
                        Description = "Switch to record mode",
                        DoubleDashes = new[] { "record" },
                        SingleDash = new[] { "R" }
                    },
                    recordOutput = new()
                    {
                        Name = "Record Output",
                        Description = "Set record output",
                        DoubleDashes = new[] { "record-output" },
                        SingleDash = new[] { "O" },
                        Parameters = new[] { "Output = osu-replay.mp4" },
                        ProcessedParameters = new[] { "osu-replay.mp4" }
                    },

                    // Misc
                    experimental = new()
                    {
                        Name = "Experimental Toggle",
                        Description = "Toggle experimental feature",
                        DoubleDashes = new[] { "experimental" },
                        SingleDash = new[] { "experimental" },
                        Parameters = new[] { "Flag" }
                    },
                    overrideOverlayOptions = new()
                    {
                        Name = "Override Overlay Options",
                        Description = "Control the visiblity of player overlay",
                        DoubleDashes = new[] { "overlay-override" },
                        SingleDash = new[] { "overlay" },
                        Parameters = new[] { "true/false" }
                    },
                    applySkin = new()
                    {
                        Name = "Select Skin",
                        Description = "Select a skin to use in replay",
                        DoubleDashes = new[] { "skin" },
                        SingleDash = new[] { "skin", "s" },
                        Parameters = new[] { "Type (import/select/match/id)", "Skin name/File.osk/Partial name/Id" }
                    },
                    listSkins = new()
                    {
                        Name = "List Skins",
                        Description = "List all available skins",
                        DoubleDashes = new[] { "list-skin", "list-skins" },
                        SingleDash = new [] { "lskins", "lskin" }
                    },
                    extendedImportInfo = new()
                    {
                        Name = "Extended import info",
                        Description = "Show extended info about imported objects (skins/beatmaps)",
                        DoubleDashes = new[] { "extended" },
                        SingleDash = new[] { "ex" }
                    },
                    preserveImportedFiles = new()
                    {
                        Name = "Preserve imported files",
                        Description = "Preserve imported beatmaps and skins files",
                        DoubleDashes = new[] { "preserve" },
                        SingleDash = new[] { "pr" }
                    },
                    dataPath = new()
                    {
                        Name = "Data path",
                        Description = "Custom data folder path for osu!lazer storage (default: ~/.local/share/osu_replay_viewer on Linux or AppData on Windows)",
                        DoubleDashes = new[] { "data-path" },
                        SingleDash = new[] { "data-path", "dp" },
                        Parameters = new[] { "/path/to/data" }
                    },
                    screenshot = new()
                    {
                        Name = "Screenshot",
                        Description = "Take a screenshot at the specified beatmap timestamp (in milliseconds)",
                        DoubleDashes = new[] { "screenshot" },
                        SingleDash = new[] { "ss" },
                        Parameters = new[] { "<timestamp_ms>" }
                    },
                    screenshotOutput = new()
                    {
                        Name = "Screenshot output",
                        Description = "Output path for the screenshot (default: screenshot.png)",
                        DoubleDashes = new[] { "screenshot-output" },
                        SingleDash = new[] { "screenshot-output", "so" },
                        Parameters = new[] { "path/to/output.png" }
                    },
                }
            };


            var patched = false;
            // Apply patches
            if (ShouldApplyPatch(args))
            {
                new AudioPatcher().DoPatching();
                new ClockPatcher().DoPatching();
                new RenderPatcher().DoPatching();
                new WindowPatcher().DoPatching();
                patched = true;
            }

            // Always seed RNG deterministically for reproducible screenshots
            new RNGPatcher().DoPatching();

            // Always remove scrolling "Watching ..." text from replay overlay
            new ReplayOverlayPatcher().DoPatching();

            var modsOverride = new List<string>();
            var experimentalFlags = new List<string>();

            modOverride.OnOptions += (args) => { modsOverride.Add(args[0]); };
            experimental.OnOptions += (args) => { experimentalFlags.Add(args[0]); };
            GameHost host;
            OsuGameRecorder game;

            try
            {
                var progParams = cli.ProcessOptionsAndFilter(args);
                if (args.Length == 0 || generalHelp.Triggered)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  dotnet run osu-replay-viewer [options...]");
                    Console.WriteLine("  osu-replay-viewer [options...]");
                    Console.WriteLine();
                    cli.PrintHelp(generalHelp.Triggered, query.Triggered ? query[0] : null);
                    return;
                }

                var orvConfig = Config.ReadFromFile(orvConfigPath[0]);

                var gameName = GAME_NAME;

                if (osuGameName.Triggered)
                {
                    gameName = OSU_GAME_NAME;
                }

                if (recordMode.Triggered)
                {
                    if (!CLIUtils.AskFileDelete(alwaysYes.Triggered, recordOutput[0])) return;

                    var recordClock = new RecordClock(orvConfig.RecordOptions.FrameRate);
                    if (patched)
                    {
                        WrappedClock sharedWrappedClock = null;
                        ClockPatcher.OnStopwatchClockSetAsSource += clock =>
                        {
                            if (sharedWrappedClock == null)
                                sharedWrappedClock = new WrappedClock(recordClock, clock.Source as StopwatchClock);
                            clock.ChangeSource(sharedWrappedClock);
                        };
                    }

                    var resolution = ParseResolution(orvConfig.RecordOptions.Resolution);

                    var config = new EncoderConfig
                    {
                        FPS = orvConfig.RecordOptions.FrameRate,
                        Resolution = resolution,
                        OutputPath = recordOutput[0],
                        Preset = orvConfig.FFmpegOptions.VideoEncoderPreset,
                        Encoder = orvConfig.FFmpegOptions.VideoEncoder,
                        Bitrate = orvConfig.FFmpegOptions.VideoEncoderBitrate,

                        // External only
                        FFmpegExec = orvConfig.FFmpegOptions.Executable,
                        PixelFormat = orvConfig.OutputOptions.PixelFormat,
                        ColorSpace = orvConfig.OutputOptions.ColorSpace,
                    };

                    Console.WriteLine($"[Encoder] Pixel format: {config.PixelFormat}, Color space: {config.ColorSpace}");

                    FFmpegAudioTools.FFmpegExec = orvConfig.FFmpegOptions.Executable;

                    if (!string.IsNullOrWhiteSpace(orvConfig.FFmpegOptions.LibrariesPath))
                    {
                        config.FFmpegPath = orvConfig.FFmpegOptions.LibrariesPath;
                    }

                    EncoderBase encoder;

                    if (ShouldUseNvidiaGpuEncoder(orvConfig, config))
                    {
                        if (config.PixelFormat != PixelFormatMode.NV12)
                        {
                            Console.WriteLine("[Encoder] NVIDIA GPU path requires NV12. Overriding output pixel format to NV12.");
                            config.PixelFormat = PixelFormatMode.NV12;
                        }

                        encoder = new NvidiaGpuFFmpegEncoder(config);
                    }
                    else
                    {
                        encoder = orvConfig.FFmpegOptions.Mode switch
                        {
                            FFmpegMode.Pipe => new ExternalFFmpegEncoder(config),
                            FFmpegMode.Binding => new FFmpegAutoGenEncoder(config),
                            _ => throw new ArgumentOutOfRangeException("FFmpeg mode")
                        };
                    }

                    host = new ReplayRecordGameHost(gameName, encoder, recordClock, orvConfig.RecordOptions.Renderer, patched, orvConfig.GameSettings, dataPath.Triggered ? dataPath[0] : null);
                }
                else if (screenshot.Triggered)
                {
                    var targetMs = double.Parse(screenshot[0]);
                    var fps = orvConfig.RecordOptions.FrameRate;
                    var outputPath = screenshotOutput.Triggered ? screenshotOutput[0] : "screenshot.png";

                    var recordClock = new RecordClock(fps);
                    if (patched)
                    {
                        WrappedClock sharedWrappedClock = null;
                        ClockPatcher.OnStopwatchClockSetAsSource += clock =>
                        {
                            if (sharedWrappedClock == null)
                                sharedWrappedClock = new WrappedClock(recordClock, clock.Source as StopwatchClock);
                            clock.ChangeSource(sharedWrappedClock);
                        };
                    }

                    var screenshotHost = new ScreenshotGameHost(gameName, recordClock, patched, dataPath.Triggered ? dataPath[0] : null);
                    screenshotHost.TargetScreenshotMs = targetMs;
                    screenshotHost.ScreenshotOutputPath = outputPath;
                    screenshotHost.Resolution = ParseResolution(orvConfig.RecordOptions.Resolution);
                    host = screenshotHost;

                    Console.WriteLine($"Screenshot mode: capturing at {targetMs}ms -> {outputPath}");
                }
                else
                {
                    // --view mode without record/screenshot
                    if (dataPath.Triggered)
                    {
                        host = new CustomDataPathGameHost(gameName, dataPath[0]);
                    }
                    else
                    {
                        host = Host.GetSuitableDesktopHost(gameName);
                    }
                }

                game = new OsuGameRecorder(orvConfig.GameSettings);
                game.ModsOverride = modsOverride;
                game.ExperimentalFlags = experimentalFlags;
                game.PreserveImportedFiles = preserveImportedFiles.Triggered;
                game.ExtendedImportInfo = extendedImportInfo.Triggered;
                game.ScreenshotMode = screenshot.Triggered;

                if (applySkin.Triggered)
                {
                    game.SkinActionType = (SkinAction)Enum.Parse(typeof(SkinAction), applySkin[0][0].ToString().ToUpper() + applySkin[0].Substring(1));
                    game.Skin = applySkin[1];
                }

                if (beatmapImport.Triggered)
                {
                    game.BeatmapPath = beatmapImport[0];
                }

                if (generalList.Triggered)
                {
                    game.ListReplays = true;
                    game.ListQuery = query.Triggered ? query[0] : null;
                }
                else if (listSkins.Triggered)
                {
                    game.SkinActionType = SkinAction.List;
                }
                else if (!generalView.Triggered && !beatmapImport.Triggered && !(applySkin.Triggered && game.SkinActionType == SkinAction.Import)) throw new CLIException
                {
                    Cause = "General Problem",
                    DisplayMessage = "--view must be present (except for --list, --list-skins, --import-beatmap, --skin import, and --screenshot)",
                    Suggestions = new[] {
                        "Add --view <Type> <ID/Path> to your command",
                        "Add --list to your command",
                        "Add --list-skins to your command",
                        "Add --import-beatmap <path> to your command",
                        "Add --skin import <path> to your command"
                    }
                };
                else if (generalView.Triggered)
                {
                    string path = generalView[1];
                    bool isValidInteger = long.TryParse(generalView[1], out long id);
                    bool isValidInt32 = id < int.MaxValue;
                    bool isValidGuid = Guid.TryParse(generalView[1], out var guid);

                    string viewArg = generalView[1].Trim().ToLowerInvariant();
                    bool isValidMd5 = viewArg.Length == 32 && viewArg.All(Uri.IsHexDigit);

                    if (
                        (generalView[0].Equals("local") && !isValidGuid) ||
                        ((
                            generalView[0].Equals("auto") ||
                            generalView[0].Equals("online")
                        ) && !isValidInteger) ||
                        (generalView[0].Equals("md5") && !isValidMd5)
                    ) throw new CLIException
                    {
                        Cause = "Command-line Arguments (Parsing)",
                        DisplayMessage = $"Value {generalView[1]} is invaild"
                    };

                    switch (generalView[0])
                    {
                        case "local":
                        case "auto":
                            if (generalView[0].Equals("local")) game.ReplayOfflineScoreID = guid;
                            else
                            {
                                if (!isValidInt32) throw new CLIException
                                {
                                    Cause = "Command-line Arguments (Parsing)",
                                    DisplayMessage = $"{id} exceed int32 limit (larger than {int.MaxValue})",
                                    Suggestions = new[] { "Keep the number lower than the limit" }
                                };
                                game.ReplayAutoBeatmapID = (int)id;
                            }
                            break;

                        case "online": game.ReplayOnlineScoreID = id; break;

                        case "md5": game.ReplayAutoBeatmapMD5 = viewArg; break;

                        case "file":
                            if (!File.Exists(path)) throw new CLIException
                            {
                                Cause = "Files",
                                DisplayMessage = $"{path} doesn't exists"
                            };
                            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory)) throw new CLIException
                            {
                                Cause = "Files",
                                DisplayMessage = $"{path} is detected as directory"
                            };
                            game.ReplayFileLocation = path;
                            break;

                        default:
                            throw new CLIException
                            {
                                Cause = "Command-line Arguments (Options)",
                                DisplayMessage = $"Unknown type: {generalView[0]}",
                                Suggestions = new[] { "Available types: local/online/file/auto/md5" }
                            };
                    }
                    game.ReplayViewType = generalView[0];
                }

                // Misc
                if (overrideOverlayOptions.Triggered) game.HideOverlaysInPlayer = ParseBoolOrThrow(overrideOverlayOptions.ProcessedParameters[0]);
                else if (recordMode.Triggered) game.HideOverlaysInPlayer = true;
                else game.HideOverlaysInPlayer = false;

                game.SkipIntro = orvConfig.GameSettings.SkipIntro;

            }
            catch (CLIException cliException)
            {
                Console.WriteLine("Error while processing CLI arguments:");
                Console.WriteLine($"  Cause:      {cliException.Cause}");
                Console.WriteLine($"  Message:    {cliException.DisplayMessage}");
                if (cliException.Suggestions.Length == 0) return;
                else if (cliException.Suggestions.Length == 1) Console.WriteLine($"  Suggestion: {cliException.Suggestions[0]}");
                else
                {
                    Console.WriteLine("  Suggestions:");
                    for (int i = 0; i < cliException.Suggestions.Length; i++) Console.WriteLine("  - " + cliException.Suggestions[i]);
                }
                return;
            }

            host.Run(game);
            host.Dispose();
        }

        static bool ParseBoolOrThrow(string str)
        {
            str = str.ToLower();
            return str switch
            {
                "true" or "yes" or "1" => true,
                "false" or "no" or "0" => false,
                _ => throw new CLIException
                {
                    Cause = "Command-line Arguments (Parsing)",
                    DisplayMessage = $"Invaild boolean: {str}",
                    Suggestions = new[] { "Allowed values: true/yes/1 or false/no/0" }
                }
            };
        }

        private static Size ParseResolution(string resolution)
        {
            var parts = resolution.ToLower().Split('x').Select(x => int.Parse(x.Trim())).ToArray();
            return new Size
            {
                Width = parts[0],
                Height = parts[1]
            };
        }

        private static bool CanApplyPatch()
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X86 ||
                   RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
                   RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        }

        /// <summary>
        /// Determine when to apply Harmony patches. Could decrease start time.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static bool ShouldApplyPatch(string[] args)
        {
            return CanApplyPatch() && args.Any(arg => arg.Equals("--record") || arg.Equals("-R") || arg.Equals("--screenshot") || arg.Equals("-ss"));
        }

        private static bool ShouldUseNvidiaGpuEncoder(Config config, EncoderConfig encoderConfig)
        {
            if (config.FFmpegOptions.Mode != FFmpegMode.Binding)
                return false;

            if (!config.FFmpegOptions.UseCudaIfPossible)
                return false;

            if (!NvidiaGpuFFmpegEncoder.IsSupportedConfig(encoderConfig))
                return false;

            return true;
        }
    }
}
