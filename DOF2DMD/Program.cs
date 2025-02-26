// DOF2DMD : a utility to interface DOFLinx with DMD Devices through 
//           [FlexDMD](https://github.com/vbousquet/flexdmd), and 
//           [Freezy DMD extensions](https://github.com/freezy/dmd-extensions)
//
//                                            ##          ##
//                                              ##      ##         )  )
//                                            ##############
//                                          ####  ######  ####
//                                        ######################
//                                        ##  ##############  ##     )   )
//                                        ##  ##          ##  ##
//                                              ####  ####
//
//                                     Copyright (C) 2024 Olivier JACQUES & Gustavo Lara
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

using System;
using System.Drawing;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using FlexDMD;
using FlexDMD.Actors;
using FlexDMD.Scenes;
using System.IO;
using System.Drawing.Imaging;
using System.Web;
using Microsoft.Extensions.Configuration;
using System.Text;
using UltraDMD;
using System.Reflection.Emit;
using static System.Formats.Asn1.AsnWriter;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;
using FlexDMD.Properties;
using System.Collections;
using FuzzySharp;
using FuzzySharp.SimilarityRatio;

namespace DOF2DMD
{
    class DOF2DMD
    {
        public static FlexDMD.FlexDMD gDmdDevice;
        public static int[] gScore = [0, 0, 0, 0, 0];
        public static int gActivePlayer = 1;
        public static int gNbPlayers = 1;
        public static int gCredits = 1;
        private static readonly object gGameMarqueeLock = new object();
        public static string gGameMarquee = "DOF2DMD";
        // Getter
        public static string GetGameMarquee()
        {
            lock (gGameMarqueeLock)
            {
                return gGameMarquee;
            }
        }

        // Setter
        public static void SetGameMarquee(string value)
        {
            lock (gGameMarqueeLock)
            {
                gGameMarquee = value;
            }
        }
        private static Timer _scoreTimer;
        private static Timer _animationTimer = null;
        private static Timer _loopTimer;
        private static readonly object _scoreQueueLock = new object();
        private static readonly object _animationQueueLock = new object();
        private static readonly object sceneLock = new object();
        private static Sequence _queue;


        public static ScoreBoard _scoreBoard;
        // Animation item for the queue
        private class QueueItem
        {
            public string Path { get; set; }
            public float Duration { get; set; } 
            public string Animation { get; set; }

            public QueueItem(string path, float duration, string animation)
            {
                Path = path;
                Duration = duration;
                Animation = animation;
            }
        }
        private static Queue<QueueItem> _animationQueue = new Queue<QueueItem>();
        private static float _currentDuration;
        
        private static Timer _scoreDelayTimer;

        static async Task Main()
        {
            // Set up logging to a file
            Console.OutputEncoding = Encoding.UTF8;
            Trace.Listeners.Add(new TextWriterTraceListener("dof2dmd.log") { TraceOutputOptions = TraceOptions.Timestamp });
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;

            LogIt($"Starting DOF2DMD v{Assembly.GetExecutingAssembly().GetName().Version}...");
            LogIt("Starting HTTP listener");
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"{AppSettings.UrlPrefix}/");
            listener.Start();
            LogIt($"DOF2DMD is now listening for requests on {AppSettings.UrlPrefix}...");

            // Initialize DMD in parallel
            LogIt("Starting DMD initialization");
            var dmdInitTask = Task.Run(() => InitializeDMD());

            // Start handling HTTP connections
            LogIt("Starting HTTP connection handler");
            var listenTask = HandleIncomingConnections(listener);

            // Wait for DMD initialization to complete
            LogIt("Waiting for DMD initialization to complete");
            await dmdInitTask;

            // Wait for the HTTP listener
            LogIt("DOF2DMD now fully initialized!");
            await listenTask;
        }

        private static void InitializeDMD()
        {
            var grayColor = Color.FromArgb(168, 168, 168);

            // Initialize DMD device with configuration
            gDmdDevice = new FlexDMD.FlexDMD
            {
                Width = AppSettings.dmdWidth,
                Height = AppSettings.dmdHeight,
                GameName = "DOF2DMD",
                Color = Color.White,
                RenderMode = RenderMode.DMD_RGB,
                Show = true,
                Run = true
            };

            // Initialize sequence
            _queue = new Sequence(gDmdDevice) { FillParent = true };

            // Initialize fonts
            var fonts = InitializeFonts(gDmdDevice, grayColor);

            // Initialize scoreboard
            _scoreBoard = new ScoreBoard(
                gDmdDevice,
                fonts.NormalFont,
                fonts.HighlightFont,
                fonts.TextFont
            )
            { Visible = false };

            // Add actors to stage
            gDmdDevice.Stage.AddActor(_queue);
            gDmdDevice.Stage.AddActor(_scoreBoard);

            // Set and display game marquee
            SetGameMarquee(AppSettings.StartPicture);
            DisplayPicture(GetGameMarquee(), -1, "none", false);
        }

        private static (FlexDMD.Font TextFont, FlexDMD.Font NormalFont, FlexDMD.Font HighlightFont) InitializeFonts(
            FlexDMD.FlexDMD device, Color grayColor)
        {
            // Font configurations
            var fontConfig = new[]
            {
                new { Path = "", ForeColor = Color.Black } // Inicialización genérica
            };
            if(gDmdDevice.Height == 64 && gDmdDevice.Width == 256)
            {
                fontConfig = new[]
                {
                    new { Path = "FlexDMD.Resources.udmd-f6by12.fnt", ForeColor = grayColor },
                    new { Path = "FlexDMD.Resources.udmd-f7by13.fnt", ForeColor = grayColor },
                    new { Path = "FlexDMD.Resources.udmd-f12by24.fnt", ForeColor = Color.Orange }
                };
            }
            else
            {
                fontConfig = new[]
                {
                    new { Path = "FlexDMD.Resources.udmd-f4by5.fnt", ForeColor = grayColor },
                    new { Path = "FlexDMD.Resources.udmd-f7by13.fnt", ForeColor = grayColor },
                    new { Path = "FlexDMD.Resources.udmd-f12by24.fnt", ForeColor = Color.Orange }
                };
            }
            return (
                TextFont: device.NewFont(fontConfig[0].Path, fontConfig[0].ForeColor, Color.Black, 1),
                NormalFont: device.NewFont(fontConfig[1].Path, fontConfig[1].ForeColor, Color.Black, 1),
                HighlightFont: device.NewFont(fontConfig[2].Path, fontConfig[2].ForeColor, Color.Red, 1)
            );
        }



        /// <summary>
        /// Callback method once animation is finished.
        /// Displays the player's score
        /// </summary>
        private static void AnimationTimer(object state)
        {
            _animationTimer.Dispose();
            _animationTimer = null;

            // Check if there are more animations in the queue
            if (_animationQueue.Count > 0)
            {
                lock (_animationQueueLock)
                {
                    var item = _animationQueue.Dequeue();
                    LogIt($"⏱️ ⏳AnimationTimer: animation done, I will play {item.Path} next");
                    if (_animationQueue.Count > 0)
                    {
                        LogIt($"⏱️ ⏳Animation queue has now {_animationQueue.Count} items: {string.Join(", ", _animationQueue.Select(i => i.Path))}");
                    }
                    else
                    {
                        LogIt($"⏱️ ⏳Animation queue is now empty");
                    }

                    DisplayPicture(item.Path, item.Duration, item.Animation, false);
                }
            }
            else if (AppSettings.ScoreDmd != 0)
            {
                LogIt("⏱️ AnimationTimer: previous animation is done, no more animation queued, starting 1s delay before score");

                // Dispose existing delay timer if any
                _scoreDelayTimer?.Dispose();

                // Create new timer with 1 second delay
                _scoreDelayTimer = new Timer(DelayedScoreDisplay, null, 1000, Timeout.Infinite);
            }
        }

        private static void DelayedScoreDisplay(object state)
        {
            _scoreDelayTimer?.Dispose();
            _scoreDelayTimer = null;

            // Check if we still want to display the score (no new animations queued)
            if (_animationQueue.Count == 0 && AppSettings.ScoreDmd != 0)
            {
                LogIt("⏱️ DelayedScoreDisplay: delay complete, displaying score");
                if (gScore[gActivePlayer] > 0)
                {
                    DisplayScore(gNbPlayers, gActivePlayer, gScore[gActivePlayer], false, gCredits);
                }
            }
        }
        /// <summary>
        /// This method is a callback for a timer that displays the current score.
        /// It then calls the DisplayPicture method to show the game marquee picture.
        /// </summary>
        private static void ScoreTimer(object state)
        {
            LogIt("⏱️ ScoreTimer - restore marquee");
            lock (_scoreQueueLock)
            {
                try
                {
                    DisplayPicture(GetGameMarquee(), -1, "none", false);
                }
                finally
                {
                    // Ensure that the timer is not running
                    _scoreTimer?.Dispose();
                }
            }
        }

        /// <summary>
        /// This class provides access to application settings stored in an INI file.
        /// The settings are loaded from the 'settings.ini' file in the current directory.
        /// If a setting is not found in the file, default values are provided.
        /// </summary>
        public class AppSettings
        {
            private static IConfiguration _configuration;

            static AppSettings()
            {
                var builder = new ConfigurationBuilder();
                builder.SetBasePath(Directory.GetCurrentDirectory());
                builder.AddIniFile("settings.ini", optional: true, reloadOnChange: true);

                _configuration = builder.Build();
            }

            public static string UrlPrefix => _configuration["url_prefix"] ?? "http://127.0.0.1:8080";
            public static int NumberOfDmd => Int32.Parse(_configuration["number_of_dmd"] ?? "1");
            public static int AnimationDmd => Int32.Parse(_configuration["animation_dmd"] ?? "1");
            public static int ScoreDmd => Int32.Parse(_configuration["score_dmd"] ?? "1");
            public static int marqueeDmd => Int32.Parse(_configuration["marquee_dmd"] ?? "1");
            public static int displayScoreDuration => Int32.Parse(_configuration["display_score_duration_s"] ?? "5");
            public static bool Debug => Boolean.Parse(_configuration["debug"] ?? "false");
            public static string artworkPath => _configuration["artwork_path"] ?? "artwork";
            public static ushort dmdWidth => ushort.Parse(_configuration["dmd_width"] ?? "128");
            public static ushort dmdHeight => ushort.Parse(_configuration["dmd_height"] ?? "32");
            public static string StartPicture => _configuration["start_picture"] ?? "DOF2DMD";
        }

        /// <summary>
        /// Save debug message in file
        /// </summary>
        public static void LogIt(string message)
        {
            // If debug is enabled
            if (AppSettings.Debug)
            {
                Trace.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Thread {Thread.CurrentThread.ManagedThreadId}] {message}");
            }
        }
        public static Boolean DisplayScore(int cPlayers, int player, int score, bool sCleanbg, int credits)
        {
            gScore[player] = score;
            gActivePlayer = player;
            gNbPlayers = cPlayers;
            gCredits = credits;
            _scoreDelayTimer?.Dispose();
            // If no ongoing animation or we can display score over it
            if (_animationTimer == null || sCleanbg == false || _currentDuration == -1)
            {
                LogIt($"DisplayScore for player {player}: {score}");
                DisplayScoreboard(gNbPlayers, player, gScore[1], gScore[2], gScore[3], gScore[4], "", "", sCleanbg);
            } 
            return true;

        }
        /// <summary>
        /// Displays the Score Board on the DMD device using native FlexDMD capabilities.
        /// </summary>
        public static bool DisplayScoreboard(int cPlayers, int highlightedPlayer, Int64 score1, Int64 score2, Int64 score3, Int64 score4, string lowerLeft, string lowerRight, bool cleanbg)
        {
            try
            {
                
                _queue.Visible = !cleanbg;

                //gDmdDevice.LockRenderThread();
                gDmdDevice.Post(() =>
                {

                    _scoreBoard.SetNPlayers(cPlayers);
                    _scoreBoard.SetHighlightedPlayer(highlightedPlayer);
                    _scoreBoard.SetScore(score1, score2, score3, score4);
                    _scoreBoard._lowerLeft.Text = lowerLeft;
                    _scoreBoard._lowerRight.Text = lowerRight;

                    _scoreBoard.Visible = true;


                });
                //gDmdDevice.UnlockRenderThread();
                if (AppSettings.ScoreDmd != 0)
                {
                    _scoreTimer?.Dispose();
                    _scoreTimer = new Timer(ScoreTimer, null, AppSettings.displayScoreDuration * 1000, Timeout.Infinite);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogIt($"  Error occurred while genering the Score Board. {ex.Message}");
                return false;
            }
        }

        private static string FindBestFuzzyMatch(string searchPath, List<string> validExtensions)
        {
            try
            {
                string directory = Path.GetDirectoryName(searchPath) ?? AppSettings.artworkPath;
                string searchFileName = Path.GetFileNameWithoutExtension(searchPath);

                if (!Directory.Exists(directory))
                    return null;

                // Get all files with valid extensions
                var files = Directory.GetFiles(directory)
                    .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                if (!files.Any())
                    return null;

                // Create a dictionary to maintain original paths
                var fileDict = files
                    .GroupBy(f => Path.GetFileNameWithoutExtension(f))
                    .ToDictionary(
                        g => g.Key,
                        g => g.First()
                    );

                // Get the best match using FuzzySharp
                var bestMatch = FuzzySharp.Process.ExtractOne(
                    searchFileName,
                    fileDict.Keys,
                    cutoff: 65  // Minimum score threshold (0-100)
                );

                if (bestMatch != null)
                {
                    return fileDict[bestMatch.Value];
                }

                return null;
            }
            catch (Exception ex)
            {
                LogIt($"Error in fuzzy matching: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Displays an image or video file on the DMD device using native FlexDMD capabilities.
        /// </summary>
        public static bool DisplayPicture(string path, float duration, string animation, bool toQueue)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
        
                // Validate file path and existence
                string localPath;
                localPath = HttpUtility.UrlDecode(
                    Path.IsPathRooted(path)
                        ? Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))
                        : Path.Combine(AppSettings.artworkPath, 
                            Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)))
                );

                // If path is gGameMarquee, then extensions are only static (no video) pictures
                List<string> extensions = null;
                if (path == GetGameMarquee())
                {
                    // List of possible extensions for a static marquee
                    extensions = new List<string> { ".png", ".jpg", ".bmp" };
                    LogIt($"Setting marquee to: {path}");
                }
                else
                {
                    // List of possible extensions for other
                    extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };
                }

                // Find the file to display
                if (!FileExistsWithExtensions(localPath, extensions, out string foundExtension))
                {
                    var matchedFile = FindBestFuzzyMatch(localPath, extensions);
                    if (!string.IsNullOrEmpty(matchedFile))
                    {
                        LogIt($"Exact match not found for {localPath}, but found {matchedFile} using fuzzy matching");
                        localPath = Path.Combine(
                            Path.GetDirectoryName(matchedFile),
                            Path.GetFileNameWithoutExtension(matchedFile)
                        );
                        foundExtension = Path.GetExtension(matchedFile);
                    }
                    else
                    {
                        LogIt($"❗ Picture not found {localPath}");
                        return false;
                    }
                }
        
                string fullPath = localPath + foundExtension;
                if (localPath.Contains("&"))
                {
                    LogIt($"❗ Can't display picture with '&' in the name {fullPath}.\nSolution is rename the file and replace '&' by '-' in file name - see https://github.com/DMDTools/DOF2DMD/issues/27");
                    return false;
                }
                bool isVideo = new List<string> { ".gif", ".avi", ".mp4" }.Contains(foundExtension.ToLower());
                bool isImage = new List<string> { ".png", ".jpg", ".bmp" }.Contains(foundExtension.ToLower());
                if (!isVideo && !isImage)
                {
                    return false;
                }
        
                // Now that we've validated everything, process the display asynchronously
                _ = Task.Run(() =>
                {
                    // Check if gDmdDevice is initialized
                    int retries = 10;
                    while (gDmdDevice == null && retries > 0)
                    {
                        Thread.Sleep(1000);
                        LogIt($"Retrying DMD device initialization {retries} retries left");
                        retries--;
                    }

                    if (gDmdDevice == null)
                    {
                        LogIt("DMD device initialization failed 10 retries");
                        return;
                    }

                    // If this picture needs to be queued AND there is an animation running BUT current animation is not meant to be infinite, 
                    // then add this picture and its parameters to the animation queue. The animation timer will take care of it
                    if (toQueue && _animationTimer != null && _currentDuration > 0)
                    {
                        lock (_animationQueueLock)
                        {
                            LogIt($"⏳Queuing {path} for display after current animation");
                            _animationQueue.Enqueue(new QueueItem(path, duration, animation));
                            LogIt($"⏳Queue has {_animationQueue.Count} items: {string.Join(", ", _animationQueue.Select(i => i.Path))}");
                            return;
                        }
                    }

                    gDmdDevice.Post(() =>
                    {
                        gDmdDevice.Clear = true;
                        try
                        {
                            // Clear existing resources
                            _queue.RemoveAllScenes();
                            gDmdDevice.Graphics.Clear(Color.Black);
                            _scoreDelayTimer?.Dispose();
                            _scoreDelayTimer = null;
                            _scoreBoard.Visible = false;
                        }
                        catch (Exception ex)
                        {
                            LogIt($"⚠️ Warning: Error while clearing resources: {ex.Message}");
                            // Continue execution as this is not critical
                        }
                        Actor mediaActor;
                        try
                        {
                            mediaActor = isVideo ?
                                (Actor)gDmdDevice.NewVideo("MyVideo", fullPath) :
                                (Actor)gDmdDevice.NewImage("MyImage", fullPath);

                            mediaActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                        }
                        catch (Exception ex)
                        {
                            LogIt($"❌ Error creating media actor: {ex.Message}");
                            return;
                        }

                        // Set random position if the file name contains "expl" (explosion?)
                        if (fullPath.Contains("expl"))
                        {
                            mediaActor.SetPosition(new Random().Next(-1, 2) * 32, 0);

                        }
                        // Handle looping for GIFs when duration is -1
                        if (isVideo && duration < 0)
                        {
                            LogIt($"🔄 Setting video loop to true for {fullPath}");
                            ((AnimatedActor)mediaActor).Loop = true; //this doesn't work
                        }
                        _currentDuration = duration;
                        // If duration is negative - show immediately and clear the animation queue
                        if (duration < 0)
                        {
                            lock (_animationQueueLock)
                            {
                                _animationQueue.Clear();
                                LogIt($"⏳Animation queue cleared as duration was negative (immediate display, infinite duration)");
                            }
                            duration = 0;
                        }

                        // Adjust duration for videos and images if not explicitly set
                        // For image, set duration to infinite (9999s)
                        duration = (isVideo && duration == 0) ? ((AnimatedActor)mediaActor).Length :
                                   (isImage && duration == 0) ? 9999 : duration;

                        // Arm timer once animation is done playing
                        _animationTimer?.Dispose();
                        _animationTimer = new Timer(AnimationTimer, null, (int)(duration * 1000), Timeout.Infinite);
                        
                        //BackgroundScene bg = CreateBackgroundScene(gDmdDevice, mediaActor, animation.ToLower(), duration);
                        BackgroundScene bg = CreateBackgroundScene(gDmdDevice, mediaActor, animation.ToLower(), -1);
                        
                        _queue.Visible = true;
                        _queue.Enqueue(bg);
                        LogIt($"📷Rendering {(isVideo ? $"video (duration: {duration * 1000}ms)" : "image")}: {fullPath}");
                    });
        
                });
        
                // Return true immediately after validation, while display processing continues in background
                return true;
            }
            catch (Exception ex)
            {
                LogIt($"Error occurred while fetching the image. {ex.Message}");
                return false;
            }
        }
        


        private static BackgroundScene CreateBackgroundScene(FlexDMD.FlexDMD gDmdDevice, Actor mediaActor, string animation, float duration)
        {
            return animation switch
            {
                "none" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.None, duration, AnimationType.None, ""),
                "fade" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.FadeIn, duration, AnimationType.FadeOut, ""),
                "scrollright" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnRight, duration, AnimationType.ScrollOffRight, ""),
                "scrollrightleft" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnRight, duration, AnimationType.ScrollOffLeft, ""),
                "scrollleft" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnLeft, duration, AnimationType.ScrollOffLeft, ""),
                "scrollleftright" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnLeft, duration, AnimationType.ScrollOffRight, ""),
                "scrolldown" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnDown, duration, AnimationType.ScrollOffDown, ""),
                "scrolldownup" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnDown, duration, AnimationType.ScrollOffUp, ""),
                "scrollup" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnUp, duration, AnimationType.ScrollOffUp, ""),
                "scrollupdown" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnUp, duration, AnimationType.ScrollOffDown, ""),
                "zoom" => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ZoomIn, duration, AnimationType.ZoomOut, ""),
                _ => new BackgroundScene(gDmdDevice, mediaActor, AnimationType.None, duration, AnimationType.None, "")
            };
        }

        /// <summary>
        /// Displays text on the DMD device.
        /// %0A or | for line break
        /// </summary>
        public static bool DisplayText(string text, string size, string color, string font, string bordercolor, string bordersize, bool cleanbg, string animation, float duration, bool loop)
        {
            try
            {
                // Convert size to numeric value based on device dimensions
                size = GetFontSize(size, gDmdDevice.Width, gDmdDevice.Height);

                // Check if the font exists
                string localFontPath = $"resources/{font}_{size}";
                List<string> extensions = new List<string> { ".fnt", ".png" };

                if (FileExistsWithExtensions(localFontPath, extensions, out string foundExtension))
                {
                    localFontPath = localFontPath + ".fnt";
                }
                else
                {
                    localFontPath = $"resources/Consolas_{size}.fnt";
                    LogIt($"Font not found, using default: {localFontPath}");
                }

                // Determine if border is needed
                int border = bordersize != "0" ? 1 : 0;

                System.Action displayAction = () =>
                {
                    // Create font and label actor
                    FlexDMD.Font myFont = gDmdDevice.NewFont(localFontPath, HexToColor(color), HexToColor(bordercolor), border);
                    var labelActor = (Actor)gDmdDevice.NewLabel("MyLabel", myFont, text);

                    gDmdDevice.Graphics.Clear(Color.Black);
                    _scoreDelayTimer?.Dispose();
                    _scoreBoard.Visible = false;

                    var currentActor = new Actor();
                    if (cleanbg)
                    {
                        _queue.RemoveAllScenes();
                        _loopTimer?.Dispose();
                    }

                    if (duration > 0)
                    {
                        _animationTimer?.Dispose();
                        _animationTimer = new Timer(AnimationTimer, null, (int)duration * 1000 + 1000, Timeout.Infinite);
                    }

                    // Create background scene based on animation type
                    BackgroundScene bg = CreateTextBackgroundScene(animation.ToLower(), currentActor, text, myFont, duration);

                    _queue.Visible = true;

                    // Add scene to the queue or directly to the stage
                    if (cleanbg)
                    {
                        _queue.Enqueue(bg);
                        _loopTimer?.Dispose();
                    }
                    else
                    {
                        gDmdDevice.Stage.AddActor(bg);
                    }
                };

                // Execute initial action
                gDmdDevice.Post(displayAction);

                // If loop is true, configure the timer
                if (loop)
                {
                    float waitDuration = duration * 0.85f; // 15% less than duration
                    _loopTimer = new Timer(_ =>
                    {
                        gDmdDevice.Post(displayAction);
                    }, null, (int)(waitDuration * 1000), (int)(waitDuration * 1000));
                }

                LogIt($"Rendering text: {text}");
                return true;
            }
            catch (Exception ex)
            {
                LogIt($"Error in DisplayText: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns de correct pixel size for the font depending on the DMD size (256x64 or 128x32) and the letter based size.
        /// </summary>
        private static string GetFontSize(string size, int width, int height)
        {
            size = size.ToLower();
            var sizeMapping = new Dictionary<(int, int), Dictionary<string, string>>
            {
                {
                    (128, 32), new Dictionary<string, string>
                    {
                        { "xs", "6" }, { "s", "8" }, { "m", "12" },
                        { "l", "16" }, { "xl", "24" }, { "xxl", "32" }
                    }
                },
                {
                    (256, 64), new Dictionary<string, string>
                    {
                        { "xs", "12" }, { "s", "16" }, { "m", "24" },
                        { "l", "32" }, { "xl", "48" }, { "xxl", "64" }
                    }
                }
            };

            if (sizeMapping.TryGetValue((width, height), out var sizeDict) && sizeDict.TryGetValue(size, out var newSize))
            {
                return newSize;
            }

            return sizeMapping.ContainsKey((width, height)) ? sizeDict["s"] : "8";
        }

        private static BackgroundScene CreateTextBackgroundScene(string animation, Actor currentActor, string text, FlexDMD.Font myFont, float duration)
        {
            return animation switch
            {
                "scrolldown" => new ScrollingDownScene(gDmdDevice, currentActor, text, myFont, AnimationType.None, duration, AnimationType.None),
                "scrollup" => new ScrollingUpScene(gDmdDevice, currentActor, text, myFont, AnimationType.None, duration, AnimationType.None),
                "scrollright" => new ScrollingRightScene(gDmdDevice, currentActor, text, myFont, AnimationType.None, duration, AnimationType.None),
                "scrollleft" => new ScrollingLeftScene(gDmdDevice, currentActor, text, myFont, AnimationType.None, duration, AnimationType.None),
                _ => new NoAnimationScene(gDmdDevice, currentActor, text, myFont, AnimationType.None, duration, AnimationType.None)
            };
        }



        /// <summary>
        /// Displays text or image with image or with text on the DMD device.
        /// %0A or | for line break
        /// </summary>
        public static bool AdvancedDisplay(string text, string path, string size, string color, string font, string bordercolor, string bordersize, bool cleanbg, string animationIn, string animationOut, float duration)
        {
            try
            {
                // Convert size to numeric value based on device dimensions
                size = GetFontSize(size, gDmdDevice.Width, gDmdDevice.Height);

                //Check if the font exists
                string localFontPath = $"resources/{font}_{size}";
                List<string> fontextensions = new List<string> { ".fnt", ".png" };

                if (FileExistsWithExtensions(localFontPath, fontextensions, out string foundPathExtension))
                {
                    localFontPath = localFontPath + ".fnt";
                }
                else
                {
                    localFontPath = $"resources/Consolas_{size}.fnt";
                    LogIt($"Font not found, using default: {localFontPath}");
                }

                // Determine if border is needed
                int border = bordersize != "0" ? 1 : 0;

                var bgActor = new Actor();

                if (!string.IsNullOrEmpty(path))
                {
                    
                    if(!string.IsNullOrEmpty(AppSettings.artworkPath))
                        path = AppSettings.artworkPath + "/" + path;
                    string localPath = HttpUtility.UrlDecode(path);

                    List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };

                    if (FileExistsWithExtensions(localPath, extensions, out string foundExtension))
                    {
                        string fullPath = localPath + foundExtension;

                        List<string> videoExtensions = new List<string> { ".gif", ".avi", ".mp4" };
                        List<string> imageExtensions = new List<string> { ".png", ".jpg", ".bmp" };

                        if (videoExtensions.Contains(foundExtension.ToLower()))
                        {
                            bgActor = (AnimatedActor)gDmdDevice.NewVideo("MyVideo", fullPath);
                            bgActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                        }
                        else if (imageExtensions.Contains(foundExtension.ToLower()))
                        {
                            bgActor = (Actor)gDmdDevice.NewImage("MyImage", fullPath);
                            bgActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                        }
                    }
                }

                gDmdDevice.Post(() =>
                {
                    // Create font and label actor
                    FlexDMD.Font myFont = gDmdDevice.NewFont(localFontPath, HexToColor(color), HexToColor(bordercolor), border);
                    var labelActor = (Actor)gDmdDevice.NewLabel("MyLabel", myFont, text);

                    if (cleanbg)
                    {
                        _queue.RemoveAllScenes();
                        _loopTimer?.Dispose();
                    }

                    // Create advanced scene
                    var bg = new AdvancedScene(gDmdDevice, bgActor, text, myFont,
                                               (AnimationType)Enum.Parse(typeof(AnimationType), FormatAnimationInput(animationIn)),
                                               duration,
                                               (AnimationType)Enum.Parse(typeof(AnimationType), FormatAnimationInput(animationOut)),
                                               "");

                    _queue.Visible = true;
                    gDmdDevice.Graphics.Clear(Color.Black);
                    _scoreBoard.Visible = false;
                    _scoreDelayTimer?.Dispose();

                    // Add scene to the queue or directly to the stage
                    if (cleanbg)
                    {
                        _queue.Enqueue(bg);
                        _loopTimer?.Dispose();
                    }
                    else
                    {
                        gDmdDevice.Stage.AddActor(bg);
                    }
                });

                LogIt($"Rendering text: {text}");
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Displays text or image with image or with text on the DMD device.
        /// %0A o | para salto de linea
        /// </summary>
        public static bool DisplayScoreBackground(string path)
        {
            try
            {
                
                var bgActor = new Actor();

                if (!string.IsNullOrEmpty(path))
                {
                    path = AppSettings.artworkPath + "/" + path;
                    string localPath = HttpUtility.UrlDecode(path);

                    List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };

                    if (FileExistsWithExtensions(localPath, extensions, out string foundExtension))
                    {
                        string fullPath = localPath + foundExtension;

                        List<string> videoExtensions = new List<string> { ".gif", ".avi", ".mp4" };
                        List<string> imageExtensions = new List<string> { ".png", ".jpg", ".bmp" };

                        if (videoExtensions.Contains(foundExtension.ToLower()))
                        {
                            bgActor = (AnimatedActor)gDmdDevice.NewVideo("MyVideo", fullPath);
                            bgActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                        }
                        else if (imageExtensions.Contains(foundExtension.ToLower()))
                        {
                            bgActor = (Actor)gDmdDevice.NewImage("MyImage", fullPath);
                            bgActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                        }
                    }
                }

                
                gDmdDevice.Post(() =>
                {
                    _scoreBoard.SetBackground(bgActor);
                });
                
                LogIt($"Rendering Score Background: {path}");
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Handle incoming HTTP requests
        /// </summary>
        static async Task HandleIncomingConnections(HttpListener listener)
        {
            bool runServer = true;
            while (runServer)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                string dof2dmdUrl = req.Url.ToString();
                string sResponse = "OK";
                if (dof2dmdUrl.Contains("v1/") || dof2dmdUrl.Contains("v2/"))
                {
                    LogIt($"Received request for {req.Url}");
                    sResponse = ProcessRequest(dof2dmdUrl);
                }
                // LogIt($"Response: {sResponse}");
                resp.StatusCode = 200;
                resp.ContentType = "text/plain";
                resp.ContentEncoding = Encoding.UTF8;
                byte[] responseBytes = Encoding.UTF8.GetBytes(sResponse);
                resp.ContentLength64 = responseBytes.Length;

                await resp.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                resp.Close();

                // Exit the server if requested
                if (sResponse=="exit")
                {
                    runServer = false;
                }
            }
            gDmdDevice.Run = false;
        }

        /// <summary>
        /// Convert Hex Color to Int
        /// </summary>
        public static Color HexToColor(string hexColor)
        {

            // Convert hexadecimal to integer
            Color _color = System.Drawing.ColorTranslator.FromHtml("#" + hexColor);

            return _color;
        }
        /// <summary>
        /// Check if a file with extension exists
        /// </summary>
        public static bool FileExistsWithExtensions(string filePath, List<string> extensions, out string foundExtension)
        {
            // Get the current extension of the filePath (if it exists)
            string currentExtension = Path.GetExtension(filePath).ToLower();

            // If the file already has a valid extension, check if it exists directly
            if (extensions.Contains(currentExtension))
            {
                if (File.Exists(filePath))
                {
                    foundExtension = currentExtension;
                    return true;
                }
            }

            // If no valid extension is provided, try appending valid extensions
            string fileWithoutExtension = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
            foreach (var extension in extensions)
            {
                string fullPath = fileWithoutExtension + extension;
                if (File.Exists(fullPath))
                {
                    foundExtension = extension;
                    return true;
                }
            }
            foundExtension = null;
            return false;
        }
       
        /// <summary>
        /// Parses the animation names to correct values
        /// </summary>
        public static string FormatAnimationInput(string input)
        {
            var validValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "scrollonup", "ScrollOnUp" },
                { "scrolloffup", "ScrollOffUp" },
                { "scrollonright", "ScrollOnRight" },
                { "scrolloffright", "ScrollOffRight" },
                { "scrolloffleft", "ScrollOffLeft" },
                { "scrollonleft", "ScrollOnLeft" },
                { "fadein", "FadeIn" },
                { "fadeout", "FadeOut" },
                { "scrolloffdown", "ScrollOffDown" },
                { "scrollondown", "ScrollOnDown" },
                { "fillfadein", "FillFadeIn" },
                { "fillfadeout", "FillFadeOut" },
                { "none", "None" }
            };

            return validValues.TryGetValue(input, out var formattedInput) ? formattedInput : null;
        }

        /// <summary>
        /// Clear DMD screen
        /// </summary>
        private static void Blank()
        {
            gDmdDevice.Post(() =>
            {
                LogIt("Clear DMD");
                _queue.RemoveAllScenes();
                _animationQueue.Clear();
                gDmdDevice.Graphics.Clear(Color.Black);
                gDmdDevice.Stage.RemoveAll();
                gDmdDevice.Stage.AddActor(_queue);
                gDmdDevice.Stage.AddActor(_scoreBoard);
                _scoreBoard.Visible = false;
                if (_queue.IsFinished()) _queue.Visible = false;
            });
        }

        /// <summary>
        /// Process incoming requests
        /// </summary>
        private static string ProcessRequest(string dof2dmdUrl)
        {
            dof2dmdUrl = dof2dmdUrl.Replace(" & ", "%20%26%20");    // Handle cases such as "Track & Field"
            var newUrl = new Uri(dof2dmdUrl);
            var query = HttpUtility.ParseQueryString(newUrl.Query);
            string sReturn = "OK";

            string[] urlParts = newUrl.AbsolutePath.Split('/');

            switch (urlParts[1])
            {
                case "v1":
                    switch (urlParts[2])
                    {
                        case "blank":
                            //gGameMarquee = "";
                            _loopTimer?.Dispose();
                            Blank();
                            sReturn = "OK";
                            break;
                        case "loopstop":
                            _loopTimer?.Dispose();
                            sReturn = "Scroll text stopped";
                            break;
                        case "exit":
                            Blank();
                            // Sleep 500ms
                            Thread.Sleep(500);
                            sReturn = "exit";
                            break;
                        case "version":
                            sReturn = "1.0";
                            break;
                        case "display":
                            switch (urlParts[3])
                            {
                                case "picture":
                                    //[url_prefix]/v1/display/picture?path=<image or video path>&animation=<fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<seconds>&fixed=<true|false>
                                    string picturepath = query.Get("path");
                                    string pFixed = query.Get("fixed") ?? "false";
                                    float pictureduration = float.TryParse(query.Get("duration"), out float result) ? result : 0.0f;
                                    string pictureanimation = query.Get("animation") ?? "none";
                                    bool queue;
                                    // Check if 'queue' exists in the query parameters
                                    queue = dof2dmdUrl.Contains("&queue") || dof2dmdUrl.EndsWith("?queue");

                                    if (StringComparer.OrdinalIgnoreCase.Compare(pFixed, "true") == 0)
                                    {
                                        pictureduration = -1.0f;
                                    }
                                    if (!picturepath.Contains("mameoutput"))
                                    {
                                        // This is certainly a game marquee, provided during new game
                                        // If path corresponds to an existing file, set game marquee
                                        //List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };
                                        List<string> extensions = new List<string> { ".png", ".jpg", ".bmp" };
                                        if (FileExistsWithExtensions(HttpUtility.UrlDecode(AppSettings.artworkPath + "/" + picturepath), extensions, out string foundExtension)) {
                                            SetGameMarquee(picturepath);
                                            LogIt($"Setting Game Marquee to: {picturepath}");
                                        }
                                        // Reset scores for all players
                                        for (int i = 1; i <= 4; i++)
                                            gScore[i] = 0;
                                    }
                                    bool success = DisplayPicture(picturepath, pictureduration, pictureanimation, queue);
                                    if (!success)
                                    {
                                        sReturn = $"Picture or video not found: {picturepath}";
                                    }
                                    break;
                                case "text":
                                    string text = query.Get("text") ?? "";
                                    string size = query.Get("size") ?? "M";
                                    string color = query.Get("color") ?? "FFFFFF";
                                    string font = query.Get("font") ?? "Consolas";
                                    string bordercolor = query.Get("bordercolor") ?? "000000";
                                    string bordersize = query.Get("bordersize") ?? "0";
                                    string animation = query.Get("animation") ?? "none";
                                    float textduration = float.TryParse(query.Get("duration"), out float tresult) ? tresult : 5.0f;
                                    LogIt($"Text is now set to: {text} with size {size}, color {color}, font {font}, border color {bordercolor}, border size {bordersize}, animation {animation} with a duration of {textduration} seconds");
                                    bool cleanbg;
                                    if (!bool.TryParse(query.Get("cleanbg"), out cleanbg))
                                    {
                                        cleanbg = true; // valor predeterminado si la conversión falla
                                    }
                                    bool loop;
                                    if (!bool.TryParse(query.Get("loop"), out loop))
                                    {
                                        loop = false; // valor predeterminado si la conversión falla
                                    }

                                    if (!DisplayText(text, size, color, font, bordercolor, bordersize, cleanbg, animation, textduration, loop))
                                    {
                                        sReturn = "Error when displaying text";
                                    }
                                    break;
                                case "advanced":
                                    string advtext = query.Get("text") ?? "";
                                    string advpath = query.Get("path") ?? "";
                                    string advsize = query.Get("size") ?? "M";
                                    string advcolor = query.Get("color") ?? "FFFFFF";
                                    string advfont = query.Get("font") ?? "Consolas";
                                    string advbordercolor = query.Get("bordercolor") ?? "0000FF";
                                    string advbordersize = query.Get("bordersize") ?? "0";
                                    string animationIn = query.Get("animationin") ?? "none";
                                    string animationOut = query.Get("animationout") ?? "none";
                                    float advtextduration = float.TryParse(query.Get("duration"), out float aresult) ? aresult : 5.0f;
                                    LogIt($"Advanced Text is now set to: {advtext} with size {advsize}, color {advcolor}, font {advfont}, border color {advbordercolor}, border size {advbordersize}, animation In {animationIn}, animation Out {animationOut} with a duration of {advtextduration} seconds");
                                    bool advcleanbg;
                                    if (!bool.TryParse(query.Get("cleanbg"), out advcleanbg))
                                    {
                                        cleanbg = true; // valor predeterminado si la conversión falla
                                    }

                                    if (!AdvancedDisplay(advtext, advpath, advsize, advcolor, advfont, advbordercolor, advbordersize, advcleanbg, animationIn, animationOut, advtextduration))
                                    {
                                        sReturn = "Error when displaying advanced scene";
                                    }
                                    break;
                                case "score":
                                    // [url_prefix]/v1/display/score?players=<number of players>&player=<active player>&score=<score>&cleanbg=<true|false>
                                    gActivePlayer = int.TryParse(query.Get("player"), out int parsedAPlayer) ? parsedAPlayer : gActivePlayer;
                                    gScore[gActivePlayer] = int.Parse(query.Get("score"));
                                    gNbPlayers = int.TryParse(query.Get("players"), out int parsedPlayers) ? parsedPlayers : gNbPlayers;
                                    gCredits = int.TryParse(query.Get("credits"), out int parsedCredits) ? parsedCredits : gCredits;
                                    bool sCleanbg;
                                    if (!bool.TryParse(query.Get("cleanbg"), out sCleanbg))
                                    {
                                        sCleanbg = true; // valor predeterminado si la conversión falla
                                    }

                                    if (!DisplayScore(gNbPlayers, gActivePlayer, gScore[gActivePlayer], sCleanbg, gCredits))
                                    {
                                        sReturn = "Error when displaying score board";
                                    }

                                    break;
                                case "scorebackground":
                                    //[url_prefix]/v1/display/scorebackground?path=<path>
                                    string scorebgpath = query.Get("path") ?? "";
                                    if (!DisplayScoreBackground(scorebgpath))
                                    {
                                        sReturn = "Error when displaying score board background";
                                    }
                                    break;
                                default:
                                    sReturn = "Not implemented";
                                    break;
                            }
                            break;
                        default:
                            sReturn = "Not implemented";
                            break;
                    }
                    break;
                default:
                    sReturn = "Not implemented";
                    break;
            }
            return sReturn;
        }
    }
    class BackgroundScene : Scene
    {
        private Actor _background;

        public Actor Background
        {
            get => _background;
            set
            {
                if (_background == value) return;
                if (_background != null)
                {
                    RemoveActor(_background);
                }
                _background = value;
                if (_background != null)
                {
                    AddActorAt(_background, 0);
                }
            }
        }

        public BackgroundScene(IFlexDMD flex, Actor background, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "") : base(flex, animateIn, pauseS, animateOut, id)
        {
            _background = background;
            if (_background != null) AddActor(_background);
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            _background?.SetSize(Width, Height);
        }
    }
    class NoAnimationScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;

        public NoAnimationScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "") : base(flex, background, animateIn, pauseS, animateOut, id)
        {
            _container = new Group(FlexDMD);

            AddActor(_container);
            var y = 0f;
            string[] lines = text.Split(new char[] { '\n', '|' });

            _length = pauseS;

            foreach (string line in lines)
            {
                //var txt = line.Trim();
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                label.Alignment = Alignment.Left;
                _container.AddActor(label);
            }
            _container.Height = y;
        }

        protected override void Begin()
        {
            base.Begin();
            _container.Y = (Height - _container.Height) / 2;
            
            var action1 = new FlexDMD.WaitAction(_length);
            var action2 = new FlexDMD.ShowAction(_container, false);
            var sequenceAction = new FlexDMD.SequenceAction();
            if (_length > -1)
            {
                sequenceAction.Add(action1);
                sequenceAction.Add(action2);
                
            _container.AddAction(sequenceAction);
            }
            

        }

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width) / 2;
                }
            }
        }
    }
    class AdvancedScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;

        public AdvancedScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "") : base(flex, background, animateIn, pauseS, animateOut, id)
        {
            _container = new Group(FlexDMD);
            
            AddActor(_container);
            var y = 0f;
            string[] lines = text.Split(new char[] { '\n', '|' });

            _length = pauseS;

            foreach (string line in lines)
            {
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                label.Alignment = Alignment.Left;
                _container.AddActor(label);
            }
            _container.Height = y;
        }
        protected override void Begin()
        {
            base.Begin();
            _container.Y = (Height - _container.Height) / 2;
        }
        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width) / 2;
                }
            }
        }
    }
    class ScrollingUpScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;

        public ScrollingUpScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "") : base(flex, background, animateIn, pauseS, animateOut, id)
        {
            _container = new Group(FlexDMD);

            AddActor(_container);
            var y = 0f;
            string[] lines = text.Split(new char[] { '\n', '|' });

            _length = pauseS;
            //_length = 3f + lines.Length * 0.2f;

            foreach (string line in lines)
            {
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                _container.AddActor(label);
            }
            _container.Height = y;
        }

        protected override void Begin()
        {
            base.Begin();
            _container.Y = Height;
            _tweener.Tween(_container, new { Y = -_container.Height }, _length, 0f);
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width) / 2;
                }
            }
        }
    }
    class ScrollingDownScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;

        public ScrollingDownScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "") : base(flex, background, animateIn, pauseS, animateOut, id)
        {
            _container = new Group(FlexDMD);

            AddActor(_container);
            var y = 0f;
            string[] lines = text.Split(new char[] { '\n', '|' });

            _length = pauseS;
            //_length = 3f + lines.Length * 0.2f;

            foreach (string line in lines)
            {
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                _container.AddActor(label);
            }
            _container.Height = y;
        }

        protected override void Begin()
        {
            base.Begin();
            _container.Y = -_container.Height;
            _tweener.Tween(_container, new { Y = Height * 1.02f }, _length, 0f);
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width) / 2;
                }
            }
        }
    }
    class ScrollingLeftScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;

        public ScrollingLeftScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn , float pauseS, AnimationType animateOut , string id = "") : base(flex, background, animateIn, pauseS, animateOut, id)
        {
            _container = new Group(FlexDMD);
            
            AddActor(_container);
            var y = 0f;
            string[] lines = text.Split(new char[] { '\n', '|' });

            _length = pauseS;
            //_length = 3f + lines.Length * 0.2f;

            foreach (string line in lines)
            {
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                _container.AddActor(label);
            }
            _container.Height = y;
        }

        protected override void Begin()
        {
            base.Begin();

            _container.Y = (Height - _container.Height) / 2;
            _container.X = Width;
            _tweener.Tween(_container, new { X = -(Width + Width*.1) }, _length, 0f);
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width)/2;
                }
            }
        }
    }
    class ScrollingRightScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;

        public ScrollingRightScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "") : base(flex, background, animateIn, pauseS, animateOut, id)
        {
            _container = new Group(FlexDMD);

            
            
            AddActor(_container);
            var y = 0f;
            string[] lines = text.Split(new char[] { '\n', '|' });

            _length = pauseS;
            //_length = 3f + lines.Length * 0.2f;

            foreach (string line in lines)
            {
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                _container.AddActor(label);
            }
            _container.Height = y;
        }

        protected override void Begin()
        {
            base.Begin();
            _container.Y = (Height - _container.Height) / 2;
            _container.X = -Width;
            _tweener.Tween(_container, new { X = Width + Width * .1 }, _length, 0f);
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width) / 2;
                }
            }
        }
    }

    class ScoreBoard : Group
    {
        private readonly FlexDMD.Label[] _scores = new FlexDMD.Label[4];
        private Actor _background = null;
        private int _highlightedPlayer = 0;
        private int _nplayers;
        public FlexDMD.Label _lowerLeft, _lowerRight;

        public FlexDMD.Font ScoreFont { get; private set; }
        public FlexDMD.Font HighlightFont { get; private set; }
        public FlexDMD.Font TextFont { get; private set; }

        public ScoreBoard(IFlexDMD flex, FlexDMD.Font scoreFont, FlexDMD.Font highlightFont, FlexDMD.Font textFont) : base(flex)
        {
            ScoreFont = scoreFont;
            HighlightFont = highlightFont;
            TextFont = textFont;
            _lowerLeft = new FlexDMD.Label(flex, textFont, "");
            _lowerRight = new FlexDMD.Label(flex, textFont, "");
            AddActor(_lowerLeft);
            AddActor(_lowerRight);
            for (int i = 0; i < 4; i++)
            {
                _scores[i] = new FlexDMD.Label(flex, scoreFont, "0");
                AddActor(_scores[i]);
            }
        }

        public void SetBackground(Actor background)
        {
            if (_background != null)
            {
                RemoveActor(_background);
                if (_background is IDisposable e) e.Dispose();
            }
            _background = background;
            if (_background != null)
            {
                AddActorAt(_background, 0);
            }
        }

        public void SetNPlayers(int nPlayers)
        {
            for (int i = 0; i < 4; i++)
            {
                _scores[i].Visible = i < nPlayers;
            }
            _nplayers = nPlayers;
        }

        public void SetFonts(FlexDMD.Font scoreFont, FlexDMD.Font highlightFont, FlexDMD.Font textFont)
        {
            ScoreFont = scoreFont;
            HighlightFont = highlightFont;
            TextFont = textFont;
            SetHighlightedPlayer(_highlightedPlayer);
            _lowerLeft.Font = textFont;
            _lowerRight.Font = textFont;
        }

        public void SetHighlightedPlayer(int player)
        {
            _highlightedPlayer = player;
            for (int i = 0; i < 4; i++)
            {
                if (i == player - 1)
                {
                    _scores[i].Font = HighlightFont;
                }
                else
                {
                    _scores[i].Font = ScoreFont;
                }
            }
        }

        public void SetScore(Int64 score1, Int64 score2, Int64 score3, Int64 score4)
        {
            _scores[0].Text = score1.ToString("#,##0");
            _scores[1].Text = score2.ToString("#,##0");
            _scores[2].Text = score3.ToString("#,##0");
            _scores[3].Text = score4.ToString("#,##0");
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            SetBounds(0, 0, Parent.Width, Parent.Height);
            float yText = Height - TextFont.BitmapFont.BaseHeight - 1;
            float yLine2 = (Height - TextFont.BitmapFont.BaseHeight) / 2f;
            float dec = (HighlightFont.BitmapFont.BaseHeight - ScoreFont.BitmapFont.BaseHeight) / 2f;
            _scores[0].Pack();
            _scores[1].Pack();
            _scores[2].Pack();
            _scores[3].Pack();
            _lowerLeft.Pack();
            _lowerRight.Pack();
            switch (_nplayers)
            {
                case 1:
                    _scores[0].Visible = true;
                    _scores[0].SetAlignedPosition(Width/2, (Height - (_highlightedPlayer == 1 ? 0 : dec))/2, Alignment.Center);
                    _scores[1].Visible = false;
                    _scores[2].Visible = false;
                    _scores[3].Visible = false;
                    _lowerLeft.SetAlignedPosition(1, yText, Alignment.TopLeft);
                    _lowerRight.SetAlignedPosition(Width - 1, yText, Alignment.TopRight);
                break;
                case 2:
                    _scores[0].Visible = true;
                    _scores[0].SetAlignedPosition(1, (Height - (_highlightedPlayer == 1 ? 0 : dec))/2, Alignment.Left);
                    _scores[1].Visible = true;
                    _scores[1].SetAlignedPosition(Width - 1, (Height - (_highlightedPlayer == 2 ? 0 : dec))/2, Alignment.Right);
                    _scores[2].Visible = false;
                    _scores[3].Visible = false;
                    _lowerLeft.SetAlignedPosition(1, yText, Alignment.TopLeft);
                    _lowerRight.SetAlignedPosition(Width - 1, yText, Alignment.TopRight);
                break;
                case 3:
                    _scores[0].Visible = true;
                    _scores[1].Visible = true;
                    _scores[2].Visible = true;
                    _scores[3].Visible = false;
                    _scores[0].SetAlignedPosition(1, 1 + (_highlightedPlayer == 1 ? 0 : dec), Alignment.TopLeft);
                    _scores[1].SetAlignedPosition(Width - 1, 1 + (_highlightedPlayer == 2 ? 0 : dec), Alignment.TopRight);
                    _scores[2].SetAlignedPosition(Width/2, Height/5.2f + yLine2 + (_highlightedPlayer == 3 ? 0 : dec)  , Alignment.Center);
                    _lowerLeft.SetAlignedPosition(1, yText, Alignment.TopLeft);
                    _lowerRight.SetAlignedPosition(Width - 1, yText, Alignment.TopRight);
                break;
                case 4:
                    _scores[0].Visible = true;
                    _scores[1].Visible = true;
                    _scores[2].Visible = true;
                    _scores[3].Visible = true;
                    _scores[0].SetAlignedPosition(1, 1 + (_highlightedPlayer == 1 ? 0 : dec), Alignment.TopLeft);
                    _scores[1].SetAlignedPosition(Width - 1, 1 + (_highlightedPlayer == 2 ? 0 : dec), Alignment.TopRight);
                    _scores[2].SetAlignedPosition(1, yLine2 + (_highlightedPlayer == 3 ? 0 : dec), Alignment.TopLeft);
                    _scores[3].SetAlignedPosition(Width - 1, yLine2 + (_highlightedPlayer == 4 ? 0 : dec), Alignment.TopRight);
                    _lowerLeft.SetAlignedPosition(1, yText, Alignment.TopLeft);
                    _lowerRight.SetAlignedPosition(Width - 1, yText, Alignment.TopRight);
                break;
            }
        }

        public override void Draw(Graphics graphics)
        {
            if (Visible)
            {
                _background?.SetSize(Width, Height);
                base.Draw(graphics);
            }
        }
    }
}
