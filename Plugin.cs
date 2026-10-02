using System;
using System.Collections.Generic;
using System.IO;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using Exiled.Events.EventArgs.Player;
using Newtonsoft.Json;

namespace PlaytimeRanks
{
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;
        
        public Dictionary<string, int> RanksByMinutes { get; set; } = new Dictionary<string, int>
        {
            { "rekrut", 0 },         // 0 godzin
            { "szeregowy", 300 },    // 5 godzin
            { "kapral", 600 },       // 10 godzin
            { "plutonowy", 900 },    // 15 godzin
            { "sierzant", 1440 },    // 24 godziny
            { "porucznik", 1800 },   // 30 godzin
            { "kapitan", 3000 },     // 50 godzin
            { "major", 6000 },       // 100 godzin
            { "pulkownik", 12000 },  // 200 godzin
            { "general", 24000 }     // 400 godzin
        };
    }

    public class PlaytimeRanksPlugin : Plugin<Config>
    {
        public override string Name => "CzasoweRangi";
        public override string Author => "Gemini";
        public override Version Version => new Version(1, 1, 0);

        public static PlaytimeRanksPlugin Instance;
        private EventHandler _handler;

        public override void OnEnabled()
        {
            Instance = this;
            _handler = new EventHandler();
            
            Exiled.Events.Handlers.Player.Verified += _handler.OnVerified;
            Exiled.Events.Handlers.Player.Destroying += _handler.OnDestroying;
            
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Verified -= _handler.OnVerified;
            Exiled.Events.Handlers.Player.Destroying -= _handler.OnDestroying;
            
            _handler = null;
            Instance = null;
            
            base.OnDisabled();
        }
    }

    public class EventHandler
    {
        private Dictionary<string, DateTime> _joinTimes = new Dictionary<string, DateTime>();
        private Dictionary<string, double> _playtimes = new Dictionary<string, double>();
        private string _dataPath = Path.Combine(Paths.Configs, "playtimes.json");

        public EventHandler()
        {
            LoadData();
        }

        public void OnVerified(VerifiedEventArgs ev)
        {
            _joinTimes[ev.Player.UserId] = DateTime.Now;

            if (!_playtimes.ContainsKey(ev.Player.UserId))
            {
                _playtimes[ev.Player.UserId] = 0;
            }
            
            double totalMinutes = _playtimes[ev.Player.UserId];
            AssignRank(ev.Player, totalMinutes);
        }

        public void OnDestroying(DestroyingEventArgs ev)
        {
            if (_joinTimes.ContainsKey(ev.Player.UserId))
            {
                var sessionMinutes = (DateTime.Now - _joinTimes[ev.Player.UserId]).TotalMinutes;
                
                if (!_playtimes.ContainsKey(ev.Player.UserId))
                    _playtimes[ev.Player.UserId] = 0;
                
                _playtimes[ev.Player.UserId] += sessionMinutes;
                _joinTimes.Remove(ev.Player.UserId);
                
                SaveData(); 
            }
        }

        private void AssignRank(Player player, double totalMinutes)
        {
            if (player.Group != null && player.Group.Permissions > 0)
                return; 

            string highestRank = null;
            int maxRequired = -1;

            foreach (var rank in PlaytimeRanksPlugin.Instance.Config.RanksByMinutes)
            {
                if (totalMinutes >= rank.Value && rank.Value > maxRequired)
                {
                    highestRank = rank.Key;
                    maxRequired = rank.Value;
                }
            }

            if (highestRank != null)
            {
                UserGroup group = ServerStatic.PermissionsHandler.GetGroup(highestRank);
                if (group != null)
                {
                    player.Group = group;
                }
                else
                {
                    Log.Warn($"Grupa '{highestRank}' nie istnieje w config_remoteadmin.txt!");
                }
            }
        }

        private void LoadData()
        {
            if (File.Exists(_dataPath))
                _playtimes = JsonConvert.DeserializeObject<Dictionary<string, double>>(File.ReadAllText(_dataPath));
        }

        private void SaveData()
        {
            File.WriteAllText(_dataPath, JsonConvert.SerializeObject(_playtimes, Formatting.Indented));
        }
    }
}
