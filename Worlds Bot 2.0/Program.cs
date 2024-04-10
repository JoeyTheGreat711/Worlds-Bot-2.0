
//invite: https://discord.com/api/oauth2/authorize?client_id=1100380213868777482&permissions=274877910016&scope=bot

using Discord;
using Discord.WebSocket;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Worlds_Bot_2._0
{
    class Program
    {
        //important discord ids
        static ulong masterUserId = 637997156883628046;
        static ulong errorDmChannel = 1100666304463126589;
        static ulong selfId = 1100380213868777482;

        static int eventID = 0;// last worlds: 49726 //kalahari = 48542 open = 48473 2022 = 45258 2023 = 49725 ms = 49726
        static string url = "https://www.robotevents.com/api/v2/events";
        private static DiscordSocketClient discordClient;
        static List<List<string>> allTeams = new List<List<string>>();
        static List<string> divisions = new List<string>();
        static HttpClient roboteventsClient = new HttpClient();
        public static async Task Main()
        {
            StreamReader sr = new StreamReader(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/secrets.json");
            Secrets secrets = JsonConvert.DeserializeObject<Secrets>(sr.ReadToEnd());
            sr.Close();

            Console.Write("enter event sku: ");
            string eventSku = Console.ReadLine();

            roboteventsClient.DefaultRequestHeaders.Add("accept-language", "en");
            roboteventsClient.DefaultRequestHeaders.Add("user-agent", "C# Discord Bot");
            roboteventsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secrets.robotEventsToken);

            eventID = int.Parse("" + JsonConvert.DeserializeObject<dynamic>((await (await roboteventsClient.GetAsync(url + "?sku=" + eventSku)).Content.ReadAsStringAsync())).data[0].id);
            Console.WriteLine("found event id: " + eventID);
            url += "/" + eventID;
            Console.WriteLine("requesting teams...");
            HttpResponseMessage response = await roboteventsClient.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            List<int> divs = JsonConvert.DeserializeObject<Event>(json).divisions.Select(d => d.id).Where(id => id < 100).ToList();
            foreach (int i in divs)
            {
                allTeams.Add(new List<string>());
                int page = 1;
                while (page > 0)
                {
                    Console.WriteLine("requesting div " + i + " page " + page);
                    string target = url + "/divisions/" + i + "/rankings?page=" + page;
                    HttpResponseMessage pageResponse = await roboteventsClient.GetAsync(target);
                    page++;
                    Division data = JsonConvert.DeserializeObject<Division>(await pageResponse.Content.ReadAsStringAsync());
                    if (page > data.meta.last_page)
                        page = 0;
                    allTeams[allTeams.Count - 1].AddRange(data.data.Select(t => t.team.name));
                    if (page == 2)
                        divisions.Add(data.data[0].division.name);
                }
            }

            DiscordSocketConfig config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            discordClient = new DiscordSocketClient(config);
            discordClient.Log += Log;
            discordClient.MessageReceived += MessageRecieved;
            await discordClient.LoginAsync(TokenType.Bot, secrets.discordToken);
            await discordClient.StartAsync();
            await Task.Delay(-1);

        }
        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        private static async Task MessageRecieved(SocketMessage messageData)
        {
            string mostRecentRequest = "";
            string mostRecentResponse = "";
            if (messageData.Author.IsBot) return;
            //if (messageData.Author.Id != masterUserId) return;
            if (messageData.MentionedUsers.Select(u => u.Id).ToList<ulong>().Contains(selfId))
            {
                try
                {
                    List<string> args = messageData.Content.ToUpper().Split(' ').ToList();
                    if (args.Exists(s => allTeams.Exists(d => d.Contains(s))))
                    {
                        string curTeam = args.Find(s => allTeams.Exists(d => d.Contains(s)));
                        int div = allTeams.FindIndex(d => d.Contains(curTeam));
                        int page = 1;
                        List<Match> matches = new List<Match>();
                        while (page > 0)
                        {
                            string target = url + "/divisions/" + (div + 1) + "/matches?page=" + page;
                            mostRecentRequest = target;
                            HttpResponseMessage response = await roboteventsClient.GetAsync(target);
                            mostRecentResponse = await response.Content.ReadAsStringAsync();
                            Schedule schedule = JsonConvert.DeserializeObject<Schedule>(await response.Content.ReadAsStringAsync());
                            foreach (Match m in schedule.data)
                            {
                                if (m.alliances.Exists(a => a.teams.Exists(t => t.team.name == curTeam)))
                                    matches.Add(m);
                            }
                            page++;
                            if (schedule.meta.last_page < page)
                                page = 0;
                        }
                        string reply = "";
                        int W = 0, L = 0, T = 0, unplayed = 0;
                        Console.WriteLine(curTeam);
                        foreach (Match m in matches)
                        {
                            int teamScore = m.alliances.Find(a => a.teams.Exists(t => t.team.name == curTeam)).score;
                            int oppScore = m.alliances.Find(a => !a.teams.Exists(t => t.team.name == curTeam)).score;
                            if (m.name.ToLower().Contains("qualifier"))
                            {
                                if (teamScore + oppScore > 0)
                                {
                                    if (teamScore > oppScore) W++;
                                    if (teamScore < oppScore) L++;
                                    if (teamScore == oppScore) T++;
                                    T += unplayed;
                                    unplayed = 0;
                                }
                                else
                                    unplayed++;
                            }
                            string name = m.name;
                            if (name.ToLower().Contains("qualifier"))
                                name = "Q" + name.Substring(9, name.Length - 9);
                            if (name.ToLower().Contains("practice"))
                                name = "P" + name.Substring(8, name.Length - 8);
                            reply += (name + ':').PadRight(12)
                                + (m.alliances[1].teams[0].team.name.PadRight(7) + m.alliances[1].teams[1].team.name.PadRight(7))
                                + "|"
                                + m.alliances[1].score.ToString().PadLeft(4) + "-" + m.alliances[0].score.ToString().PadRight(4)
                                + "| "
                                + (m.alliances[0].teams[0].team.name.PadRight(7) + m.alliances[0].teams[1].team.name.PadRight(7)) + "\n";
                        }
                        reply = curTeam + " is " + W + "-" + L + "-" + T + " in **" + divisions[div] + "** qualification matches\n```" + reply;
                        reply += "\n```More info: https://vexscouting.com/team/" + curTeam;
                        await messageData.Channel.SendMessageAsync(reply);
                        Console.WriteLine("Displayed " + curTeam.PadRight(6) + " for " + messageData.Author.Username);
                    }

                }
                catch (Exception e)
                {
                    IDMChannel c = (await discordClient.GetDMChannelAsync(errorDmChannel));
                    async Task sendError(string s)
                    {
                        await c.SendMessageAsync("```" + s + "```");
                    }
                    await sendError(e.Message);
                    await sendError(e.StackTrace);
                    await sendError(mostRecentRequest);
                    try
                    {
                        await sendError(mostRecentResponse);
                    }
                    catch
                    {
                        MemoryStream stream = new MemoryStream();
                        StreamWriter writer = new StreamWriter(stream);
                        writer.Write(mostRecentResponse);
                        writer.Flush();
                        stream.Position = 0;
                        await c.SendFileAsync(new FileAttachment(stream, "response.txt"));
                    }
                    await sendError(messageData.Content);
                    await sendError(messageData.Author.Username);
                }
            }
        }
    }
}