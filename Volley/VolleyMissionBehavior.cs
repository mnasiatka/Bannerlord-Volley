using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Volley
{
    class VolleyMissionBehavior : MissionBehavior
    {
        private bool ReleaseByRank = false;
        private InputKey DefaultVolleyKey = InputKey.U;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        private Dictionary<FormationClass, bool> VolleyActiveMap = new Dictionary<FormationClass, bool>();
        private Dictionary<(FormationClass, WeaponClass?), bool> FormationHasUnitsReloading = new Dictionary<(FormationClass, WeaponClass?), bool>();
        private Dictionary<FormationClass, Formation> FormationByFormationClass = new Dictionary<FormationClass, Formation>();
        private FormationClass SelectedFormation;

        private Dictionary<int, FormationClass> FormationGroupKeys = new Dictionary<int, FormationClass>() {
            { MissionOrderHotkeyCategory.Group0Hear, FormationClass.Infantry },
            { MissionOrderHotkeyCategory.Group1Hear, FormationClass.Ranged },
            { MissionOrderHotkeyCategory.Group2Hear, FormationClass.Cavalry },
            { MissionOrderHotkeyCategory.Group3Hear, FormationClass.HorseArcher },
            { MissionOrderHotkeyCategory.Group4Hear, FormationClass.Skirmisher },
            { MissionOrderHotkeyCategory.Group5Hear, FormationClass.HeavyInfantry },
            { MissionOrderHotkeyCategory.Group6Hear, FormationClass.LightCavalry },
            { MissionOrderHotkeyCategory.Group7Hear, FormationClass.HeavyCavalry },
        };

        public VolleyMissionBehavior()
        {
        }

        public override void OnMissionTick(float dt)
        {
            if (Agent.Main == null)
            {
                return;
            }
            RunVolleyLogic();
        }

        void RunVolleyLogic()
        {
            var mainAgent = Agent.Main;

            foreach (var formationKey in FormationGroupKeys)
            {
                var kbKey = HotKeyManager.GetCategory("MissionOrderHotkeyCategory").GetGameKey(formationKey.Key).KeyboardKey;
                if (kbKey.InputKey.IsPressed())
                {
                    SelectedFormation = formationKey.Value;
                    break;
                }
            }

            var keyExists = VolleyActiveMap.TryGetValue(SelectedFormation, out var volleyEnabled);
            if (!keyExists)
            {
                VolleyActiveMap[SelectedFormation] = false;
            }

            var allFormations = Mission.Current.PlayerTeam.Formations;

            if (DefaultVolleyKey.IsPressed())
            {
                VolleyActiveMap[SelectedFormation] = !VolleyActiveMap[SelectedFormation];
                var enabledString = VolleyActiveMap[SelectedFormation] ? "enabled" : "disabled";
                InformationManager.DisplayMessage(new InformationMessage($"Volley { (ReleaseByRank ? "by ranks " : "") }{ enabledString } for { SelectedFormation }"));

                if (!VolleyActiveMap[SelectedFormation])
                {
                    var disabledFormations = Mission.Current.Agents
                        .Where(agent => IsAgentUnderPlayer(agent) && agent.Formation != null && agent.Formation.FormationIndex == SelectedFormation)
                        .Select(x => x.Formation);
                    foreach (var formation in disabledFormations)
                    {
                        formation.ApplyActionOnEachUnit(agent => SetUnitAttack(agent));
                    }
                    Yell(mainAgent, SkinVoiceManager.VoiceType.FireAtWill);
                }
                else
                {
                    var formation = allFormations.SingleOrDefault(x => x.FormationIndex == SelectedFormation);
                    Yell(mainAgent, SkinVoiceManager.VoiceType.Focus);
                }
            }

            var agents = Mission.Current.PlayerTeam.ActiveAgents
                .Where(agent => IsAgentInEnabledFormations(agent));
            
            foreach (var agent in agents)
            {
                FormationHasUnitsReloading[(agent.Formation.FormationIndex, GetAgentWeaponClass(agent))] = false;
                FormationByFormationClass[agent.Formation.FormationIndex] = agent.Formation;
            }

            foreach (var agent in agents)
            {
                if (agent == null || agent.Formation == null)
                {
                    continue;
                }
                if (!UnitCanShoot(agent))
                {
                    FormationHasUnitsReloading[(agent.Formation.FormationIndex, GetAgentWeaponClass(agent))] = true;
                    SetUnitAttack(agent);
                }
                else
                {
                    if (FormationHasUnitsReloading[(agent.Formation.FormationIndex, GetAgentWeaponClass(agent))] == true)
                    {
                        SetUnitNoAttack(agent);
                    }
                    else
                    {
                        SetUnitAttack(agent);
                    }
                }
            }

            foreach (var formationWeaponClassKey in FormationByFormationClass)
            {
                if (IsFormationUnderMeleeAttack(formationWeaponClassKey.Key))
                {
                    FormationByFormationClass[formationWeaponClassKey.Key].ApplyActionOnEachUnit((agent) => SetUnitAttack(agent));
                }
            }

        }

        void Yell(Agent agent, SkinVoiceManager.SkinVoiceType voiceType)
        {
            agent.MakeVoice(voiceType, SkinVoiceManager.CombatVoiceNetworkPredictionType.Prediction);
        }

        void SetUnitAttack(Agent agent)
        {
            agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
        }

        void SetUnitNoAttack(Agent agent)
        {
            agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanAttack);
        }

        bool UnitCanShoot(Agent agent)
        {
            return !agent.WieldedWeapon.IsReloading; // || agent.WieldedWeapon.ReloadPhase == agent.WieldedWeapon.ReloadPhaseCount;
        }

        WeaponClass? GetAgentWeaponClass(Agent agent)
        {
            return agent?.WieldedWeapon.CurrentUsageItem?.WeaponClass;
        }

        bool IsFormationUnderMeleeAttack(FormationClass formationClass)
        {
            var formation = FormationByFormationClass[formationClass];
            return formation != null && (formation.GetUnderAttackTypeOfUnits(1) & Agent.UnderAttackType.UnderMeleeAttack) == Agent.UnderAttackType.UnderMeleeAttack;
        }

        bool IsAgentInEnabledFormations(Agent agent)
        {
            if (!IsAgentUnderPlayer(agent) || agent.Formation == null)
            {
                return false;
            }
            var keyExists = VolleyActiveMap.TryGetValue(agent.Formation.FormationIndex, out var enabled);
            return keyExists && enabled;
        }

        bool IsAgentUnderPlayer(Agent agent)
        {
            try
            {
                return agent != null && agent.IsHuman && agent.Origin.IsUnderPlayersCommand;
            } catch 
            {
                return false;
            }
        }
    }
}
