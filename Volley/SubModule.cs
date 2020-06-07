using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace Volley
{
    public class Volley : MBSubModuleBase
    {
        private static Dictionary<FormationClass, bool> VolleyActiveMap = new Dictionary<FormationClass, bool>();
        private static FormationClass SelectedFormation;

        private enum ReloadPhase
        {
            Loading = 0,
            Aiming = 1, // Only crossbowmen seem to get this
            CanAttack = 2
        };

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            InformationManager.DisplayMessage(new InformationMessage("Toggle Volleys with \"U\" for each formation"));
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            var mainAgent = Agent.Main;
            if (Mission.Current == null || Mission.Current.CombatType != Mission.MissionCombatType.Combat || Mission.Current.IsFriendlyMission || mainAgent == null)
            {
                if (VolleyActiveMap.Keys.Count() > 0)
                {
                    VolleyActiveMap.Clear();
                }
                return;
            }

            // TODO: listen/subscribe to formation change handler
            if (InputKey.D1.IsPressed())
            {
                SelectedFormation = FormationClass.Infantry;
            }
            else if (InputKey.D2.IsPressed())
            {
                SelectedFormation = FormationClass.Ranged;
            }
            else if (InputKey.D3.IsPressed())
            {
                SelectedFormation = FormationClass.Cavalry;
            }
            else if (InputKey.D4.IsPressed())
            {
                SelectedFormation = FormationClass.HorseArcher;
            }
            else if (InputKey.D5.IsPressed())
            {
                SelectedFormation = FormationClass.Skirmisher;
            }
            else if (InputKey.D6.IsPressed())
            {
                SelectedFormation = FormationClass.HeavyInfantry;
            }
            else if (InputKey.D7.IsPressed())
            {
                SelectedFormation = FormationClass.LightCavalry;
            }
            else if (InputKey.D8.IsPressed())
            {
                SelectedFormation = FormationClass.HeavyCavalry;
            }
            else if (InputKey.D9.IsPressed())
            {
                SelectedFormation = FormationClass.General;
            }
            else if (InputKey.D0.IsPressed())
            {
                SelectedFormation = FormationClass.Bodyguard;
            }

            var keyExists = VolleyActiveMap.TryGetValue(SelectedFormation, out var volleyEnabled);
            if (!keyExists)
            {
                VolleyActiveMap[SelectedFormation] = false;
            }

            // Volley key pressed
            if (InputKey.U.IsPressed())
            {
                // Toggle volley for formation
                VolleyActiveMap[SelectedFormation] = !VolleyActiveMap[SelectedFormation];
                // Output status
                var enabledString = VolleyActiveMap[SelectedFormation] ? "enabled" : "disabled";
                InformationManager.DisplayMessage(new InformationMessage($"Volley { enabledString } for { SelectedFormation }"));

                // When disabled, make sure that formation can continue/resume attack
                if (!VolleyActiveMap[SelectedFormation])
                {
                    var disabledFormations = Mission.Current.Agents
                        .Where(agent => IsAgentUnderPlayer(agent) && agent.Formation != null && agent.Formation.FormationIndex == SelectedFormation)
                        .Select(x => x.Formation);
                    foreach (var formation in disabledFormations)
                    {
                        formation.ApplyActionOnEachUnit(agent => agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack));
                    }
                    mainAgent.MakeVoice(new SkinVoiceManager.SkinVoiceType("FireAtWill"), SkinVoiceManager.CombatVoiceNetworkPredictionType.Prediction);
                }
            }

            var agents = Mission.Current.Agents
                .Where(agent => IsAgentInEnabledFormations(agent));

            foreach (var agent in agents)
            {
                if (agent == null || agent.Formation == null)
                {
                    continue;
                }
                if (IsUnitFormationUnderMeleeAttack(agent))
                {
                    agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
                }
                else if (agent.WieldedWeapon.ReloadPhase != (short)ReloadPhase.CanAttack)
                {
                    agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
                }
                else
                {
                    var agentWeaponClassIsReloading = agent.Formation.HasUnitsWithCondition(otherAgent => otherAgent.WieldedWeapon.ReloadPhase != (short)ReloadPhase.CanAttack && WeaponClassesMatch(agent, otherAgent));
                    if (agentWeaponClassIsReloading)
                    {
                        agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanAttack);
                    }
                    else
                    {
                        agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
                    }
                }
            }
        }

        WeaponClass? GetAgentWeaponClass(Agent agent)
        {
            return agent?.WieldedWeapon.CurrentUsageItem?.WeaponClass;
        }

        bool WeaponClassesMatch(Agent a1, Agent a2)
        {
            return a1 != null && a2 != null && GetAgentWeaponClass(a1) == GetAgentWeaponClass(a2);
        }

        bool IsUnitFormationUnderMeleeAttack(Agent agent)
        {
            return agent != null && agent.Formation != null && (agent.Formation.GetUnderAttackTypeOfUnits() & Agent.UnderAttackType.UnderMeleeAttack) == Agent.UnderAttackType.UnderMeleeAttack;
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
            return agent != null && agent.IsHuman && agent.Origin.IsUnderPlayersCommand;
        }
    }
}
