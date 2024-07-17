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
//                                     Copyright (C) 2024 Olivier JACQUES
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

class DOF2DMD
{
    public static FlexDMD.FlexDMD gDmdDevice;
    public static FlexDMD.IUltraDMD gUDmdDevice;
    public static int[] gScore = [0, 0, 0, 0, 0];
    public static int gActivePlayer = 1;
    public static int gNbPlayers = 1;
    public static bool gScoreQueued = false;
    public static string gGameMarquee = "DOF2DMD";
    private static Timer _scoreTimer;
    private static Timer _animationTimer;
    private static readonly object _scoreQueueLock = new object();
    private static readonly object _animationQueueLock = new object();
    

    static void Main(string[] args)
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
        gUDmdDevice = gDmdDevice.NewUltraDMD();

        // Display start picture as game marquee
        gGameMarquee = AppSettings.StartPicture;
        DisplayPicture(gGameMarquee, true);

        // Start the http listener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add($"{AppSettings.UrlPrefix}/");
        listener.Start();

        Trace.WriteLine($"DOF2DMD is now listening for requests on {AppSettings.UrlPrefix}...");

        Task listenTask = HandleIncomingConnections(listener);
        listenTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Callback method once animation is finished.
    /// Displays the player's score or the game marquee picture based on the state of gScoreQueued.
    /// </summary>
    private static void AnimationTimer(object state)
    {
        LogIt("⏱️ AnimationTimer");
        lock (_animationQueueLock)
        {
            if (gUDmdDevice.IsRendering())
                gUDmdDevice.CancelRendering();
            _animationTimer.Dispose();
            // Display score if gScoreQueued, or display marquee
            if (gScoreQueued)
            {
                LogIt("  ⏱️ AnimationTimer: score queued, display it");
                if (gScore[gActivePlayer] > 0)
                    DisplayScore(gActivePlayer, gScore[gActivePlayer]);
                gScoreQueued = false;
            }
            else
            {
                LogIt("  ⏱️ AnimationTimer: no score queued, restore game marquee");
                DisplayPicture(gGameMarquee, true);
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
            DisplayPicture(gGameMarquee, true);
            _scoreTimer.Dispose();
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

    public struct ImageInfo
    {
        public int Width;
        public int Height;
        public bool IsAnimated;
        public bool IsLooped;
        public int AnimationLength; // In milliseconds
    }

    /// <summary>
    /// Get image (gif info, including animation length)
    /// </summary>
    public static ImageInfo GetImageInfo(string path)
    {
        ImageInfo info = new ImageInfo();
        using (System.Drawing.Image image = System.Drawing.Image.FromFile(path))
        {
            info.Height = image.Height;
            info.Width = image.Width;

            if (image.RawFormat.Equals(ImageFormat.Gif))
            {
                if (System.Drawing.ImageAnimator.CanAnimate(image))
                {
                    FrameDimension frameDimension = new FrameDimension(image.FrameDimensionsList[0]);
                    int frameCount = image.GetFrameCount(frameDimension);
                    int delay = 0;
                    int index = 0;

                    for (int f = 0; f < frameCount; f++)
                    {
                        int this_delay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, index) * 10;
                        delay += this_delay < 100 ? 100 : this_delay;  // Minimum delay is 100 ms
                        index += 4;
                    }

                    info.AnimationLength = delay;
                    info.IsAnimated = true;
                    info.IsLooped = BitConverter.ToInt16(image.GetPropertyItem(20737).Value, 0) != 1;
                }
            }
        }
        return info;
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

    /// <summary>
    /// Displays an image file on the DMD device using native FlexDMD capabilities.
    /// </summary>

    public static Boolean DisplayPicture2(string path, int duration)
    {
        LogIt($"DisplayPicture2 {path}");
        try
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(path))
                return false;

            // Convert path in URI format to path in local filesystem format
            string localPath = HttpUtility.UrlDecode(path);

            //Aquí habrá que chequear si la imagen es una gif o video para tratarlo como NewVideo, para otras extensiones será NewImage
            var assetManager = new FlexDMD.AssetManager();
            assetManager.ResolveSrc("");
            var imageActor = new FlexDMD.Image(assetManager, path, "MyImage");
            imageActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
            //This passes to the DMD the actions to do
            gDmdDevice.Post(() =>
            {


                gDmdDevice.LockRenderThread();
                //This create a scene group
                var sceneGroup = new Group(gDmdDevice);


                //This adds the image to the scene group
                sceneGroup.AddActor(imageActor);
                var act1 = new FlexDMD.WaitAction(duration);

                // This creates the variable that storages the actions and uses the extended SequenceAction class
                var sequenceAction = new CustomSequenceAction();

                //This add the diferent actions for them to be performed one after the other
                sequenceAction.Add(act1);
                //sequenceAction.Add(act2);

                //This activates the actions in the sequenceAction
                imageActor.AddAction(sequenceAction);

                // This checks if the sequence has finished, in which case the initial marquee image is shown again,
                // the actors are removed, and frame deletion is disabled

                sequenceAction.Finished += () =>
                {
                    gDmdDevice.Stage.RemoveActor(sceneGroup);
                    gDmdDevice.Clear = false;
                    DisplayPicture(gGameMarquee, true);
                };
                //The sceneGroup is added as an actor to be shown on the DMD
                gDmdDevice.Stage.AddActor(sceneGroup);
                gDmdDevice.UnlockRenderThread();
            });

            LogIt($"Rendering image: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"  Error occurred while fetching the image. {ex.Message}");
            return false;
        }

    }

    /// <summary>
    /// Displays an image or video file on the DMD device using native FlexDMD capabilities.
    /// </summary>
    /// REvisar el DisplayPicture y DisplayText porque esta es la manera buena de usar los NewVideo, NewImage y tal
    public static Boolean DisplayVideo(string path, bool repeat=false)
    {
        LogIt($"DisplayVideo {path}");
        try
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(path))
                return false;

            // Convert path in URI format to path in local filesystem format
            string localPath = HttpUtility.UrlDecode(path);

            //Aquí habrá que chequear si la imagen es una gif o video para tratarlo como NewVideo, para otras extensiones será NewImage

            var videoActor = (AnimatedActor)gDmdDevice.NewVideo("MyVideo", localPath);

            //var videoActor = new FlexDMD.Video(localPath, "myVideo", repeat);
            videoActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
            var sceneGroup = (Group)gDmdDevice.NewGroup("VideoGroup");

            //This passes to the DMD the actions to do
            gDmdDevice.Post(() =>
            {


                gDmdDevice.LockRenderThread();
                //This create a scene group



                //This adds the image to the scene group
                sceneGroup.AddActor(videoActor);
                //var act1 = new FlexDMD.WaitAction(duration);

                // This creates the variable that storages the actions and uses the extended SequenceAction class
                //var sequenceAction = new CustomSequenceAction();

                //This add the diferent actions for them to be performed one after the other
                //sequenceAction.Add(act1);
                //sequenceAction.Add(act2);

                //This activates the actions in the sequenceAction
                //videoActor.AddAction(sequenceAction);
                LogIt($"La duración del video es: {videoActor.Length}");
                // This checks if the sequence has finished, in which case the initial marquee image is shown again,
                // the actors are removed, and frame deletion is disabled


                /*
                sequenceAction.Finished += () =>
                {
                    gDmdDevice.Stage.RemoveActor(sceneGroup);
                    gDmdDevice.Clear = false;
                    DisplayPicture(gGameMarquee, true);
                };
                */
                //The sceneGroup is added as an actor to be shown on the DMD

                gDmdDevice.Stage.AddActor(sceneGroup);
                gDmdDevice.UnlockRenderThread();
            });
            
           
            LogIt($"Rendering Video: {localPath}");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"  Error occurred while fetching the video. {ex.Message}");
            return false;
        }

    }
    /// <summary>
    /// Displays an image file (GIF or PNG) on the DMD device.
    /// Handles animation timers and file selection based on input parameters.
    /// </summary>

    public static Boolean DisplayPicture(string path, bool bfixed)
    {
        LogIt($"DisplayPicture {path}, fixed? {bfixed}");
        try
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(path))
                return false;

            // Convert path in URI format to path in local filesystem format
            string localPath = HttpUtility.UrlDecode(path);

            // If there is a gif, use it unless bfixed is true. Otherwise, default to png
            string curDir = Directory.GetCurrentDirectory();
            string gifPath = $"{AppSettings.artworkPath}/{localPath}.gif";
            string pngPath = $"{AppSettings.artworkPath}/{localPath}.png";
            string fileToUse;
            if (bfixed)
                fileToUse = pngPath;
            else
                fileToUse = File.Exists(gifPath) ? gifPath : pngPath;
            int gifDuration = 100; // 100 ms by default - for PNGs for example
            // If the image to use is a Gif
            if (fileToUse.EndsWith(".gif"))
            {
                ImageInfo info = GetImageInfo(fileToUse);
                gifDuration = info.AnimationLength;
                // Start timer to end after the animation
                if (_animationTimer != null)
                    _animationTimer.Dispose();
                _animationTimer = new Timer(AnimationTimer, null, gifDuration, Timeout.Infinite);
            }

            if (File.Exists(fileToUse))
            {
                LogIt($"  Using file {curDir}/{fileToUse}");
                // See https://github.com/vbousquet/flexdmd/blob/master/FlexDMDCmdTest/Program.cs
                // For an example of displaying score (or a scene), then showing animations on top :
                // https://www.vpforums.org/index.php?app=tutorials&article=145
                // UltraDMD reference: https://ultradmd.wordpress.com/programming-guide/documentation-2/
                // Animations
                // 0:  Fade In - apparently broken
                // 1:  Fade Out - apparently broken
                // 2:  Zoom In - apparently broken
                // 3:  Zoom Out - apparently broken
                // 4:  Scroll Off Left
                // 5:  Scroll Off Right
                // 6:  Scroll On Left
                // 7:  Scroll On Right
                // 8:  Scroll Off Up
                // 9:  Scroll Off Down
                // 10: Scroll On Up
                // 11: Scroll On Down
                // 14: No animation
                // TODO : Show score after animation - will have to check if UDMD is still playing the animation, in a loop
                // TODO : an option should indicate whether the score or the marquee must be displayed after an animation
                // udmd.DisplayScoreboard(2, activePlayer, scorePlayer1, scorePlayer2, 0, 0, activePlayer == 2 ? "PLAYER 2" : "PLAYER 1", "");
                // udmd.DisplayScoreboard(2, 1, 32760, 0, 0, 0, "PLAYER 1", "SAY NO TO DRUGS");
                // void DisplayScoreboard(int cPlayers, int highlightedPlayer, long score1, long score2, long score3, long score4, string lowerLeft, string lowerRight);
                // void DisplayScoreboard00(int cPlayers, int highlightedPlayer, long score1, long score2, long score3, long score4, string lowerLeft, string lowerRight);
                // void DisplayScene00(string background, string toptext, int topBrightness, string bottomtext, int bottomBrightness, int animateIn, int pauseTime, int animateOut);
                // void DisplayScene00Ex(string background, string toptext, int topBrightness, int topOutlineBrightness, string bottomtext, int bottomBrightness, int bottomOutlineBrightness, int animateIn, int pauseTime, int animateOut);
                // void DisplayScene00ExWithId(string sceneId, bool cancelPrevious, string background, string toptext, int topBrightness, int topOutlineBrightness, string bottomtext, int bottomBrightness, int bottomOutlineBrightness, int animateIn, int pauseTime, int animateOut);
                // void ModifyScene00(string id, string toptext, string bottomtext);
                // void ModifyScene00Ex(string id, string toptext, string bottomtext, int pauseTime);
                // void DisplayScene01(string sceneId, string background, string text, int textBrightness, int textOutlineBrightness, int animateIn, int pauseTime, int animateOut);
                // void DisplayText(string text, int textBrightness, int textOutlineBrightness);
                // void ScrollingCredits(string background, string text, int textBrightness, int animateIn, int pauseTime, int animateOut);
                gUDmdDevice.CancelRendering();
                gUDmdDevice.DisplayScene00($"{fileToUse}", "", -1, "", -1, 14, gifDuration, 14);
            }
            else
            {
                LogIt($"  File not found: {curDir}/{fileToUse}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"  Error occurred while fetching the image. {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Displays the player's score on the DMD device.
    /// Starts a timer to revert to the game marquee after a specified duration.
    /// </summary>
    public static Boolean DisplayScore(int player, int score)
    {
        gScore[player] = score;
        LogIt($"DisplayScore for player {player}: {score}");
        gScoreQueued = true;
        gUDmdDevice.DisplayScoreboard(gNbPlayers, gActivePlayer, gScore[1], gScore[2], gScore[3], gScore[4], $"PLAYER {gActivePlayer}", "");
        if (_scoreTimer != null)
            _scoreTimer.Dispose();
        _scoreTimer = new Timer(ScoreTimer, null, AppSettings.displayScoreDuration * 1000, Timeout.Infinite);
        return true;
    }

    /// <summary>
    /// Displays ScoreBackground on the DMD device.
    /// </summary>
    public static Boolean DisplayScorebackground(string scorebackground, int brightness)
    {
        LogIt("Display ScoreBackground");
        gUDmdDevice.SetScoreboardBackgroundImage(scorebackground, brightness, 10);
        return true;
    }
    /// <summary>
    /// Displays text on the DMD device.
    /// </summary>
    public static Boolean DisplayText(string text, string size, string color, string font, string bordercolor, string bordersize)
    {
        int border = 0;
        if (bordersize != "0")
        {
            border = 1;
        }

        // This configures the DMD in background erase mode so it doesn't leave a halo when scrolling or animating between frames
        gDmdDevice.Clear = true;

        // This defines a font; it has to be in fnt format with its associated png. The story here is that there are some
        // already included as resources in FlexDMD (They can be invoked as FlexDMD.Resources [font_name]. The list it includes is: 
        // bm_army-12.fnt
        // teeny_tiny_pixls-5.fnt
        // udmd-f12by24.fnt
        // udmd-f14by26.fnt
        // udmd-f4by5.fnt
        // udmd-f5by7.fnt
        // udmd-f6by12.fnt
        // udmd-f7by13.fnt
        // udmd-f7by5.fnt
        // zx_spectrum-7.fnt

        FlexDMD.Font myFont = gDmdDevice.NewFont(font + ".fnt", Color.FromName(color), Color.FromName(bordercolor), border);

        // This is necessary to add an image to the scene. The ResolveSrc is not
        // clear to me; it is necessary, but since the path is also provided when
        // adding the image, here leaving it blank has worked for me. I leave it
        // commented because for these texts we leave it blank
        //var assetManager = new FlexDMD.AssetManager();
        //assetManager.ResolveSrc("");

        //This defines a label and position it off to the right to scroll it to the left later
        var myLabel = new FlexDMD.Label(gDmdDevice, myFont, text);

        var fSize = myFont.MeasureFont(text);
        myLabel.SetAlignedPosition(fSize.Width * 2, gDmdDevice.Height / 2, Alignment.Center);
        //myLabel.SetSize(fSize.Width, fSize.Height);

        //This define an image too
        //var imageActor = new FlexDMD.Image(assetManager, "artwork/bnj.png", "MyImage");
        //imageActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);

        //This passes to the DMD the actions to do
        gDmdDevice.Post(() =>
        {
            gDmdDevice.LockRenderThread();
            //This create a scene group
            var sceneGroup = new Group(gDmdDevice);

            //This adds the label to the scene group
            //sceneGroup.AddActor(imageActor);
            sceneGroup.AddActor(myLabel);

            //This defines the move action over the label to create the scrool from right to the left
            var act1 = new FlexDMD.MoveToAction(myLabel, myLabel.X - fSize.Width * 3, myLabel.Y, fSize.Width / 40);

            // This creates the variable that storages the actions and uses the extended SequenceAction class
            var sequenceAction = new CustomSequenceAction();

            //This add the diferent actions for them to be performed one after the other
            sequenceAction.Add(act1);
            //sequenceAction.Add(act2);

            //This activates the actions in the sequenceAction
            myLabel.AddAction(sequenceAction);

            // This checks if the sequence has finished, in which case the initial marquee image is shown again,
            // the actors are removed, and frame deletion is disabled
            sequenceAction.Finished += () =>
            {
                gDmdDevice.Stage.RemoveActor(sceneGroup);
                gDmdDevice.Clear = false;
                DisplayPicture(gGameMarquee, true);
            };

            // The sceneGroup is added as an actor to be shown on the DMD
            gDmdDevice.Stage.AddActor(sceneGroup);
            gDmdDevice.UnlockRenderThread();

        });

        LogIt($"Rendering text: {text}");
        return true;
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
            if (dof2dmdUrl.Contains("v1/"))
            {
                LogIt($"Received request for {req.Url}");
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
    public static int HexToInt(string hexColor)
    {
        // Remove '#' if present
        if (hexColor.IndexOf('#') != -1)
            hexColor = hexColor.Replace("#", "");

        // Convert hexadecimal to integer
        int intValue = Int32.Parse(hexColor, System.Globalization.NumberStyles.HexNumber);

        return intValue;
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

        // This is the set of APIs that DOF2DMD can handle:
        // [url_prefix]/v1/display/picture?path=<path>?fixed=[true|false]&duration=<duration in ms> (without gif or png extension - which is automatically handled)
        // [url_prefix]/v1/display/score?player=<player>&score=<score>
        // [url_prefix]/v1/blank
        // [url_prefix]/v1/exit
        // [url_prefix]/v1/version
        // [url_prefix]/v1/display/scorebackgroundimage?path=<path>&brightness=<brightness 0-15>
        // SEMI IMPLEMENTED : [url_prefix]/v1/display/text?text=<text>?size=[S|M|L]&color=[color]]&font=[font]&bordercolor=[color]&bordersize=[size]
        // NOT IMPLEMENTED : [url_prefix]/v1/display/scene?background =<image or video path>&toptext=<text>&topbrightness=<brightness 0 - 15>&bottomtext=<text>&bottombrightness=<brightness  0 - 15>&animatein=<0 - 15>&animateout=<0 - 15>&pausetime=<pause in ms>
        if (urlParts[1] == "v1")
        {
            switch (urlParts[2])
            {
                case "blank":
                    gGameMarquee = "";
                    gUDmdDevice.CancelRendering();
                    gUDmdDevice.Clear();
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "version":
                    // Return the version number
                    sReturn = "1.0";
                    break;
                case "display":
                    switch (urlParts[3])
                    {
                        case "picture":
                            // [url_prefix]/v1/display/picture?path=<path>?fixed=[true|false] (without gif or png extension - which is automatically handled)
                            // Extract parameters:
                            // path = path to the image
                            // fixed = whether the image is fixed (not animated)
                            // duration = duration of the animation
                            string path = query.Get("path");
                            // If argument fixed is present
                            string sfixed = "";
                            if (query.Get("fixed") != null)
                                sfixed = query.Get("fixed");

                            if (query.Count == 1)
                            {
                                // This is certainly a game marquee, provided during new game
                                // If path corresponds to an existing file, set game marquee
                                if (File.Exists($"{AppSettings.artworkPath}/{path}.png"))
                                    gGameMarquee = path;
                                // Reset scores for all players
                                for (int i = 1; i <= 4; i++)
                                    gScore[i] = 0;
                            }
                            if (!DisplayPicture(path, sfixed == "true"))
                            {
                                sReturn = $"Picture not found: {path}";
                            }
                            break;

                        case "picture2":
                            // [url_prefix]/v1/display/picture2?path=<path>&duration=<seconds>
                            // Extract parameters:
                            // path = path to the image
                            // duration = duration of the animation
                            string path2 = query.Get("path");
                            int duration = Convert.ToInt16(query.Get("duration"));
                            // If argument fixed is present

                            if (!DisplayPicture2(path2, duration))
                            {
                                sReturn = $"Picture not found: {path2}";
                            }
                            break;

                        case "video":
                            // [url_prefix]/v1/display/video?path=<path>
                            // Extract parameters:
                            // path = path to the video
                            string videopath = query.Get("path");
                            // If argument fixed is present
                            if (!DisplayVideo(videopath))
                            {
                                sReturn = $"Video not found: {videopath}";
                            }
                            break;

                        case "score":
                            // [url_prefix]/v1/display/score?player=<player>&score=<score>
                            int score = int.Parse(query.Get("score"));
                            int player = int.Parse(query.Get("player"));
                            if (player > gNbPlayers)
                                gNbPlayers = player;
                            gActivePlayer = player;
                            DisplayScore(player, score);
                            break;
                        case "scorebackgroundimage":
                            //[url_prefix] / v1 / display / scorebackgroundimage ? path =< path > &brightness =< brightness 0 - 15 >
                            string scorebackground = query.Get("path");
                            int brightness = int.Parse(query.Get("brightness"));
                            LogIt($"Score Background is now set to: {scorebackground} with brightness {brightness}");
                            DisplayScorebackground(scorebackground, brightness);
                            break;
                        case "text":
                            // [url_prefix] / v1 / display / text ? text =< text >? size = [S | M | L] & color=[color]&font=[font]&bordercolor=[color]&bordersize=[size]
                            string text = query.Get("text") ?? "";
                            string size = query.Get("size") ?? "M"; // Not currently implemented
                            string color = query.Get("color") ?? "white";
                            string font = query.Get("font") ?? "bm_army-12";
                            string bordercolor = query.Get("bordercolor") ?? "red";
                            string bordersize = query.Get("bordersize") ?? "1";
                            LogIt($"Text is now set to: {text} with size {size} ,color {color} ,font {font} ,border color {bordercolor} and border size {bordersize}");

                            if (DisplayText(text, size, color, font, bordercolor, bordersize)) 
                            {
                                sReturn = "OK";
                            } else
                            {
                                sReturn = "Error when displaying text";
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
        else
            return "Not implemented";
    }

}
/// <summary>
/// Extends the FlexDMD SequenceAction class of FlexDMD to be able to know 
/// when a sequence of actions ends by adding the Finished method
/// </summary>
public class CustomSequenceAction : FlexDMD.SequenceAction
{
    public event System.Action Finished;

    private readonly List<FlexDMD.Action> _actions = new List<FlexDMD.Action>();
    private int _pos = 0;

    public new ICompositeAction Add(FlexDMD.Action action)
    {
        _actions.Add(action);
        return this;
    }

    public override bool Update(float secondsElapsed)
    {
        if (_pos >= _actions.Count)
        {
            // Prepare for restart
            _pos = 0;
            Finished?.Invoke();
            return true;
        }
        while (_actions[_pos].Update(secondsElapsed))
        {
            _pos++;
            if (_pos >= _actions.Count)
            {
                // Prepare for restart
                _pos = 0;
                Finished?.Invoke();
                return true;
            }
        }
        return false;
    }
}
