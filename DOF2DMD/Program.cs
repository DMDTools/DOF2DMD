
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
using System.Security.Cryptography;

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
        private static Scene _currentScene = null;
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
        private static readonly object _textQueueLock = new object();
        private static readonly object _scoreBoardLock = new object();
        private static readonly object _scoreDelayTimerLock = new object();
        private static readonly object _currentSceneLock = new object();
        private static readonly object _animationTimersLock = new object();
        private static Sequence _SequenceQueue;
        private static List<AnimationTimerInfo> _animationTimers = new List<AnimationTimerInfo>();


        public static ScoreBoard _scoreBoard;
        // Animation item for the queue
        private class QueueItem
        {
            public string Path { get; set; }
            public float Duration { get; set; } 
            public string Animation { get; set; }
            public string Text { get; set; }
            public string Size  { get; set; }
            public string Color  { get; set; }
            public string Font  { get; set; }
            public string Bordercolor  { get; set; }
            public int Bordersize  { get; set; }
            public bool Cleanbg  { get; set; }
            public float Wait  { get; set; }
            public int Xpos  { get; set; }
            public int Ypos  { get; set; }
            public string Scale  { get; set; }
            public string Align  { get; set; }
            public float PlaySpeed  { get; set; }
            public float Pause  { get; set; }
            public bool Loop  { get; set; }
                
            public QueueItem(string path, float duration, string animation, bool cleanbg, float wait, int xpos, int ypos, string scale, string align, float playSpeed, float pause)
            {
                Path = path;
                Duration = duration;
                Animation = animation;
                Text = string.Empty;
                Size = string.Empty;
                Color = string.Empty;
                Font = string.Empty;
                Bordercolor = string.Empty;
                Bordersize = 0;
                Cleanbg = cleanbg;
                Wait = wait;
                Xpos = xpos;
                Ypos = ypos;
                Scale = scale;
                Align = align;
                PlaySpeed = playSpeed;
                Pause = pause;
            }
            public QueueItem(string text, string size, string color, string font, string bordercolor, int bordersize, bool cleanbg, string animation, float duration, bool loop, float wait, string align, float pause)
            {
                Path = string.Empty;
                Text = text;
                Size = size;
                Color = color;
                Font = font;
                Bordercolor = bordercolor;
                Bordersize = bordersize;
                Duration = duration;
                Animation = animation;
                Cleanbg = cleanbg;
                Loop = loop;
                Wait = wait;
                Align = align;
                Pause = pause;
                Xpos = 0;
                Ypos = 0;
                Scale = string.Empty;
                PlaySpeed = 1.0f;
            }
        }
        private static Queue<QueueItem> _animationQueue = new Queue<QueueItem>();
        private static float _totalDuration;
        
        private static Timer _scoreDelayTimer;

        static async Task Main()
        {
            // Set up logging to a file
            Console.OutputEncoding = Encoding.UTF8;
            Trace.Listeners.Add(new TextWriterTraceListener("dof2dmd.log") { TraceOutputOptions = TraceOptions.Timestamp });
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;

            LogIt($"🏁Starting DOF2DMD v{Assembly.GetExecutingAssembly().GetName().Version}...");
            LogIt("🚦Starting HTTP listener");
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"{AppSettings.UrlPrefix}/");
            listener.Start();
            LogIt($"👂DOF2DMD is now listening for requests on {AppSettings.UrlPrefix}...");

            // Initialize DMD in parallel
            LogIt("🏁Starting DMD initialization");
            var dmdInitTask = Task.Run(() => InitializeDMD());

            // Start handling HTTP connections
            LogIt("🚦Starting HTTP connection handler");
            var listenTask = HandleIncomingConnections(listener);

            // Wait for DMD initialization to complete
            LogIt("⌚Waiting for DMD initialization to complete");
            await dmdInitTask;

            // Wait for the HTTP listener
            LogIt("👌DOF2DMD now fully initialized!");
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
            _SequenceQueue = new Sequence(gDmdDevice) { FillParent = true };

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
            gDmdDevice.Stage.AddActor(_SequenceQueue);
            gDmdDevice.Stage.AddActor(_scoreBoard);

            // Set and display game marquee
            SetGameMarquee(AppSettings.StartPicture);
            DisplayPicture(GetGameMarquee(), -1, "none", false, true);
        }

        private static (FlexDMD.Font TextFont, FlexDMD.Font NormalFont, FlexDMD.Font HighlightFont) InitializeFonts(
            FlexDMD.FlexDMD device, Color grayColor)
        {
            // Font configurations
            var fontConfig = new[]
            {
                new { Path = "", ForeColor = Color.Black } // Generic inicialization
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
                    new { Path = "FlexDMD.Resources.udmd-f5by7.fnt", ForeColor = grayColor },
                    new { Path = "FlexDMD.Resources.udmd-f6by12.fnt", ForeColor = Color.Orange }
                };
            }
            return (
                TextFont: device.NewFont(fontConfig[0].Path, fontConfig[0].ForeColor, Color.Black, 1),
                NormalFont: device.NewFont(fontConfig[1].Path, fontConfig[1].ForeColor, Color.Black, 1),
                HighlightFont: device.NewFont(fontConfig[2].Path, fontConfig[2].ForeColor, Color.Red, 1)
            );
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
            public static bool hi2txt_enabled => Boolean.Parse(_configuration["hi2txt_enabled"] ?? "false");
            public static string hi2txt_path => _configuration["hi2txt_path"] ?? "c:\\hi2txt";
            public static string mame_path => _configuration["mame_path"] ?? "c:\\mame";
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

	/// <summary>
        /// Extract the list of actors from a scene
        /// </summary>
        private static List<Actor> GetAllActors(object parent)
        {
            List<Actor> actors = new List<Actor>();

            if (parent is Group group)
            {
                foreach (Actor child in group.Children)
                {
                    actors.AddRange(GetAllActors(child)); // Recursive call
                }
            }
            else if (parent is Actor actor)
            {
                actors.Add(actor);
            }

            return actors;
        }

        private class AnimationTimerInfo
        {
            public Timer Timer { get; set; }
            public BackgroundScene Scene { get; set; }

            public AnimationTimerInfo(Timer timer, BackgroundScene scene)
            {
                Timer = timer;
                Scene = scene;
            }
        }

        /// <summary>
        /// Callback method once animation is finished.
        /// Displays the player's score
        /// </summary>
        private static void AnimationTimer(object state)
        {
            LogIt("⏱️ ⏳AnimationTimer: Starting...");
    List<AnimationTimerInfo> expiredWaitTimers = new List<AnimationTimerInfo>();
    List<AnimationTimerInfo> expiredQueueTimers = new List<AnimationTimerInfo>();
    lock (_animationTimersLock)
    {
        // Separar temporizadores de wait y queue
        expiredWaitTimers = _animationTimers.Where(info => info.Scene == null).ToList();
        expiredQueueTimers = _animationTimers.Where(info => info.Scene != null && info.Scene.Time >= info.Scene.Pause).ToList();

        // Procesar temporizadores de wait
        foreach (var info in expiredWaitTimers)
        {
            LogIt($"⏱️ ⏳AnimationTimer: Wait Timer is done, removing it from the list");
            info.Timer.Dispose();
            _animationTimers.Remove(info);
        }

        // Procesar temporizadores de queue
        foreach (var info in expiredQueueTimers)
        {
            LogIt($"⏱️ ⏳AnimationTimer: Animation {info.Scene.Name} is done, removing it from the list");
            info.Timer.Dispose();
            _animationTimers.Remove(info);
        }

        var localQueue = _animationQueue.ToList();
        var localAnimationTimer = _animationTimers.Where(info => info.Scene != null).ToList();

        // Procesar la cola si no hay temporizadores de wait activos
        if (localQueue.Count > 0 && _animationTimers.Count(info => info.Scene == null) == 0) // Cambio importante: procesar si no hay timers wait
        {
            QueueItem item;
            item = _animationQueue.Dequeue();
			
                    if (!string.IsNullOrEmpty(item.Path))
                    {
                        LogIt($"⏱️ ⏳AnimationTimer: animation done, I will play {item.Path} next");
                    }
                    else if (!string.IsNullOrEmpty(item.Text))
                    {
                        LogIt($"⏱️ ⏳AnimationTimer: animation done, I will show {item.Text} next");
                    }

                    if (!string.IsNullOrEmpty(item.Path))
                    {
                        DisplayPicture(item.Path, item.Duration, item.Animation, false, item.Cleanbg, item.Wait, item.Xpos, item.Ypos, item.Scale, item.Align, item.PlaySpeed, item.Pause); 
                    }
                    else if (!string.IsNullOrEmpty(item.Text))
                    {
                        DisplayText(item.Text, item.Size, item.Color, item.Font, item.Bordercolor, item.Bordersize, item.Cleanbg, item.Animation, item.Duration, item.Loop, false, item.Wait, item.Align, item.Pause);
                    }
                }
                
                else if (AppSettings.ScoreDmd != 0  )
                {
                    LogIt("⏱️ AnimationTimer: previous animation is done, no more animation queued, starting 1s delay before score");

                    // Dispose existing delay timer if any
                    lock (_scoreDelayTimerLock)
                    {
                        _scoreDelayTimer?.Dispose();
                        // Create new timer with 1 second delay
                        _scoreDelayTimer = new Timer(DelayedScoreDisplay, null, 1000, Timeout.Infinite);
                    }
                }
                else if (_animationQueue.Count == 0 && _animationTimers.Count == 0)
                {
                    LogIt($"⏱️ ⏳Animation queue is now empty");
                }

                LogIt($"⏱️ AnimationTimer: Current Actors on the scene ({gDmdDevice.Stage.ChildCount - 1}): {string.Join(", ", GetAllActors(gDmdDevice.Stage).Select(actor => actor.Name))}");
                LogIt($"⏳ AnimationTimer: Current Actors on the animation Timer ({localAnimationTimer.Count}): {string.Join(", ", localAnimationTimer.Select(timerInfo => timerInfo.Scene.Name))}");
                LogIt($"⏳ AnimationTimer: Current Actors enqueued ({localQueue.Count}): {string.Join(", ", localQueue.Select(i => !string.IsNullOrEmpty(i.Text) ? i.Text : i.Path).Where(text => !string.IsNullOrEmpty(text)))}");
            }
        }

        private static void DelayedScoreDisplay(object state)
        {
            _scoreDelayTimer?.Dispose();
            _scoreDelayTimer = null;
            // Check if we still want to display the score (no new animations queued)
            if (_animationQueue.Count == 0 && AppSettings.ScoreDmd != 0)
            {
                LogIt($"⏱️ DelayedScoreDisplay: delay complete, displaying Player {gActivePlayer} score: {gScore[gActivePlayer]}");
                if (gScore[gActivePlayer] > 0)
                {
                    lock (_scoreBoardLock)
                    {
                        DisplayScore(gNbPlayers, gActivePlayer, gScore[gActivePlayer], false, gCredits);
                    }
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
                    DisplayPicture(GetGameMarquee(), -1, "none", false, true);

                }
                finally
                {
                    // Ensure that the timer is not running
                    _scoreTimer?.Dispose();
                }
            }
        }
	    
        public static Boolean DisplayScore(int cPlayers, int player, int score, bool sCleanbg, int credits)
        {
            lock (_scoreBoardLock)
            {
                gScore[player] = score;
                gActivePlayer = player;
                gNbPlayers = cPlayers;
                gCredits = credits;
                _scoreDelayTimer?.Dispose();
                // If no ongoing animation or we can display score over it
                if (_animationTimer == null || sCleanbg == false || _totalDuration == -1)
                {
                    LogIt($"DisplayScore for player {player}: {score}");
                    DisplayScoreboard(gNbPlayers, player, gScore[1], gScore[2], gScore[3], gScore[4], "", "", sCleanbg);
                }
                return true;
            }

        }
	    
        /// <summary>
        /// Displays the Score Board on the DMD device using native FlexDMD capabilities.
        /// </summary>
        public static bool DisplayScoreboard(int cPlayers, int highlightedPlayer, Int64 score1, Int64 score2, Int64 score3, Int64 score4, string lowerLeft, string lowerRight, bool cleanbg)
        {
            try
            {

                _SequenceQueue.Visible = !cleanbg;

                //gDmdDevice.LockRenderThread();
                lock (_scoreBoardLock)
                { 
                    gDmdDevice.Post(() =>
                    {

                        _scoreBoard.SetNPlayers(cPlayers);
                        _scoreBoard.SetHighlightedPlayer(highlightedPlayer);
                        _scoreBoard.SetScore(score1, score2, score3, score4);
                        _scoreBoard._lowerLeft.Text = lowerLeft;
                        _scoreBoard._lowerRight.Text = lowerRight;

                        _scoreBoard.Visible = true;


                    });
                }
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
                LogIt($"❌ Error in fuzzy matching: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Displays an image or video file on the DMD device using native FlexDMD capabilities.
        /// </summary>
        public static bool DisplayPicture(string path, float duration, string animation, bool toQueue, bool cleanbg, float wait = 0f, int xpos = 0, int ypos = 0, string scale = "fit", string align = "center", float playSpeed = 1.0f, float pause = 0f)    
        {
            try
            {
                _totalDuration = duration + pause;
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
                //    extensions = new List<string> { ".png", ".jpg", ".bmp", ".jpeg" };  ///Lo comento, no tiene sentido, las marquees pueden ser animadas
                    LogIt($"Setting marquee to: {path}");
                }
                //else
                //{
                    // List of possible extensions for other
                    extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp", ".apng", ".jpeg" };
                //}

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
                bool isVideo = new List<string> { ".gif", ".avi", ".mp4", ".apng" }.Contains(foundExtension.ToLower());
                bool isImage = new List<string> { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(foundExtension.ToLower());
                if (!isVideo && !isImage)
                {
                    return false;
                }
                // If this picture needs to be queued AND there is an animation/text running BUT current animation/text is not meant to be infinite, 
                // then add this picture and its parameters to the animation queue. The animation timer will take care of it
                
                if (toQueue && (_totalDuration > 0 || _totalDuration == -1))
                {
                    lock (_animationQueueLock)
                    {
                        LogIt($"⏳Queuing {path} for display after current animation");
                        _animationQueue.Enqueue(new QueueItem(path, duration, animation, cleanbg, wait, xpos, ypos, scale, align, playSpeed, pause));
                        LogIt($"⏳Queue has {_animationQueue.Count} items: {string.Join(", ", _animationQueue.Select(i => i.Path))}");
                        return true;
                    }
                }
                if (wait > 0)
                {
                    lock (_animationQueueLock)
                    {
                        LogIt($"⏳Queuing {path} for display after {wait} seconds wait");
                        _animationQueue.Enqueue(new QueueItem(path, duration, animation, cleanbg, 0, xpos, ypos, scale, align, playSpeed, pause));
                        LogIt($"⏳Queue has {_animationQueue.Count} items: {string.Join(", ", _animationQueue.Select(i => i.Path))}");
                    }
                    // Crear un temporizador para esperar el tiempo de espera.
                    var animationTimer = new Timer(AnimationTimer, null, (int)wait * 1000, Timeout.Infinite);
                    lock (_animationTimers)
                    {
                        _animationTimers.Add(new AnimationTimerInfo(animationTimer, null)); // Usar null para indicar que este temporizador es para wait.
                    }
                    return true;
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

                    System.Action displayAction = () =>
                    {
                        gDmdDevice.Clear = true;
                        try
                        {
                            // Clear existing resources if cleanbg=true and not queued
                            if (cleanbg && !toQueue)
                            {
                                _SequenceQueue.RemoveAllScenes();
                                _animationQueue.Clear();
                                gDmdDevice.Graphics.Clear(Color.Black);
                                gDmdDevice.Stage.RemoveAll();
                                gDmdDevice.Stage.AddActor(_SequenceQueue);
                                gDmdDevice.Stage.AddActor(_scoreBoard);
                                _scoreBoard.Visible = false;
                                _loopTimer?.Dispose();
                                lock (_animationQueueLock)
                                {
                                    _animationQueue.Clear();
                                    LogIt($"⏳Animation queue and all scenes cleared because cleanbg=true (immediate display)");
                                }
                            }

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
                        var scalingMap = new Dictionary<string, Scaling>(StringComparer.OrdinalIgnoreCase)
                        {
                            {"none", Scaling.None},
                            {"fit", Scaling.Fit},
                            {"stretch", Scaling.Stretch},
                            {"fill", Scaling.Fill},
                            {"fillx", Scaling.FillX},
                            {"filly", Scaling.FillY},
                            {"stretchx", Scaling.StretchX},
                            {"stretchy", Scaling.StretchY}
                        };

                        Scaling scaleValue = scalingMap.TryGetValue(scale, out var scaling) 
                            ? scaling 
                            : Scaling.Fit; // ← Asignará Fit porque "invalid_value" no existe
                        
                        var alignmentMap = new Dictionary<string, Alignment>(StringComparer.OrdinalIgnoreCase)
                        {
                            {"center", Alignment.Center},
                            {"right", Alignment.Right},
                            {"left", Alignment.Left},
                            {"top", Alignment.Top},
                            {"bottom", Alignment.Bottom},
                            {"topright", Alignment.TopRight},
                            {"topleft", Alignment.TopLeft},
                            {"bottomright", Alignment.BottomRight},
                            {"bottomleft", Alignment.BottomLeft}
                        };

                        Alignment alignmentValue = alignmentMap.TryGetValue(align, out var alignment) 
                            ? alignment
                            : Alignment.Center; // Default

                        try
                        {
                            mediaActor = isVideo ?
                                (Actor)gDmdDevice.NewVideo("Video: " + path, fullPath) :
                                (Actor)gDmdDevice.NewImage("Image: " + path, fullPath);

                            //mediaActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                            // Asignar Scaling después de castear al tipo correcto
                            if (isVideo)
                            {
                                ((IVideoActor)mediaActor).Scaling = scaleValue;
                                ((IVideoActor)mediaActor).Alignment = alignmentValue;
                                ((IVideoActor)mediaActor).PlaySpeed = playSpeed;
                            }
                            else
                            {
                                ((IImageActor)mediaActor).Scaling = scaleValue;
                                ((IImageActor)mediaActor).Alignment = alignmentValue;
                            }
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
                        else
                        {
                            mediaActor.SetPosition(xpos, ypos);
                        }

                        // Handle looping for GIFs/Videos when duration is -1
                        bool videoLoop = false;
                        if (isVideo && duration < 0)
                        {
                            LogIt($"🔄 Setting video loop to true for {fullPath}");
                            videoLoop = true;
                        }
                       
                        // Adjust duration for videos and images if not explicitly set
                        // For image, set duration to infinite (9999s)
                        duration = (isVideo && duration == 0) ? ((AnimatedActor)mediaActor).Length :
                                   (isImage && duration == 0) ? 9999 : duration;


                        //Check the video Loop
                        duration = (videoLoop) ? -1 : duration;

                        BackgroundScene bg = CreateBackgroundScene(mediaActor, "", animation.ToLower(), gDmdDevice.NewFont("FlexDMD.Resources.udmd-f6by12.fnt",Color.Black,Color.Black,0), duration, xpos, ypos, path, alignment, pause, "DisplayPicture");
                        _currentScene = bg; // Almacenar la referencia a la escena actual
                        _SequenceQueue.Visible = true;

                        // Add scene to the queue or directly to the stage
                        if (cleanbg)
                        {                     
                            bg.Name = "Sequence " + path; 
                            _SequenceQueue.Enqueue(bg);
                            _loopTimer?.Dispose();
                        }
                        else
                        {
                            gDmdDevice.Stage.AddActor(bg);
                        }
                        
                        // Arm timer once animation is done playing
                        if (duration  + pause >= 0 )
                        {
                            duration = duration * 1.5f  + pause;
                            LogIt($"⏳AnimationTimer: Duration is greater than 0, calling animation timer for {bg.Name}");
                            var animationTimer = new Timer(AnimationTimer, null, (int)duration * 1000 + 1000, Timeout.Infinite);
                            lock (_animationTimers)
                            {
                                _animationTimers.Add(new AnimationTimerInfo(animationTimer, bg));
                            }
                        }
                        
                    };

                    LogIt($"📷Rendering {(isVideo ? $"video (duration: {duration * 1000}ms)" : "image")}: {fullPath} (duration: {duration * 1000}ms)");

                    // Execute initial action
                    gDmdDevice.LockRenderThread();
                    gDmdDevice.Post(displayAction);
                    gDmdDevice.UnlockRenderThread();

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
        /// <summary>
        /// Displays Highscores on the DMD device.
        /// %0A or | for line break
        /// </summary>
        public static bool DisplayHighscores(string game, string size, string color, string font, string bordercolor, int bordersize, bool cleanbg, string animation, float duration, bool loop, bool toQueue)
        {
            // Make the path for the highscore exe and the highscore file
            string hi2txtExe = System.IO.Path.Combine(AppSettings.hi2txt_path, "Hi2Txt.exe");
            string hiscoreFile = System.IO.Path.Combine(AppSettings.mame_path, "hiscore", $"{game}.hi");
            LogIt($"Hi2txt set to {hi2txtExe}");
            LogIt($"Game highscore file set to {hiscoreFile}");
            // Verifies if files exist before run
            if (!System.IO.File.Exists(hi2txtExe))
            {
                LogIt($"Error: No Hi2Txt.exe found in the path specified.");
                return false;
            }
    
            if (!System.IO.File.Exists(hiscoreFile))
            {
                LogIt($"Error: No highscore file found for {game}.");
                return false;
            }
    
			// Verifies if files exist before run
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = hi2txtExe,
                Arguments = $"-r -keep-field \"NAME\" -keep-field \"RANK\" -keep-field \"SCORE\" -keep-first-table \"yes\" \"{hiscoreFile}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
    
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
                {
                    if (process == null)
                    {
                        LogIt($"Error: Hi2Txt can't be started.");
                        return false;
                    }
    
                    // Read and process the output
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
    
                    // Replace '|' by ' - ' in every output before join the lines
                    string formattedOutput = string.Join("|", output.Replace("|", " - ").Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) + "|";
                    
                    DisplayText(formattedOutput, size, color, font, bordercolor, bordersize, cleanbg, animation, duration,loop, toQueue);
                    
                }
    
                return true;
            }
            catch (Exception ex)
            {
                LogIt($"Error at start Hi2Txt: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Displays text on the DMD device.
        /// %0A or | for line break
        /// </summary>
        public static bool DisplayText(string text, string size, string color, string font, string bordercolor, int bordersize, bool cleanbg, string animation, float duration, bool loop, bool toQueue, float wait = 0f, string align = "center", float pause = 0f)
        {
            try
            {
                // Convert size to numeric value based on device dimensions
                size = GetFontSize(size, gDmdDevice.Width, gDmdDevice.Height);
                var alignmentMap = new Dictionary<string, Alignment>(StringComparer.OrdinalIgnoreCase)
                        {
                            {"center", Alignment.Center},
                            {"right", Alignment.Right},
                            {"left", Alignment.Left},
                            {"top", Alignment.Top},
                            {"bottom", Alignment.Bottom},
                            {"topright", Alignment.TopRight},
                            {"topleft", Alignment.TopLeft},
                            {"bottomright", Alignment.BottomRight},
                            {"bottomleft", Alignment.BottomLeft}
                        };

                Alignment alignmentValue = alignmentMap.TryGetValue(align, out var alignment) 
                    ? alignment
                    : Alignment.Center; // Default

                _totalDuration = duration + pause;

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
                int border = bordersize != 0 ? 1 : 0;
                
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

                    // If this text needs to be queued AND there is an animation/text running BUT current animation/text is not meant to be infinite, 
                    // then add this text and its parameters to the animation queue. The animation timer will take care of it
                    
                    if (toQueue && _totalDuration > 0)
                    {
                        lock (_animationQueueLock)
                        {
                            LogIt($"⏳Queuing {text} for display after current animation");
                            _animationQueue.Enqueue(new QueueItem(text, size, color, font, bordercolor, bordersize, cleanbg, animation, duration, loop, wait, align, pause));  
                            LogIt($"⏳Queue has {_animationQueue.Count} items: {string.Join(", ", _animationQueue.Select(i => i.Text))}");
                            return;
                            
                        } 
                    }
                    
                    if (wait > 0)
                    {
                        lock (_animationQueueLock)
                        {
                            LogIt($"⏳Queuing {text} for display after {wait} seconds wait");
                            _animationQueue.Enqueue(new QueueItem(text, size, color, font, bordercolor, bordersize, cleanbg, animation, duration, loop, 0, align, pause));  
                            LogIt($"⏳Queue has {_animationQueue.Count} items: {string.Join(", ", _animationQueue.Select(i => i.Text))}");
                        }
                        // Crear un temporizador para esperar el tiempo de espera.
                        var animationTimer = new Timer(AnimationTimer, null, (int)wait * 1000, Timeout.Infinite);
                        lock (_animationTimers)
                        {
                            _animationTimers.Add(new AnimationTimerInfo(animationTimer, null)); // Usar null para indicar que este temporizador es para wait.
                        }
                        return;
                    }
                    System.Action displayAction = () =>
                    {
                        // Create font and label actor
                        FlexDMD.Font myFont = gDmdDevice.NewFont(localFontPath, HexToColor(color), HexToColor(bordercolor), border);
                            
                        gDmdDevice.Graphics.Clear(Color.Black);
                        _scoreDelayTimer?.Dispose();
                        _scoreBoard.Visible = false;
    
                        var labelActor = new Actor();

                        if (cleanbg)
                        {


                            _SequenceQueue.RemoveAllScenes();
                            _animationQueue.Clear();
                            gDmdDevice.Graphics.Clear(Color.Black);
                            gDmdDevice.Stage.RemoveAll();
                            gDmdDevice.Stage.AddActor(_SequenceQueue);
                            gDmdDevice.Stage.AddActor(_scoreBoard);
                            _scoreBoard.Visible = false;
                            _loopTimer?.Dispose();
                        }
    
                        if (_totalDuration > 0)
                        {
                            _animationTimer?.Dispose();
                            _animationTimer = new Timer(AnimationTimer, null, (int)(duration) * 1000 , Timeout.Infinite);
                        }
                        
                        // Create background scene based on animation type
                        BackgroundScene bg = CreateBackgroundScene(labelActor, text, animation.ToLower(), myFont, duration, 0, 0, "Text", alignmentValue, pause, "DisplayText");
                         _currentScene = bg;
                        _SequenceQueue.Visible = true;
    
                        // Add scene to the queue or directly to the stage
                        if (cleanbg)
                        {
                            _SequenceQueue.Enqueue(bg);
                            _loopTimer?.Dispose();
                        }
                        else
                        {
                            gDmdDevice.Stage.AddActor(bg);
                        }

                    };

                     LogIt($"Rendering text: {text}");
                    
                    // Execute initial action
                    gDmdDevice.LockRenderThread();
                    gDmdDevice.Post(displayAction);
                    gDmdDevice.UnlockRenderThread();
    
                    // If loop is true, configure the timer
                    if (loop)
                    {
                        LogIt($"Rendering text: {text} in a loop");
                        float waitDuration = duration * 0.85f; // 15% less than duration
                        _loopTimer = new Timer(_ =>
                        {
                            gDmdDevice.LockRenderThread();
                            gDmdDevice.Post(displayAction);
                            gDmdDevice.UnlockRenderThread();
                        }, null, (int)(waitDuration * 1000), (int)(waitDuration * 1000));
                    }
               
                });
                
                // Return true immediately after validation, while display processing continues in background
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
        private static BackgroundScene CreateBackgroundScene(Actor mediaActor, string text, string animation, FlexDMD.Font myFont, float duration, int xpos, int ypos, string name, Alignment alignment, float pause, string calledFrom)
        {
            
            switch (animation)
            {
                case "none":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.None, duration, AnimationType.None, name, 0);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.None, duration, AnimationType.None, alignment, "", 0);
                    break;
                case "fade":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.FadeIn, pause, AnimationType.FadeOut, name, duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.FadeIn, pause, AnimationType.FadeOut, alignment, "", duration);
                    break;
                case "scrollright":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnRight, pause, AnimationType.ScrollOffRight, name, duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnRight, pause, AnimationType.ScrollOffRight, alignment, "", duration);
                    break;
                case "left2left":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnRight, pause, AnimationType.ScrollOffLeft, name, duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnRight, pause, AnimationType.ScrollOffLeft, alignment, "", duration);
                    break;
                case "scrollleft":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnLeft, pause, AnimationType.ScrollOffLeft, name, duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnLeft, pause, AnimationType.ScrollOffLeft, alignment, "", duration);
                    break;
                case "right2right":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnLeft, pause, AnimationType.ScrollOffRight, name, duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnLeft, pause, AnimationType.ScrollOffRight, alignment, "", duration);
                    break;
                case "scrolldown":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnDown, pause, AnimationType.ScrollOffDown, name, duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnDown, pause, AnimationType.ScrollOffDown, alignment, "", duration);
                    break;
                case "top2top":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnDown, pause, AnimationType.ScrollOffUp,name,duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnDown, pause, AnimationType.ScrollOffUp, alignment, "", duration);
                    break;
                case "scrollup":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.ScrollOnUp,pause ,AnimationType.ScrollOffUp,name,duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnUp,pause ,AnimationType.ScrollOffUp,alignment,"",duration);
                    break;
                case "bottom2bottom":
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor ,AnimationType.ScrollOnUp,pause ,AnimationType.ScrollOffDown,name,duration);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.ScrollOnUp,pause ,AnimationType.ScrollOffDown,alignment,"",duration);
                    break;
                default:
                    if (calledFrom == "DisplayPicture")
                        return new BackgroundScene(gDmdDevice, mediaActor, AnimationType.None, duration, AnimationType.None, name, 0);
                    if (calledFrom == "DisplayText")
                        return new ScrollingTextScene(gDmdDevice, mediaActor, text, myFont, AnimationType.None, duration, AnimationType.None, alignment, "", 0);
                    break;
            }
            LogIt($"Error: Invalid {mediaActor.Name} with animation type {animation} called from {calledFrom} with a duration of {duration} a pause of {pause}, x: {xpos} y: {ypos} or text with {text} using font {myFont}.");
            return null;
 
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

                    List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp", ".apng", ".jpeg" };

                    if (FileExistsWithExtensions(localPath, extensions, out string foundExtension))
                    {
                        string fullPath = localPath + foundExtension;

                        List<string> videoExtensions = new List<string> { ".gif", ".avi", ".mp4", ".apng" };
                        List<string> imageExtensions = new List<string> { ".png", ".jpg", ".bmp", ".jpeg" };

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
                _SequenceQueue.RemoveAllScenes();
                _animationQueue.Clear();
                gDmdDevice.Graphics.Clear(Color.Black);
                gDmdDevice.Stage.RemoveAll();
                gDmdDevice.Stage.AddActor(_SequenceQueue);
                gDmdDevice.Stage.AddActor(_scoreBoard);
                _scoreBoard.Visible = false;
                if (_SequenceQueue.IsFinished()) _SequenceQueue.Visible = false;
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
                                        //[url_prefix]/v1/display/picture?path=<image or video path>&animation=<fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<seconds>&queue&cleanbg=<true|false>&fixed=<true|false>
                                        string picturepath = query.Get("path");
                                        string pFixed = query.Get("fixed") ?? "false";
                                        float pictureduration = float.TryParse(query.Get("duration"), out float result) ? result : 0.0f;
					                    float picturewait = float.TryParse(query.Get("wait"), out float wresult) ? wresult : 0.0f;
                                        int pxpos = int.TryParse(query.Get("xpos"), out int pxresult) ? pxresult : 0;
                                        int pypos = int.TryParse(query.Get("ypos"), out int pyresult) ? pyresult : 0;
                                        string pictureanimation = query.Get("animation") ?? "none";
                                        string palign = query.Get("align") ?? "center";
                                        string pscale = query.Get("scale") ?? "fit";
                                        float pplayspeed = float.TryParse(query.Get("playspeed"), out float psresult) ? psresult : 1.0f;
					                    float ppause = float.TryParse(query.Get("pause"), out float ppresult) ? ppresult : 0f;
                                        bool queue;
                                        // Check if 'queue' exists in the query parameters
                                        queue = dof2dmdUrl.Contains("&queue") || dof2dmdUrl.EndsWith("?queue");

                                        if (StringComparer.OrdinalIgnoreCase.Compare(pFixed, "true") == 0)
                                        {
                                            pictureduration = -1.0f;
                                        }
                                        if (picturepath.Contains("marquee"))  // creo que tiene más sentido si la carpeta que se compruebe contiene marquee en vez de sólo excluir mameouput. Pensando en múltiples usos para DOF2DMD
                                        {
                                            // This is certainly a game marquee, provided during new game
                                            // If path corresponds to an existing file, set game marquee /// Comento todo esto ya que pueden haber marquees animadas
                                            //List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };
                                            //List<string> extensions = new List<string> { ".png", ".jpg", ".bmp" };
                                            //if (FileExistsWithExtensions(HttpUtility.UrlDecode(AppSettings.artworkPath + "/" + picturepath), extensions, out string foundExtension))
                                            //{
                                                SetGameMarquee(picturepath);
                                                LogIt($"Setting Game Marquee to: {picturepath}");
                                            //}
                                        //Reset scores for all players as diferent marquee is a diferent game and we need to reset the scores
                                        for (int i = 1; i <= 4; i++)
                                            gScore[i] = 0;
                                        }
                                    bool pcleanbg;
                                        if (!bool.TryParse(query.Get("cleanbg"), out pcleanbg))
                                        {
                                            pcleanbg = true; // default value if the conversion fails
                                        }
                                        if (!DisplayPicture(picturepath, pictureduration, pictureanimation, queue, pcleanbg, picturewait,pxpos, pypos, pscale, palign, pplayspeed, ppause))
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
                                        int bordersize = int.TryParse(query.Get("bordersize"), out int bresult) ? bresult : 0;
                                        string animation = query.Get("animation") ?? "none";
                                        float textwait = float.TryParse(query.Get("wait"), out float twresult) ? twresult : 0.0f;
                                        string talign = query.Get("align") ?? "center";
                                        float textduration = float.TryParse(query.Get("duration"), out float tresult) ? tresult : 5.0f;
                                        float tpause = float.TryParse(query.Get("pause"), out float tpresult) ? tpresult : 0f;
					                    LogIt($"Text is now set to: {text} with size {size}, color {color}, font {font}, border color {bordercolor}, border size {bordersize}, animation {animation} with a duration of {textduration} seconds and a pause of {tpause} seconds");
                                        
                                        bool tqueue;
                                        // Check if 'queue' exists in the query parameters
                                        tqueue = dof2dmdUrl.Contains("&queue") || dof2dmdUrl.EndsWith("?queue");
                                        
                                        bool cleanbg;
                                        if (!bool.TryParse(query.Get("cleanbg"), out cleanbg))
                                        {
                                            cleanbg = true; // default value if the conversion fails
                                        }
                                        bool loop;
                                        if (!bool.TryParse(query.Get("loop"), out loop))
                                        {
                                            loop = false; // default value if the conversion fails
                                        }
                                        if (!DisplayText(text, size, color, font, bordercolor, bordersize, cleanbg, animation, textduration, loop, tqueue, textwait, talign, tpause))
                                        {
                                            sReturn = "Error when displaying text";
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
                                            sCleanbg = true; // default value if the conversion failsa
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
                                    case "highscores":
                                        if (!AppSettings.hi2txt_enabled)
                                        {
                                            LogIt($"Highscores is not enabled");
                                            break;
                                        }
                                        string hgame = query.Get("game") ?? "";
                                        string hsize = query.Get("size") ?? "M";
                                        string hcolor = query.Get("color") ?? "FFFFFF";
                                        string hfont = query.Get("font") ?? "Consolas";
                                        string hbordercolor = query.Get("bordercolor") ?? "000000";
                                        int hbordersize = int.TryParse(query.Get("bordersize"), out int hbresult) ? hbresult : 0;
                                        string hanimation = query.Get("animation") ?? "ScrollUp";
                                        hanimation = (hanimation == "ScrollDown" || hanimation == "ScrollUp") ? hanimation : "ScrollUp";
                                        float hduration = float.TryParse(query.Get("duration"), out float hresult) ? hresult : 15.0f;
                                        LogIt($"Highscore is now set to game {hgame} with size {hsize}, color {hcolor}, font {hfont}, border color {hbordercolor}, border size {hbordersize}, animation {hanimation} with a duration of {hduration} seconds");
                                        bool hiqueue;
                                        // Check if 'queue' exists in the query parameters
                                        hiqueue = dof2dmdUrl.Contains("&queue") || dof2dmdUrl.EndsWith("?queue");
                                        bool hcleanbg;
                                        if (!bool.TryParse(query.Get("cleanbg"), out hcleanbg))
                                        {
                                            hcleanbg = true; // default value if the conversion fails
                                        }
                                        bool hloop;
                                        if (!bool.TryParse(query.Get("loop"), out hloop))
                                        {
                                            hloop = false; // default value if the conversion fails
                                        }

                                        if (!DisplayHighscores(hgame, hsize, hcolor, hfont, hbordercolor, hbordersize, hcleanbg, hanimation, hduration, hloop, hiqueue))
                                        {
                                            sReturn = "Error when displaying highscores";
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
        private float _length;
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

        public BackgroundScene(IFlexDMD flex, Actor background, AnimationType animateIn, float pauseS, AnimationType animateOut, string id = "", float afactor = 0f) : base(flex, animateIn, pauseS, animateOut, id, afactor)
        {
            _background = background;
            _length = afactor;
            if (_background != null) AddActor(_background);
        }
        public override void Update(float delta)
        {
            base.Update(delta);
            _background?.SetSize(Width, Height);
            if (IsFinished() && !(_animateOut == AnimationType.None || _animateOut == AnimationType.FadeOut))
            {
                      FlexDMD.Stage.RemoveActor(this);
            }
        }
        protected override void Begin()
        {
            base.Begin();
            // Removes background if animation type out is None
            if (_length > -1 && (_animateOut == AnimationType.None || _animateOut == AnimationType.FadeOut))
            {
                if(Pause <0) Pause = 999999f; // Pause is negative, so we set it to a very high value
                var action1 = new FlexDMD.WaitAction(Pause);
                var action3 = new FlexDMD.RemoveFromParentAction(_background); 
                var sequenceAction2 = new FlexDMD.SequenceAction();
                sequenceAction2.Add(action1);
                sequenceAction2.Add(action3);
                _background.AddAction(sequenceAction2);
            }
                        
        }
    }
    
    class ScrollingTextScene : BackgroundScene
    {
        private readonly Group _container;
        private readonly float _length;
        private readonly Alignment _alignment;
  
        public ScrollingTextScene(IFlexDMD flex, Actor background, string text, FlexDMD.Font font, AnimationType animateIn, float pauseS, AnimationType animateOut, Alignment alignment = Alignment.Center, string id = "", float afactor = 0.5f) : base(flex, background, animateIn, pauseS, animateOut, id, afactor)
        {
            _container = new Group(FlexDMD);
            _alignment = alignment;
            _length = afactor;
            var y = 0f;

            AddActor(_container);
            string[] lines = text.Split(new char[] { '\n', '|' });
            
            _container.Width = Width;
            _container.Height = Height;
            _container.SetPosition(0,0);

            foreach (string line in lines)
            {
                var txt = line;
                if (txt.Length == 0) txt = " ";
                var label = new FlexDMD.Label(flex, font, txt);
                label.Y = y;
                y += label.Height;
                label.Alignment = _alignment;
                _container.AddActor(label);
            }
            _container.Height = y;
        }

        protected override void Begin()
        {
            base.Begin();

            // Lógica para el desplazamiento de entrada (solo si hay desplazamiento)
            if (_animateIn == AnimationType.ScrollOnLeft || _animateIn == AnimationType.ScrollOnRight || _animateIn == AnimationType.ScrollOffLeft || _animateIn == AnimationType.ScrollOffRight)
            {
                _container.X = 0;
                _container.Y = (Height - _container.Height) / 2;
            }
            else if (_animateIn == AnimationType.ScrollOnUp || _animateIn == AnimationType.ScrollOnDown || _animateIn == AnimationType.ScrollOffUp || _animateIn == AnimationType.ScrollOffDown)
            {
                _container.Y = (Height - _container.Height) / 2; 
            }

            // Lógica para ocultar el texto después de un tiempo
            
            
            if (_length > -1 && _animateOut == AnimationType.None)
            {
                var action1 = new FlexDMD.WaitAction(Pause);
                var action2 = new FlexDMD.RemoveFromParentAction(_container);
                var sequenceAction = new FlexDMD.SequenceAction();
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
                
                float totalHeight = 0;
                foreach (Actor line in _container.Children)
                {

                    line.Width = Width;
                    totalHeight += line.Height;
                    line.X = (Width - line.Width) / 2;
                }
                float y = 0;
                switch (_alignment)
                {
                    case Alignment.TopLeft:
                    case Alignment.Top:
                    case Alignment.TopRight:
                        if (_animateIn == AnimationType.None || _animateIn == AnimationType.FadeIn) y = 0;
                        if (_animateIn == AnimationType.ScrollOnRight || _animateIn == AnimationType.ScrollOnLeft ) _container.Y = 0;
                        break;
                    case Alignment.BottomLeft:
                    case Alignment.Bottom:
                    case Alignment.BottomRight:
                        if (_animateIn == AnimationType.None || _animateIn == AnimationType.FadeIn) y = Height - totalHeight;
                        if (_animateIn == AnimationType.ScrollOnRight || _animateIn == AnimationType.ScrollOnLeft ) _container.Y = Height - _container.Height;
                        break;
                    default:
                        if (_animateIn == AnimationType.None || _animateIn == AnimationType.FadeIn)
                        {
                            if (totalHeight < Height)
                            {
                                y = (Height - totalHeight) / 2;
                            }
                            else
                            {
                                y = 0;
                            }
                        }
                        if (_animateIn == AnimationType.ScrollOnRight || _animateIn == AnimationType.ScrollOnLeft ) _container.Y = (Height - _container.Height) / 2;
                        break;
                }
                foreach (Actor line in _container.Children)
                {
                    if (line is FlexDMD.Label label)
                    {
                        label.Y = y;
                        y += label.Height;
                        if (y > Height)
                        {
                            break; // Detener si se excede la altura del contenedor
                        }
                    }
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
