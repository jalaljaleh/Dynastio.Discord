﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Dynastio.Bot;
using Dynastio.Net;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynastio.Bot
{
    public class Program
    {
        public static Random Random = new Random();
        public static DateTime StartUp { get; } = DateTime.UtcNow;
        public static void Main(string[] arg) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            Program.Log("Main Async", "Started");

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            };

            var configuration = Configuration.Get(false ? @"C:\Users\Zhaleh\OneDrive\projects\Dynastio\dynastio.json" : @"C:\Users\Zhaleh\OneDrive\projects\Dynastio\dynastio.debug.json");

            //LocaleService.StartTranslateProccess();
            var languages = new LocaleService();

            var graphicService = new GraphicService().Initialize();

            var mongoService = await new MongoService(configuration.MongoConnection).InitializeAsync();

            var dynastClient = new DynastioClient(configuration.DynastioApi);

            var userService = new UserService(mongoService, dynastClient);

            var guildService = new GuildService(mongoService);

            BsonClassMap.RegisterClassMap<User>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(x => new User(userService));
            });
            BsonClassMap.RegisterClassMap<Guild>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(x => new Guild(guildService));
            });


            var services = new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton(dynastClient)
                .AddSingleton(mongoService)
                .AddSingleton(userService)
                .AddSingleton(guildService)
                .AddSingleton(graphicService)
                .AddSingleton(_socketConfig)
                .AddSingleton(languages)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<InteractionHandler>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<EventHandler>()
                .BuildServiceProvider();

            await RunAsync(services);
        }
        private readonly DiscordSocketConfig _socketConfig = new()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages,
            AlwaysDownloadUsers = true,
            MessageCacheSize = 30,

        };
        public async Task RunAsync(IServiceProvider _services)
        {
            var client = _services.GetRequiredService<DiscordSocketClient>();

            client.Log += LogAsync;

            _services.GetRequiredService<EventHandler>().Initialize();
            // Here we can initialize the service that will register and execute our commands
            await _services.GetRequiredService<InteractionHandler>().InitializeAsync();
            await _services.GetRequiredService<CommandHandler>().InitializeAsync();

            // Bot token can be provided from the Configuration object we set up earlier
            await client.LoginAsync(TokenType.Bot, _services.GetRequiredService<Configuration>().BotToken);
            await client.StartAsync();

            // Never quit the program until manually forced to.
            await Task.Delay(Timeout.Infinite);

        }
        private async Task LogAsync(LogMessage message) => Console.WriteLine(message.ToString());
        public static void Log(string service, string text)
        {
            Console.WriteLine(DateTime.UtcNow.ToString("T") + " " + service.PadRight(20) + text);

        }
        public static bool IsDebug()
        {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
}