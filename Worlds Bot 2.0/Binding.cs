//binding classes to help with parsing json
namespace Worlds_Bot_2._0
{
    public class Secrets
    {
        public string discordToken;
        public string robotEventsToken;
    }
    public class DivisionInfo
    {
        public int id;
        public string name;
        public int order;
    }
    public class Teaminfo
    {
        public int id;
        public string name;
    }
    public class Event
    {
        public List<DivisionInfo> divisions;
    }
    public class Meta
    {
        public int last_page;
    }
    public class Team
    {
        public DivisionInfo division;
        public Teaminfo team;
        public int wins;
        public int losses;
        public int ties;
    }
    public class Division
    {
        public Meta meta;
        public List<Team> data;
    }
    public class Schedule
    {
        public Meta meta;
        public List<Match> data;
    }
    public class Match
    {
        public string name;
        public string started;
        public List<Alliance> alliances;
    }
    public class Alliance
    {
        public string color;
        public int score;
        public List<Team> teams;
    }

}
