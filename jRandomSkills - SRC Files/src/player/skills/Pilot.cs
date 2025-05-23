using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using jRandomSkills.src.player;
using jRandomSkills.src.utils;
using static CounterStrikeSharp.API.Core.Listeners;
using static jRandomSkills.jRandomSkills;

namespace jRandomSkills
{
    public class Pilot : ISkill
    {
        private static Skills skillName = Skills.Pilot;
        private static readonly Dictionary<ulong, Pilot_PlayerInfo> PlayerPilotInfo = new Dictionary<ulong, Pilot_PlayerInfo>();

        public static void LoadSkill()
        {
            if (Config.config.SkillsInfo.FirstOrDefault(s => s.Name == skillName.ToString())?.Active != true)
                return;

            Utils.RegisterSkill(skillName, "#1466F5");
            
            Instance.RegisterEventHandler<EventRoundFreezeEnd>((@event, info) =>
            {
                Instance.AddTimer(0.1f, () => 
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        var playerInfo = Instance.skillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
                        if (playerInfo?.Skill == skillName)
                        {
                            EnableSkill(player);
                        }
                    }
                });

                return HookResult.Continue;
            });

            Instance.RegisterEventHandler<EventPlayerDeath>((@event, info) =>
            {
                var player = @event.Userid;
                
                var playerInfo = Instance.skillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
                if (playerInfo?.Skill == skillName)
                {
                    if (PlayerPilotInfo.ContainsKey(player.SteamID))
                        PlayerPilotInfo.Remove(player.SteamID);
                }

                return HookResult.Continue;
            });

            Instance.RegisterListener<OnTick>(() =>
            {
                foreach (var player in Utilities.GetPlayers())
                {
                    var playerInfo = Instance.skillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
                    if (playerInfo?.Skill == skillName)
                        HandlePilot(player);
                }
            });
        }

        public static void EnableSkill(CCSPlayerController player)
        {
            PlayerPilotInfo[player.SteamID] = new Pilot_PlayerInfo
            {
                SteamID = player.SteamID,
                PressedUse = false,
                CanUsePilot = true,
                PilotStartTime = DateTime.MinValue
            };
        }

        public static void DisableSkill(CCSPlayerController player)
        {
            if (PlayerPilotInfo.ContainsKey(player.SteamID))
                PlayerPilotInfo.Remove(player.SteamID);
        }

        private static void HandlePilot(CCSPlayerController player)
        {
            var buttons = player.Buttons;
            if (PlayerPilotInfo.TryGetValue(player.SteamID, out var pilotInfo))
            {
                if (buttons.HasFlag(PlayerButtons.Use))
                {
                    if (!pilotInfo.PressedUse)
                    {
                        pilotInfo.CanUsePilot = true;
                        pilotInfo.PressedUse = true;
                        pilotInfo.PilotStartTime = DateTime.Now;
                        Instance.AddTimer(4.0f, () => ChangeUsePlayerPilot(player));
                    }

                    if (pilotInfo.CanUsePilot)
                    {
                        ApplyPilotEffect(player);
                    }
                }

                UpdateHUD(player, pilotInfo);
            }
        }

        private static void UpdateHUD(CCSPlayerController player, Pilot_PlayerInfo pilotInfo)
        {
            var buttons = player.Buttons;
            float fuelPercentage = 100.0f;

            if (pilotInfo.PressedUse && pilotInfo.CanUsePilot)
            {
                float elapsedTime = (float)(DateTime.Now - pilotInfo.PilotStartTime).TotalSeconds;
                fuelPercentage = Math.Max(0, 100.0f * (4.0f - elapsedTime) / 4.0f);
            }

            string fuelColor = GetFuelColor(fuelPercentage);
            string infoLine = buttons.HasFlag(PlayerButtons.Use) ? $"<font color='#FFFFFF'>{Localization.GetTranslation("pilot")}:</font> <br>" : "";
            string remainingLine = buttons.HasFlag(PlayerButtons.Use) ? 
                (pilotInfo.CanUsePilot ? $"<font color='{fuelColor}'>{fuelPercentage:F0}%</font> <br>" : $"<font color='#FF0000'>{Localization.GetTranslation("pilot_hud_info")}</font> <br>") : "";

            if (buttons.HasFlag(PlayerButtons.Use))
            {
                var hudContent = infoLine + remainingLine;
                player.PrintToCenterHtml(hudContent);
            }
        }

        private static string GetFuelColor(float fuelPercentage)
        {
            if (fuelPercentage > 50) return "#00FF00";
            if (fuelPercentage > 25) return "#FFFF00";
            return "#FF0000";
        }

        private static void ChangeUsePlayerPilot(CCSPlayerController player)
        {
            if (PlayerPilotInfo.TryGetValue(player.SteamID, out var pilotInfo))
            {
                pilotInfo.CanUsePilot = false;
                Instance.AddTimer(2.0f, () =>
                {
                    pilotInfo.PressedUse = false;
                    pilotInfo.CanUsePilot = true;
                });

                pilotInfo.PressedUse = true;
            }
        }

        private static void ApplyPilotEffect(CCSPlayerController player)
        {
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn?.CBodyComponent == null) return;
            
            QAngle eye_angle = playerPawn.EyeAngles;
            double pitch = (Math.PI / 180) * eye_angle.X;
            double yaw = (Math.PI / 180) * eye_angle.Y;
            Vector eye_vector = new Vector((float)(Math.Cos(yaw) * Math.Cos(pitch)), (float)(Math.Sin(yaw) * Math.Cos(pitch)), (float)(-Math.Sin(pitch)));

            Vector currentVelocity = playerPawn.AbsVelocity;

            Vector jetpackVelocity = new Vector(
                eye_vector.X * 5.0f,
                eye_vector.Y * 5.0f,
                0.80f * 15.0f
            );

            float newVelocityX = currentVelocity.X + jetpackVelocity.X;
            float newVelocityY = currentVelocity.Y + jetpackVelocity.Y;
            float newVelocityZ = currentVelocity.Z + jetpackVelocity.Z;

            playerPawn.AbsVelocity.X = newVelocityX;
            playerPawn.AbsVelocity.Y = newVelocityY;
            playerPawn.AbsVelocity.Z = newVelocityZ;
        }


        public class Pilot_PlayerInfo
        {
            public ulong SteamID { get; set; }
            public bool CanUsePilot { get; set; }
            public bool PressedUse { get; set; }
            public DateTime PilotStartTime { get; set; }
        }
    }
}