using System;
using System.Diagnostics;
using System.Collections.Generic;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.RequestParams;

// TODO: Config files
// TODO: May be CLI Class?
// TODO: Cheks if it`s posible to send post
// TODO: May be improve algorithm of casting list of walls

namespace steamer
{

    static class Program
    {
        static void ConfigureLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "steamer.log" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

#if DEBUG
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logconsole);
            config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logfile);
#else
            config.AddRule(NLog.LogLevel.Warn,  NLog.LogLevel.Fatal, logconsole);
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logfile);
#endif
            NLog.LogManager.Configuration = config;
        }

        private static NLog.Logger Logger;
        private static long? MyUserId = null;

        private const int MaxNumberOfPosts = 100;

        static void Main(string[] args)
        {
            string login = Console.ReadLine();
            string password = Console.ReadLine();
            const int appId = 6642279;
            ConfigureLogger();
            Logger = NLog.LogManager.GetCurrentClassLogger();

            Logger.Info($"User Login {login}");
            Logger.Info($"User password is secure");
            Logger.Info($"Aplication ID {appId}");

            using (var api = new VkApi(NLog.LogManager.GetLogger("ApiLogger")))
            {
                try
                {
                    api.Authorize(new ApiAuthParams
                    {
                        ApplicationId = appId,
                        Login = login,
                        Password = password,
                        Settings = Settings.Wall
                    });
                }
                catch (VkApiAuthorizationException e)
                {
                    Logger.Fatal(e, "Wrong login or Password");
                    Environment.Exit(1);
                }
                catch (Exception e)
                {
                    Logger.Fatal(e, "Something strange on authorization");
                    Environment.Exit(1);
                }

                MyUserId = api.UserId;

                WallPostParams wallPost = GetWallPost();

                HashSet<Group> allGroups = new HashSet<Group>();

                foreach (var query in GetSearchQueries())
                {
                    var groups = api.Groups.Search(new GroupsSearchParams
                    {
                        Query = query,
                        Type = GroupType.Page,
                        Sort = GroupSort.Likes,
                        Count = 30
                    });
                    allGroups.UnionWith(groups);
                }

                Logger.Info($"We found {allGroups.Count} groups");
                DebugWriteGroups(allGroups); // Runs only in Debug builds

                var listOfGroups = new List<Group>(allGroups);

                for (int i = 0; i < Math.Min(listOfGroups.Count, MaxNumberOfPosts); ++i)
                {
                    Logger.Debug($"Sending to {listOfGroups[i].Name} with ID {listOfGroups[i].Id}");
                    // OPTIMISE: It`s possible to just add OwnerID to wall post and don`t create more objects
                    api.Wall.Post(new WallPostParams
                    {
                        Message = wallPost.Message,
                        Attachments = wallPost.Attachments,
                        OwnerId = -listOfGroups[i].Id
                    });
                }
            }

            Console.ReadLine();
        }

        private static WallPostParams GetWallPost()
        {
            Console.WriteLine("Enter Message Text. Just press ENTER to send without text");
            Console.Write("> ");
            string message = Console.ReadLine();

            if (message == "")
            {
                Logger.Info("Message text is empty");
                message = null;
            }
            else
            {
                Logger.Info("Message text was recived");
            }


            Console.WriteLine("Photo Attachment. It must be in ur profile. Enter photo id.");
            Console.WriteLine("It`s somthing like 319485641_456239453 after word 'photo'");
            Console.Write("> ");
            string inputRaw = Console.ReadLine();
            string albumIdRaw = null, photoIdRaw = null;
            List<VkNet.Model.Attachments.MediaAttachment> attach = null;

            if (inputRaw == "")
            {
                Logger.Info("Photo ID is empty");
                inputRaw = null;
            }
            else
            {
                albumIdRaw = inputRaw.Split("_")[0];
                photoIdRaw = inputRaw.Split("_")[1];

                Logger.Info("Photo ID was recived");
                Logger.Debug($"Album: {albumIdRaw} Id: {photoIdRaw}");

                if (!long.TryParse(photoIdRaw, out long photoId))
                {
                    Logger.Error("Can`t parse Photo ID to integer representation");
                    throw new InvalidParameterException("Photo ID, Bad input");
                }

                if (!long.TryParse(photoIdRaw, out long albumId))
                {
                    Logger.Error("Can`t parse Album ID to integer representation");
                    throw new InvalidParameterException("Album ID, Bad input");
                }

                long.TryParse(inputRaw, out long TMP);

                attach = new List<VkNet.Model.Attachments.MediaAttachment>();
                attach.Add(new VkNet.Model.Attachments.Photo()
                {
                    Id = photoId,
                    AlbumId = albumId,
                    OwnerId = MyUserId
                });
            }

            if (attach == null && message == null)
            {
                Logger.Error("Empty message");
                throw new InvalidParameterException("Post body is emty");
            }

            return new WallPostParams()
            {
                Message = message,
                Attachments = attach
            };
        }

        private static List<string> GetSearchQueries()
        {
            Logger.Info("Getting querys from stdin");

            const string terminalString = "ALL_DONE";
            Console.WriteLine($"Enter search queries; End list with {terminalString}");
            Console.Write("> ");

            List<string> res = new List<string>();
            string tmp;

            while ((tmp = Console.ReadLine()) != terminalString)
            {
                Logger.Debug($"Getting search query: {tmp}");

                res.Add(tmp);
                Console.Write("> ");
            }

            return res;
        }

        [Conditional("DEBUG")]
        private static void DebugWriteGroups(HashSet<Group> groups)
        {
            foreach (var group in groups)
            {
                Logger.Debug($"Public {group.Name} with ID {group.Id}");
            }
        }
    }
}
