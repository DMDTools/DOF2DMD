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
using System.IO;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Web;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Text;

class Program
{
    public static FlexDMD.IFlexDMD gDmd;
    public static FlexDMD.IUltraDMD gUdmd;
    public static int[] gScore = new int[5] { 0, 0, 0, 0, 0 };
    public static int gActivePlayer = 1;
    public static int gNbPlayers = 1;
    public static string gGameMarquee = "DOF2DMD";
    private static Timer _sceneQueueTimer;
    private static readonly object _sceneQueueLock = new object();
    private static DateTime _lastDisplayScoreCall = DateTime.MinValue;
    private static DateTime _lastDisplayScore = DateTime.MinValue;
    private static readonly object _displayScoreLock = new object();

    static void Main(string[] args)
    {
        // Set up logging to a file
        Trace.Listeners.Add(new TextWriterTraceListener("debug.log") { TraceOutputOptions = TraceOptions.Timestamp });
        //Trace.Listeners.Add(new ConsoleTraceListener() { TraceOutputOptions = TraceOptions.Timestamp });
        Trace.AutoFlush = true;

        // Initializing DMD
        gDmd = new FlexDMD.FlexDMD
        {
            Width = 128,
            Height = 32,
            GameName = "DOF2DMD",
            Color = Color.Aqua,
            RenderMode = RenderMode.DMD_RGB,
            Show = true,
            Run = true
        };
        gUdmd = gDmd.NewUltraDMD();

        // Initialize the timer
        _sceneQueueTimer = new Timer(SceneTimer, null, 1000, 1000);

        Trace.WriteLine($"URL Prefix: {AppSettings.UrlPrefix}");
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add($"{AppSettings.UrlPrefix}/");
        listener.Start();

        Trace.WriteLine($"DOF2DMD is now listening for requests on {AppSettings.UrlPrefix}...");

        Task listenTask = HandleIncomingConnections(listener);
        listenTask.GetAwaiter().GetResult();
    }

    private static void SceneTimer(object state)
    {
        LogIt("SceneTimer");
        lock (_sceneQueueLock)
        {
            // If score has not changed, we should display the Game Marquee
            // Priority order : animation, score (if changed, and for X seconds), marquee
            if (!gUdmd.IsRendering())
            {
                // Display the marquee if the score has been displayed since more than configured seconds
                if ((DateTime.Now - _lastDisplayScoreCall).TotalMilliseconds > AppSettings.displayScoreDuration * 1000)
                {
                    LogIt($"  ⏱️Displaying marquee {gGameMarquee} after score displayed for {AppSettings.displayScoreDuration}s");
                    DisplayPicture(gGameMarquee, true, 99999);
                }
                else if (gScore[gActivePlayer] > 0)
                {
                    LogIt($"  ⏱️Displaying score {gScore[gActivePlayer].ToString()}");
                    DisplayScore(gScore[gActivePlayer].ToString());
                }
            }
        }
    }

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
        public static string SceneDefault => _configuration["scene_default"] ?? "marquee";
        public static int NumberOfDmd => Int32.Parse(_configuration["number_of_dmd"] ?? "1");
        public static int AnimationDmd => Int32.Parse(_configuration["animation_dmd"] ?? "1");
        public static int ScoreDmd => Int32.Parse(_configuration["score_dmd"] ?? "1");
        public static int marqueeDmd => Int32.Parse(_configuration["marquee_dmd"] ?? "1");
        public static bool pixelCadeEmu => Boolean.Parse(_configuration["pixelcade_emu"] ?? "true");
        public static int displayScoreDuration => Int32.Parse(_configuration["display_score_duration_s"] ?? "4");
        public static bool Debug => Boolean.Parse(_configuration["debug"] ?? "true");
        public static string artworkPath => _configuration["artwork_path"] ?? "artwork";
    }

    public struct ImageInfo
    {
        public int Width;
        public int Height;
        public bool IsAnimated;
        public bool IsLooped;
        public int AnimationLength; // In milliseconds
    }

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
                    int this_delay = 0;
                    int index = 0;

                    for (int f = 0; f < frameCount; f++)
                    {
                        this_delay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, index) * 10;
                        delay += (this_delay < 100 ? 100 : this_delay);  // Minimum delay is 100 ms
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
    /// Translates a PixelCade API call to a DOF2DMD API call.
    /// </summary>
    /// <param name="pixelCadeUrl">The PixelCade API call URL to be translated.</param>
    /// <returns>The translated DOF2DMD API call URL.</returns>
    /// <exception cref="ArgumentException">Thrown when the PixelCade API call is not supported.</exception>
    /// <remarks>
    /// This function supports the following PixelCade API calls:
    /// - stream: Translates to the DOF2DMD `display/picture` endpoint.
    /// - text: Translates to the DOF2DMD `display/text` endpoint.
    /// - score: Translates to the DOF2DMD `display/score` endpoint.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// string pixelCadeUrl = "http://127.0.0.1:8080/arcade/stream/mameoutput/galaga_newship?l=1&ledonly";
    /// string dof2dmdUrl = TranslatePixelCadeToDoF2DMD(pixelCadeUrl);
    /// Console.WriteLine(dof2dmdUrl); // Output: [url_prefix]/v1/display/picture?path=mameoutput%2Fgalaga_newship&duration=1
    /// </code>
    /// </example>
    public static string TranslatePixelCadeToDOF2DMD(string pixelCadeUrl)
    {
        if(pixelCadeUrl.Contains("/v1/")) {
            // Does not look like PixelCade URL, just pass as is
            return pixelCadeUrl;
        }
        var url = new Uri(pixelCadeUrl);
        var query = HttpUtility.ParseQueryString(url.Query);

        var dof2dmdUrl = $"{AppSettings.UrlPrefix}/v1/";
        var dof2dmdParams = new Dictionary<string, string>();

        switch (url.Segments[1].TrimEnd('/'))
        {
            case "arcade":
                if (url.Segments[2].TrimEnd('/') == "stream")
                {
                    dof2dmdUrl += "display/picture";
                    var path = string.Join("", url.Segments.Skip(3));
                    dof2dmdParams["path"] = HttpUtility.UrlEncode(path);

                    if (query["l"] != null)
                        dof2dmdParams["duration"] = query["l"];

                    if (pixelCadeUrl.Contains("nogif"))
                        dof2dmdParams["fixed"] = "true";
                }
                break;

            case "text":
                dof2dmdUrl += "display/text";
                dof2dmdParams["text"] = query["t"];
                break;

            case "score":
                dof2dmdUrl += "display/score";
                if (query["s"] != null)
                {
                    var score = query["s"];
                    dof2dmdParams["score"] = score;
                }
                break;

            default:
                Trace.WriteLine("Unsupported PixelCade API call");
                break;
        }

        var queryString = string.Join("&", dof2dmdParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        return $"{dof2dmdUrl}?{queryString}";
    }

    // Log only if debug is enabled
    public static void LogIt(string message)
    {
        // If debug is enabled
        if (AppSettings.Debug)
        {
            Trace.WriteLine(message);
        }
    }

    public static Boolean DisplayPicture(string path, bool bfixed, int duration)
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
            }

            if (File.Exists(fileToUse)) {
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
                // LogIt($"🔥DOF2DMD DisplayPicture {fileToUse}");
                gUdmd.DisplayScene00($"{fileToUse}", "", -1, "", -1, 14, gifDuration, 14);
                if (fileToUse.EndsWith(".gif"))
                {
                    // After a Gif, always queue the game Marquee to avoid showing last Gif frame for too long
                    DisplayPicture(gGameMarquee, true, 99999);
                }
            } else {
                LogIt($"  File not found: {curDir}/{fileToUse}");
            }
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"  Error occurred while fetching the image. {ex.Message}");
            return false;
        }
    }

    public static Boolean DisplayScore(string score)
    {
        // If score is an int, then this is the score. Otherwise, this is the player number
        if (int.TryParse(score, out int scoreInt))
        {
            gScore[gActivePlayer] = scoreInt;
            if (!gUdmd.IsRendering())
            {
                LogIt($"DisplayScore: {score}");
                _lastDisplayScore = DateTime.Now;
                gUdmd.DisplayScoreboard(gNbPlayers, gActivePlayer, gScore[1], gScore[2], gScore[3], gScore[4], $"PLAYER {gActivePlayer}", "");
            }
        }
        else
        {
            // If score is not empty string
            if (score != "")
            {
                // Extract the Active player from the score : if score="PLAYER N", then gActivePlayer = N
                try
                {
                    gActivePlayer = int.Parse(score.Replace("PLAYER ", ""));

                    if (gActivePlayer > gNbPlayers)
                        gNbPlayers=gActivePlayer;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error while parsing score {score} : {ex.Message}");
                }
            }
        }
        return true;
    }

    static async Task HandleIncomingConnections(HttpListener listener)
    {
        bool runServer = true;
        while (runServer)
        {
            HttpListenerContext ctx = await listener.GetContextAsync();

            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            LogIt($"Received request for {req.Url}");

            string dof2dmdUrl = req.Url.ToString();
            if (AppSettings.pixelCadeEmu)
                // PixelCade emulation
                dof2dmdUrl = TranslatePixelCadeToDOF2DMD(dof2dmdUrl);

            string sResponse = ProcessRequest(dof2dmdUrl);
            // Send "OK" as answer in body of the response
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

        gDmd.Run = false;
    }

    private static string ProcessRequest(string dof2dmdUrl)
    {
        var newUrl = new Uri(dof2dmdUrl);
        var query = HttpUtility.ParseQueryString(newUrl.Query);
        string sReturn = "OK";

        string[] urlParts = newUrl.AbsolutePath.Split('/');
        
        // This is the set of APIs that DOF2DMD can handle:
        // [url_prefix]/v1/display/picture?path=<path>?animated=[true|false]&duration=<duration in ms> (without gif or png extension - which is automatically handled)
        // NOT IMPLEMENTED : [url_prefix]/v1/display/scorebackgroundimage?path=<path>&brightness=<brightness 0-15>
        // [url_prefix]/v1/display/score?player=<player>&score=<score>
        // NOT IMPLEMENTED : [url_prefix]/v1/display/text?text=<text>?size=[S|M|L]&color=#FFFFFF&font=[font]&bordercolor=[color]&bordersize=[size]
        // NOT IMPLEMENTED : [url_prefix]/v1/display/scene?background =<image or video path>&toptext=<text>&topbrightness=<brightness 0 - 15>&bottomtext=<text>&bottombrightness=<brightness  0 - 15>&animatein=<0 - 15>&animateout=<0 - 15>&pausetime=<pause in ms>
        // [url_prefix]/v1/blank
        // [url_prefix]/v1/exit
        // [url_prefix]/v1/version
        if (urlParts[1] == "v1")
        {
            switch (urlParts[2])
            {
                case "blank":
                    gGameMarquee = "";
                    gUdmd.CancelRendering();
                    gUdmd.Clear();
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
                            // [url_prefix]/v1/display/picture?path=<path>?animated=[true|false]&duration=<duration in ms> (without gif or png extension - which is automatically handled)
                            // Extract parameters:
                            // path = path to the image
                            // animated = whether the image is animated
                            // duration = duration of the animation
                            string path = query.Get("path");
                            // If argument fixed is present
                            string sfixed = "";
                            if (query.Get("fixed") != null)
                                sfixed = query.Get("fixed");
                            string duration = "1";
                            if (query.Get("duration") != null)
                                duration = query.Get("duration");

                            if (query.Count == 1)
                            {
                                // This is a game marquee, provided during new game
                                gGameMarquee = path;
                                // Reset scores for all players
                                for (int i = 1; i <= 4; i++)
                                    gScore[i] = 0;

                            }
                            // if fixed and duration = 99999, then change marquee, but do not display it
                            if (sfixed == "true" && duration == "99999") {
                                LogIt($"GameMarquee is now set to: {path}");
                                gGameMarquee = path;
                            } else if (!gUdmd.IsRendering()) {
                                // Show picture only if there is no rendering going on
                                DisplayPicture(path, sfixed == "true", int.Parse(duration));
                            }
                            break;
                        case "score":
                            // [url_prefix]/v1/display/score?player=<player>&score=<score>
                            string score = query.Get("score");
                            _lastDisplayScoreCall = DateTime.Now;
                            DisplayScore(score);
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

