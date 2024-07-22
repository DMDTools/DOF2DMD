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
using FlexDMD.Properties;
using System.Collections;


namespace DOF2DMD
{
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
        private static Group DMDScene;
        private static readonly object sceneLock = new object();
        private static Sequence _queue;



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
            gUDmdDevice = gDmdDevice.NewUltraDMD();

            _queue = new Sequence(gDmdDevice);
            _queue.FillParent = true;
            gDmdDevice.Stage.AddActor(_queue);

            DMDScene = (Group)gDmdDevice.NewGroup("Scene");


            // Display start picture as game marquee
            gGameMarquee = AppSettings.StartPicture;
            LogIt($"GameMarquee es: {gGameMarquee}");

            Thread.Sleep(1000);
            DisplayPicture(gGameMarquee, true, 0);


            // Start the http listener
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"{AppSettings.UrlPrefix}/");
            listener.Start();

            Trace.WriteLine($"DOF2DMD is now listening for requests on {AppSettings.UrlPrefix}...");

            Task listenTask = HandleIncomingConnections(listener);
            listenTask.GetAwaiter().GetResult();
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

        /// <summary>
        /// Displays an image file on the DMD device using native FlexDMD capabilities.
        /// </summary>

        public static bool DisplayPicture(string path, bool bfixed, float duration)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                path = AppSettings.artworkPath + "/" + path;
                string localPath = HttpUtility.UrlDecode(path);

                List<string> extensions = new List<string> { ".gif", ".avi", ".mp4", ".png", ".jpg", ".bmp" };

                LogIt($"número de actores: {DMDScene.ChildCount}");

                if (FileExistsWithExtensions(localPath, extensions, out string foundExtension))
                {
                    string fullPath = localPath + foundExtension;

                    List<string> videoExtensions = new List<string> { ".gif", ".avi", ".mp4" };
                    List<string> imageExtensions = new List<string> { ".png", ".jpg", ".bmp" };


                    gDmdDevice.Graphics.Clear(Color.Black);
                    gDmdDevice.Clear = true;

                    if (videoExtensions.Contains(foundExtension.ToLower()))
                    {
                        gDmdDevice.LockRenderThread();
                        var videoActor = (AnimatedActor)gDmdDevice.NewVideo("MyVideo", fullPath);
                        videoActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);

                        if (duration == 0)
                        {
                            duration = videoActor.Length;
                            if (DMDScene.ChildCount >= 1)
                            {
                                DMDScene.RemoveAll();
                            }
                        }

                        DMDScene.AddActor(videoActor);
                        var action1 = new FlexDMD.WaitAction(duration);
                        var action2 = new FlexDMD.ShowAction(videoActor, false);
                        var action3 = new FlexDMD.RemoveFromParentAction(videoActor);
                        var sequenceAction = new FlexDMD.SequenceAction();

                        sequenceAction.Add(action1);
                        sequenceAction.Add(action2);
                        sequenceAction.Add(action3);

                        videoActor.AddAction(sequenceAction);


                        gDmdDevice.Stage.AddActor(DMDScene);
                        gDmdDevice.UnlockRenderThread();

                       
                        LogIt($"Rendering video: {fullPath}");
                        return true;
                    }
                    else if (imageExtensions.Contains(foundExtension.ToLower()))
                    {

                        gDmdDevice.LockRenderThread();

                        var imageActor = (Actor)gDmdDevice.NewImage("MyImage", fullPath);
                        imageActor.SetSize(gDmdDevice.Width, gDmdDevice.Height);
                        /*
                        var sequenceAction = new FlexDMD.SequenceAction();

                        if (duration != 0)
                        {
                            DMDScene.AddActor(imageActor);
                            var action1 = new FlexDMD.WaitAction(duration);
                            var action2 = new FlexDMD.ShowAction(imageActor, false);
                            var action3 = new FlexDMD.RemoveFromParentAction(imageActor);


                            sequenceAction.Add(action1);
                            sequenceAction.Add(action2);
                            sequenceAction.Add(action3);

                        }
                        else
                        {
                            if (DMDScene.ChildCount >= 1)
                            {
                                DMDScene.RemoveAll();
                            }
                            DMDScene.AddActor(imageActor);
                            var action1 = new FlexDMD.ShowAction(imageActor, true);
                            sequenceAction.Add(action1);
                        }

                        imageActor.AddAction(sequenceAction);
                        */
                        _queue.RemoveAllScenes();
                        var bg = new BackgroundScene(gDmdDevice, imageActor, AnimationType.ScrollOnDown, 0, AnimationType.ScrollOffDown, "");
                        _queue.Visible = true;
                        _queue.Enqueue(bg);
                        

                        //gDmdDevice.Stage.AddActor(DMDScene);
                        gDmdDevice.UnlockRenderThread();


                        LogIt($"Rendering image: {fullPath}");
                        return true;
                    }
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"  Error occurred while fetching the image. {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Displays text on the DMD device.
        /// %0A para salto de linea
        /// </summary>
        public static bool DisplayText(string text, string size, string color, string font, string bordercolor, string bordersize, bool cleanbg)
        {
            int border = 0;
            if (bordersize != "0")
            {
                border = 1;
            }

            gDmdDevice.LockRenderThread();
            /*
            if (cleanbg)
            {
                if (DMDScene.ChildCount >= 1)
                {
                    DMDScene.RemoveAll();
                }
            }


            gDmdDevice.Clear = true;
            */
            FlexDMD.Font myFont = gDmdDevice.NewFont("FlexDMD.Resources." + font + ".fnt", Color.FromName(color), Color.FromName(bordercolor), border);
            var labelActor = (Actor)gDmdDevice.NewLabel("MyLabel", myFont, text);
            var fSize = myFont.MeasureFont(text);
            //labelActor.SetPosition(fSize.Width * 2, gDmdDevice.Height / 2);
            //labelActor.SetAlignedPosition(fSize.Width * 2, gDmdDevice.Height / 2, Alignment.Center);
            /*
            DMDScene.AddActor(labelActor);


            var action1 = new FlexDMD.MoveToAction(labelActor, labelActor.X - fSize.Width * 3, labelActor.Y, fSize.Width / 40);
            var action2 = new FlexDMD.RemoveFromParentAction(labelActor);
            var sequenceAction = new FlexDMD.SequenceAction();

            sequenceAction.Add(action1);
            sequenceAction.Add(action2);

            labelActor.AddAction(sequenceAction);

            gDmdDevice.Stage.AddActor(DMDScene);*/
            _queue.RemoveAllScenes();
            var bg = new BackgroundScene(gDmdDevice, labelActor, AnimationType.ScrollOnDown, 0, AnimationType.ScrollOffDown, "");
            _queue.Visible = true;
            _queue.Enqueue(bg);

            gDmdDevice.UnlockRenderThread();


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
        public static int HexToInt(string hexColor)
        {

            // Convert hexadecimal to integer
            int intValue = Int32.Parse(hexColor, System.Globalization.NumberStyles.HexNumber);

            return intValue;
        }
        // Función que comprueba si existe un archivo con alguna de las extensiones
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
        /// Process incoming requests
        /// </summary>
        private static string ProcessRequest(string dof2dmdUrl)
        {
            var newUrl = new Uri(dof2dmdUrl);
            var query = HttpUtility.ParseQueryString(newUrl.Query);
            string sReturn = "OK";

            string[] urlParts = newUrl.AbsolutePath.Split('/');

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
                        sReturn = "1.0";
                        break;
                    case "display":
                        switch (urlParts[3])
                        {
                            case "picture":
                                string picturepath = query.Get("path");
                                float pictureduration = float.Parse(query.Get("duration"));
                                string sfixed = "";
                                if (query.Get("fixed") != null)
                                    sfixed = query.Get("fixed");
                                if (query.Count == 1)
                                {
                                    // This is certainly a game marquee, provided during new game
                                    // If path corresponds to an existing file, set game marquee
                                    if (File.Exists($"{AppSettings.artworkPath}/{picturepath}"))
                                        gGameMarquee = picturepath;
                                    // Reset scores for all players
                                    for (int i = 1; i <= 4; i++)
                                        gScore[i] = 0;
                                }
                                if (!DisplayPicture(picturepath, sfixed == "true", pictureduration))
                                {
                                    sReturn = $"Picture not found: {picturepath}";
                                }
                                break;
                            case "text":
                                string text = query.Get("text") ?? "";
                                string size = query.Get("size") ?? "M";
                                string color = query.Get("color") ?? "white";
                                string font = query.Get("font") ?? "bm_army-12";
                                string bordercolor = query.Get("bordercolor") ?? "red";
                                string bordersize = query.Get("bordersize") ?? "1";
                                LogIt($"Text is now set to: {text} with size {size} ,color {color} ,font {font} ,border color {bordercolor} and border size {bordersize}");
                                bool cleanbg;
                                if (!bool.TryParse(query.Get("cleanbg"), out cleanbg))
                                {
                                    cleanbg = true; // valor predeterminado si la conversión falla
                                }

                                if (DisplayText(text, size, color, font, bordercolor, bordersize, cleanbg))
                                {
                                    sReturn = "OK";
                                }
                                else
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
}
