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
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
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
using System.Linq;


namespace DOF2DMD
{
    class DOF2DMD
    {
        public static FlexDMD.FlexDMD gDmdDevice;
        public static int[] gScore = [0, 0, 0, 0, 0];
        public static int gActivePlayer = 1;
        public static int gNbPlayers = 1;
        public static int gCredits = 1;
        public static string gGameMarquee = "DOF2DMD";
        private static Timer _scoreTimer;
        private static Timer _animationTimer;
        private static Timer _attractTimer;
        private static bool _AttractModeAlternate = true;
        private static string _currentAttractGif = null;
        private static readonly object _scoreQueueLock = new object();
        private static readonly object _animationQueueLock = new object();
        private static readonly object sceneLock = new object();
        private static Sequence _queue;
        private static Timer attractTimer;
        private static readonly object attractTimerLock = new object();
        private const int InactivityDelay = 30000; // 30 seconds in milliseconds
        private static string[] gGifFiles;


        public static ScoreBoard _scoreBoard;

        static void Main()
        {
            // Set up logging to a file
            Trace.Listeners.Add(new TextWriterTraceListener("debug.log") { TraceOutputOptions = TraceOptions.Timestamp });
            Trace.AutoFlush = true;

            // Initializing DMD
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

            _queue = new Sequence(gDmdDevice);
            _queue.FillParent = true;

            //DMDScene = (Group)gDmdDevice.NewGroup("Scene");

            FlexDMD.Font _scoreFontText;
            FlexDMD.Font _scoreFontNormal;
            FlexDMD.Font _scoreFontHighlight;

            // UltraDMD uses f4by5 / f5by7 / f6by12
            if(gDmdDevice.Height == 64 && gDmdDevice.Width == 256)
            {
                _scoreFontText = gDmdDevice.NewFont("FlexDMD.Resources.udmd-f6by12.fnt", Color.FromArgb(168, 168, 168), Color.Black,1);
                _scoreFontNormal = gDmdDevice.NewFont("FlexDMD.Resources.udmd-f7by13.fnt", Color.FromArgb(168, 168, 168), Color.Black,1);
                _scoreFontHighlight = gDmdDevice.NewFont("FlexDMD.Resources.udmd-f12by24.fnt", Color.White, Color.Black,1);
            }
            else
            {
            
                _scoreFontText = gDmdDevice.NewFont("FlexDMD.Resources.udmd-f4by5.fnt", Color.FromArgb(168, 168, 168), Color.Black,1);
                _scoreFontNormal = gDmdDevice.NewFont("FlexDMD.Resources.udmd-f5by7.fnt", Color.FromArgb(168, 168, 168), Color.Black,1);
                _scoreFontHighlight = gDmdDevice.NewFont("FlexDMD.Resources.udmd-f6by12.fnt", Color.White, Color.Black,1);    
            }

            _scoreBoard = new ScoreBoard(
                gDmdDevice,
                _scoreFontNormal,
                _scoreFontHighlight,
                _scoreFontText
                )
            { Visible = false };

            

            gDmdDevice.Stage.AddActor(_queue);
            gDmdDevice.Stage.AddActor(_scoreBoard);

            // Display start picture as game marquee
            gGameMarquee = AppSettings.StartPicture;

            Thread.Sleep(500);
            DisplayPicture(gGameMarquee, -1, "none");


            // Start the http listener
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"{AppSettings.UrlPrefix}/");
            listener.Start();

            Trace.WriteLine($"DOF2DMD is now listening for requests on {AppSettings.UrlPrefix}...");

            // Initialize and start the attract timer
            gGifFiles = Directory.GetFiles(AppSettings.artworkPath, "*.gif", SearchOption.AllDirectories);
            _attractTimer = new Timer(AttractTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            Task listenTask = HandleIncomingConnections(listener);
            listenTask.GetAwaiter().GetResult();
        }

        private static void ResetAttractTimer()
        {
            lock (attractTimerLock)
            {
                if (attractTimer == null)
                {
                    attractTimer = new Timer(StartAttractMode, null, InactivityDelay, Timeout.Infinite);
                }
                else
                {
                    attractTimer.Change(InactivityDelay, Timeout.Infinite);
                }
            }
        }

        private static void StartAttractMode(object state)
        {
            // Your existing attract mode logic goes here
            SelectRandomGif();
            // Add any other attract mode initialization code
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        private static void AttractTimer(object state)
        {
            LogIt("⏱️ AttractTimer");
            AttractAction();
        }

        private static void AttractAction()
        {
            DateTime now = DateTime.Now;
            string currentTime = now.ToString("HH:mm:ss");

            // Toggle the display mode every 10 seconds
            if (now.Second % 10 == 0)
            {
                _AttractModeAlternate = !_AttractModeAlternate;

                // If switching to GIF mode, select a new random GIF
                if (!_AttractModeAlternate)
                {
                    SelectRandomGif();
                    if (_currentAttractGif != null)
                    {
                        DisplayPicture(_currentAttractGif, -1, "none");
                    }
                }
            }

            if (_AttractModeAlternate)
            {
                DisplayText(currentTime, "XL", "FFFFFF", "WhiteRabbit", "00FF00", "1", true, "None", 1);
            }

        }


        private static void SelectRandomGif()
        {
            if (gGifFiles.Length > 0)
            {
                Random random = new Random();
                int randomIndex = random.Next(gGifFiles.Length);
                string fullPath = gGifFiles[randomIndex];
                string parentFolder = Path.GetFileName(Path.GetDirectoryName(fullPath));
                string fileName = Path.GetFileNameWithoutExtension(fullPath);
                _currentAttractGif = Path.Combine(parentFolder, fileName);
                Console.WriteLine($"Random GIF: {_currentAttractGif}");
            }
            else
            {
                _currentAttractGif = null;
            }
        }

        /// <summary>
        /// Callback method once animation is finished.
        /// Displays the player's score
        /// </summary>
        private static void AnimationTimer(object state)
        {
            _animationTimer.Dispose();
            if (AppSettings.ScoreDmd != 0)
            {
                LogIt("⏱️ AnimationTimer: now display score");
                if (gScore[gActivePlayer] > 0)
                {
                    DisplayScoreboard(gNbPlayers, gActivePlayer, gScore[1], gScore[2], gScore[3], gScore[4], $"Player {gActivePlayer}", $"Credits {gCredits}", true);
                }
            }
        }

        /// <summary>
        /// This method is a callback for a timer that displays the current score.
        /// It then calls the DisplayPicture method to show the game marquee picture.
        /// </summary>
        private static void ScoreTimer(object state)
        {
            LogIt("⏱️ ScoreTimer");
            lock (_scoreQueueLock)
            {
                try
                {
                    DisplayPicture(gGameMarquee, -1, "none");
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
                Trace.WriteLine(message);
            }
        }
        public static Boolean DisplayScore(int cPlayers, int player, int score, bool sCleanbg, int credits)
        {
            gScore[player] = score;
            gActivePlayer = player;
            gNbPlayers = cPlayers;
            gCredits = credits;
            LogIt($"DisplayScore for player {player}: {score}");
            DisplayScoreboard(gNbPlayers, player, gScore[1], gScore[2], gScore[3], gScore[4], $"PLAYER {gActivePlayer}", $"Credits {gCredits}", sCleanbg);

            if (AppSettings.ScoreDmd != 0)
            {
                _scoreTimer?.Dispose();
                _scoreTimer = new Timer(ScoreTimer, null, AppSettings.displayScoreDuration * 1000, Timeout.Infinite);
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
                Trace.WriteLine($"  Error occurred while genering the Score Board. {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Displays an image or video file on the DMD device using native FlexDMD capabilities.
        /// </summary>
        public static bool DisplayPicture(string path, float duration, string animation)
        {
            LogIt($"📷 {path}, {duration}, {animation}");
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                if (!string.IsNullOrEmpty(AppSettings.artworkPath))
                    path = AppSettings.artworkPath + "/" + path;
                string localPath = HttpUtility.UrlDecode(path);

                // List of possible extensions in order of priority
                List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };

                if (FileExistsWithExtensions(localPath, extensions, out string foundExtension))
                {
                    string fullPath = localPath + foundExtension;
                    bool isVideo = new List<string> { ".gif", ".avi", ".mp4" }.Contains(foundExtension.ToLower());
                    bool isImage = new List<string> { ".png", ".jpg", ".bmp" }.Contains(foundExtension.ToLower());

                    if (isVideo || isImage)
                    {
                        gDmdDevice.Post(() =>
                        {
                            gDmdDevice.Clear = true;


                            // Liberar recursos existentes
                            if (_queue.ChildCount >= 1)
                            {
                                _queue.RemoveAllScenes();
                            }
                            //gDmdDevice.LockRenderThread();
                            gDmdDevice.Graphics.Clear(Color.Black);
                            _scoreBoard.Visible = false;
                            Actor mediaActor = isVideo ? (Actor)gDmdDevice.NewVideo("MyVideo", fullPath) : (Actor)gDmdDevice.NewImage("MyImage", fullPath);
                            mediaActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);

                            // Only process if not a fixed duration (-1)
                            if (duration > -1)
                            {
                                // Adjust duration for videos and images if not explicitly set
                                // For image, set duration to infinite (-1)
                                duration = (isVideo && duration == 0) ? ((AnimatedActor)mediaActor).Length :
                                           (isImage && duration == 0) ? -1 : duration;

                                if (isVideo)
                                {
                                    // Arm timer to restore to score, once animation is done playing
                                    _animationTimer?.Dispose();
                                    _animationTimer = new Timer(AnimationTimer, null, (int)duration * 1000 + 1000, Timeout.Infinite);
                                }
                            }

                            BackgroundScene bg = CreateBackgroundScene(gDmdDevice, mediaActor, animation.ToLower(), duration);

                            _queue.Visible = true;
                            _queue.Enqueue(bg);


                            //gDmdDevice.UnlockRenderThread();

                        });

                        LogIt($"Rendering {(isVideo ? "video" : "image")}: {fullPath}");
                        return true;
                    }
                    Trace.WriteLine($"File not found: {localPath}");

                    return false;
                }
                LogIt($"❗ Picture not found {localPath}");
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error occurred while fetching the image. {ex.Message}");
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
        public static bool DisplayText(string text, string size, string color, string font, string bordercolor, string bordersize, bool cleanbg, string animation, float duration)
        {
            try
            {
                // Convert size to numeric value based on device dimensions
                size = GetFontSize(size, gDmdDevice.Width, gDmdDevice.Height);

                //Check if the font exists
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

                
                gDmdDevice.Post(() =>
                {
                    // Create font and label actor
                    FlexDMD.Font myFont = gDmdDevice.NewFont(localFontPath, HexToColor(color), HexToColor(bordercolor), border);
                    var labelActor = (Actor)gDmdDevice.NewLabel("MyLabel", myFont, text);
                    
                    gDmdDevice.Graphics.Clear(Color.Black);
                     _scoreBoard.Visible = false;
                    
                    var currentActor = new Actor();
                    if (cleanbg)
                    {
                        _queue.RemoveAllScenes();
                    }
                    if (duration > -1)
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
                    
                    // Add scene to the queue or directly to the stage
                    if (cleanbg)
                    {
                        _queue.Enqueue(bg);
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
                    //sResponse = ProcessRequest(dof2dmdUrl);
                    sResponse = ProcessRequest(dof2dmdUrl);
                }
                // Answer is 200 OK anyhow, as it may block misbehaving processes
                resp.StatusCode = 200;
                resp.ContentType = "text/plain";
                resp.ContentEncoding = Encoding.UTF8;
                byte[] responseBytes = Encoding.UTF8.GetBytes(sResponse);
                resp.ContentLength64 = responseBytes.Length;
                using (Stream output = resp.OutputStream)
                {
                    output.Write(responseBytes, 0, responseBytes.Length);
                }
                resp.Close();
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
        public static bool FileExistsWithExtensions(string fileNameWithoutExtension, List<string> extensions, out string foundExtension)
        {
            foreach (var extension in extensions)
            {
                string fullPath = fileNameWithoutExtension + extension;
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
        /// Process incoming requests
        /// </summary>
        private static string ProcessRequest(string dof2dmdUrl)
        {
            var newUrl = new Uri(dof2dmdUrl);
            var query = HttpUtility.ParseQueryString(newUrl.Query);
            string sReturn = "OK";

            string[] urlParts = newUrl.AbsolutePath.Split('/');

            // Reset attract timer
            ResetAttractTimer();

            switch (urlParts[1])
            {
                case "v1":
                    switch (urlParts[2])
                    {
                        case "blank":
                            gGameMarquee = "";


                            gDmdDevice.Post(() =>
                            {
                                LogIt("Cancel Rendering");
                                _queue.RemoveAllScenes();
                                gDmdDevice.Graphics.Clear(Color.Black);
                                gDmdDevice.Stage.RemoveAll();
                                gDmdDevice.Stage.AddActor(_queue);
                                gDmdDevice.Stage.AddActor(_scoreBoard);
                                _scoreBoard.Visible = false;
                                if (_queue.IsFinished()) _queue.Visible = false;
                            });
                            sReturn = "Marquee cleared";
                            break;
                        case "exit":
                            Environment.Exit(0);
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
                                    LogIt($"Query {query.Count}, Picture is now set to: {picturepath} with animation {pictureanimation} with a duration of {pictureduration} seconds");
                                    if (StringComparer.OrdinalIgnoreCase.Compare(pFixed, "true") == 0)
                                    {
                                        pictureduration = -1.0f;
                                    }
                                    if ((query.Count == 2) && (pictureduration == -1.0f))
                                    {
                                        // This is certainly a game marquee, provided during new game
                                        // If path corresponds to an existing file, set game marquee
                                        List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };
                                        if (FileExistsWithExtensions(HttpUtility.UrlDecode(AppSettings.artworkPath + "/" + picturepath), extensions, out string foundExtension)) {
                                            gGameMarquee = picturepath;
                                        }
                                        // Reset scores for all players
                                        for (int i = 1; i <= 4; i++)
                                            gScore[i] = 0;
                                    }
                                    if (!DisplayPicture(picturepath, pictureduration, pictureanimation))
                                    {
                                        sReturn = $"Picture or video not found: {picturepath}";
                                    }
                                    else
                                    {
                                        sReturn = "OK";
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
                                    LogIt($"Text is now set to: {text} with size {size} ,color {color} ,font {font} ,border color {bordercolor}, border size {bordersize}, animation {animation} with a duration of {textduration} seconds");
                                    bool cleanbg;
                                    if (!bool.TryParse(query.Get("cleanbg"), out cleanbg))
                                    {
                                        cleanbg = true; // valor predeterminado si la conversión falla
                                    }

                                    if (DisplayText(text, size, color, font, bordercolor, bordersize, cleanbg, animation, textduration))
                                    {
                                        sReturn = "OK";
                                    }
                                    else
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
                                    LogIt($"Advanced Text is now set to: {advtext} with size {advsize} ,color {advcolor} ,font {advfont} ,border color {advbordercolor}, border size {advbordersize}, animation In {animationIn}, animation Out {animationOut} with a duration of {advtextduration} seconds");
                                    bool advcleanbg;
                                    if (!bool.TryParse(query.Get("cleanbg"), out advcleanbg))
                                    {
                                        cleanbg = true; // valor predeterminado si la conversión falla
                                    }

                                    if (AdvancedDisplay(advtext, advpath, advsize, advcolor, advfont, advbordercolor, advbordersize, advcleanbg, animationIn, animationOut, advtextduration))
                                    {
                                        sReturn = "OK";
                                    }
                                    else
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

                                    if (DisplayScore(gNbPlayers, gActivePlayer, gScore[gActivePlayer], sCleanbg, gCredits))
                                    {
                                        sReturn = "Ok";
                                    }
                                    else
                                    {
                                        sReturn = "Error when displaying score board";
                                    }

                                    break;
                                case "scorebackground":
                                    //[url_prefix]/v1/display/scorebackground?path=<path>
                                    string scorebgpath = query.Get("path") ?? "";
                                    if (DisplayScoreBackground(scorebgpath))
                                    {
                                        sReturn = "Ok";
                                    }
                                    else
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
            _tweener.Tween(_container, new { X = -Width }, _length, 0f);
        }

        public override void Update(float delta)
        {
            base.Update(delta);
            if (_container.Width != Width)
            {
                _container.Width = Width;
                foreach (Actor line in _container.Children)
                {
                    line.X = (Width - line.Width) /2;
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
            _tweener.Tween(_container, new { X = Width }, _length, 0f);
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
