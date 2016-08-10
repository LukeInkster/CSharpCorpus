﻿#region using directives

using System;
using System.Globalization;
using System.Threading;
using PoGo.NecroBot.Logic;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Tasks;
using PoGo.NecroBot.Logic.Utils;
using System.IO;
using System.Net;
using PoGo.NecroBot.CLI.Resources;
using System.Reflection;
using PoGo.NecroBot.CLI.Plugin;
using System.Net.Http;

#endregion

namespace PoGo.NecroBot.CLI
{
    internal class Program
    {
        private static readonly ManualResetEvent QuitEvent = new ManualResetEvent(false);
        private static string subPath = "";
        private static Uri strKillSwitchUri = new Uri("https://raw.githubusercontent.com/NECROBOTIO/NecroBot/master/KillSwitch.txt");

        private static void Main(string[] args)
        {
            string strCulture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;

            var culture = CultureInfo.CreateSpecificCulture( "en" );
            CultureInfo.DefaultThreadCurrentCulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;

            Console.Title = "NecroBot";
            Console.CancelKeyPress += (sender, eArgs) =>
            {
                QuitEvent.Set();
                eArgs.Cancel = true;
            };
            if (args.Length > 0)
                subPath = args[0];

            var logger = new ConsoleLogger(LogLevel.LevelUp);
            Logger.SetLogger(logger, subPath);

            if (CheckKillSwitch())
                return;

            var profilePath = Path.Combine(Directory.GetCurrentDirectory(), subPath);
            var profileConfigPath = Path.Combine(profilePath, "config");
            var configFile = Path.Combine(profileConfigPath, "config.json");

            GlobalSettings settings;
            Boolean boolNeedsSetup = false;

            if (File.Exists(configFile))
            {
                if (!VersionCheckState.IsLatest())
                    settings = GlobalSettings.Load(subPath, true);
                else
                    settings = GlobalSettings.Load(subPath);
            }
            else
            {
                settings = new GlobalSettings();
                settings.ProfilePath = profilePath;
                settings.ProfileConfigPath = profileConfigPath;
                settings.GeneralConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config");
                settings.TranslationLanguageCode = strCulture;

                boolNeedsSetup = true;
            }

            if (args.Length > 1)
            {
                string[] crds = args[1].Split(',');
                double lat, lng;
                try
                {
                    lat = Double.Parse(crds[0]);
                    lng = Double.Parse(crds[1]);
                    settings.DefaultLatitude = lat;
                    settings.DefaultLongitude = lng;
                }
                catch (Exception) { }
            }


            var session = new Session(new ClientSettings(settings), new LogicSettings(settings));

            if (boolNeedsSetup)
            {
                if (GlobalSettings.PromptForSetup(session.Translation) && !settings.isGui)
                {
                    session = GlobalSettings.SetupSettings(session, settings, configFile);

                    if (!settings.isGui)
                    {
                        var fileName = Assembly.GetExecutingAssembly().Location;
                        System.Diagnostics.Process.Start(fileName);
                        Environment.Exit(0);
                    }
                }
                else
                {
                    GlobalSettings.Load(subPath);

                    Logger.Write("Press a Key to continue...",
                        LogLevel.Warning);
                    Console.ReadKey();
                    return;
                }

            }
            ProgressBar.start("NecroBot is starting up", 10);

            session.Client.ApiFailure = new ApiFailureStrategy(session);
            ProgressBar.fill(20);

            /*SimpleSession session = new SimpleSession
            {
                _client = new PokemonGo.RocketAPI.Client(new ClientSettings(settings)),
                _dispatcher = new EventDispatcher(),
                _localizer = new Localizer()
            };

            BotService service = new BotService
            {
                _session = session,
                _loginTask = new Login(session)
            };

            service.Run();
            */

            var machine = new StateMachine();
            var stats = new Statistics();

            ProgressBar.fill(30);
            string strVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            stats.DirtyEvent +=
                () =>
                    Console.Title = $"[Necrobot v{strVersion}] " +
                        stats.GetTemplatedStats(
                            session.Translation.GetTranslation(TranslationString.StatsTemplateString),
                            session.Translation.GetTranslation(TranslationString.StatsXpTemplateString));
            ProgressBar.fill(40);

            var aggregator = new StatisticsAggregator(stats);
            ProgressBar.fill(50);
            var listener = new ConsoleEventListener();
            ProgressBar.fill(60);

            session.EventDispatcher.EventReceived += evt => listener.Listen(evt, session);
            session.EventDispatcher.EventReceived += evt => aggregator.Listen(evt, session);
            if (settings.UseWebsocket)
            {
                var websocket = new WebSocketInterface(settings.WebSocketPort, session);
                session.EventDispatcher.EventReceived += evt => websocket.Listen(evt, session);
            }

            var plugins = new PluginManager(new PluginInitializerInfo()
            {
                Logger = logger,
                Session = session,
                Settings = settings,
                Statistics = stats
            });
            plugins.InitPlugins();

            ProgressBar.fill(70);

            machine.SetFailureState(new LoginState());
            ProgressBar.fill(80);

            Logger.SetLoggerContext(session);
            ProgressBar.fill(90);

            session.Navigation.UpdatePositionEvent +=
                (lat, lng) => session.EventDispatcher.Send(new UpdatePositionEvent { Latitude = lat, Longitude = lng });
            session.Navigation.UpdatePositionEvent += Navigation_UpdatePositionEvent;

            ProgressBar.fill(100);

            machine.AsyncStart(new VersionCheckState(), session);
            
            if (settings.UseTelegramAPI)
            {
                session.Telegram = new Logic.Service.TelegramService(settings.TelegramAPIKey, session);
            }

            if (session.LogicSettings.UseSnipeLocationServer)
                SnipePokemonTask.AsyncStart(session);

            try
            {
                Console.Clear();
            }
            catch (IOException) { }

            settings.checkProxy();

            QuitEvent.WaitOne();
        }

        private static void Navigation_UpdatePositionEvent(double lat, double lng)
        {
            SaveLocationToDisk(lat, lng);
        }

        private static void SaveLocationToDisk(double lat, double lng)
        {
            var coordsPath = Path.Combine(Directory.GetCurrentDirectory(), subPath, "Config", "LastPos.ini");

            File.WriteAllText(coordsPath, $"{lat}:{lng}");
        }

        private static bool CheckKillSwitch()
        {
            using (var wC = new NecroWebClient())
            {
                try
                {
                    string strResponse = WebClientExtensions.DownloadString(wC, strKillSwitchUri);
                    string[] strSplit = strResponse.Split(';');

                    if (strSplit.Length > 1)
                    {
                        string strStatus = strSplit[0];
                        string strReason = strSplit[1];

                        if (strStatus.ToLower().Contains("disable"))
                        {
                            Console.WriteLine(strReason + "\n");

                            Logger.Write("The bot will now close, please press enter to continue", LogLevel.Error);
                            Console.ReadLine();
                            return true;
                        }
                    }
                    else
                        return false;
                }
                catch (WebException)
                {
                }
            }

            return false;
        }

        private static void UnhandledExceptionEventHandler(object obj, UnhandledExceptionEventArgs args)
        {
            Logger.Write("Exception caught, writing LogBuffer.", force: true);
            throw new Exception();
        }
    }
}
