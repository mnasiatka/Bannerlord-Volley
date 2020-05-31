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
        private static bool DEBUG = true;
        private static string MOD_NAME = "BannerlordVolley";
        private static string MOD_VERSION = "1.1.0";
        private static string MOD_ID = $"{MOD_NAME}:{MOD_VERSION}";
        private static Dictionary<FormationClass, bool> VolleyActiveMap = new Dictionary<FormationClass, bool>();
        private static FormationClass SelectedFormation;
        private static float NoShootTimeout = 3000f;

        private enum ReloadPhase
        {
            Loading = 0,
            Aiming = 1, // Only crossbomen seem to get this
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
                        .Where(agent => IsAgentUnderPlayer(agent) && agent.Formation.FormationIndex == SelectedFormation)
                        .Select(x => x.Formation);
                    foreach (var formation in disabledFormations)
                    {
                        formation.ApplyActionOnEachUnit(agent => agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack));
                    }

                    // TODO: add voice line for "Fire at will"
                }
            }

            // Get all formations in our army
            var formations = Mission.Current.Agents
                .Where(agent => IsAgentInEnabledFormations(agent))
                .Select(x => x.Formation).Distinct();

            foreach (var formation in formations)
            {
                // If a formation is in melee allow them to attack.
                if (IsFormationUnderMeleeAttack(formation))
                {
                    formation.ApplyActionOnEachUnit(agent => agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack));
                }
                else
                {
                    var unitIndices = formation.CollectUnitIndices();

                    foreach (var i in unitIndices)
                    {
                        var agent = formation.GetUnitWithIndex(i);
                        // Probably dead, not sure
                        if (agent == null)
                        {
                            continue;
                        }
                        
                        // Testing only 95% of units, since some seem to never be able to reload
                        // var otherAgentsInFormation = Mission.Current.Agents.Where(x => unitIndices.Contains(x.Index) && x.Index != i && WeaponClassesMatch(agent, x));
                        // var mostAgentsAreReloading = otherAgentsInFormation.Most(otherAgent => otherAgent.WieldedWeapon.ReloadPhase != (short)ReloadPhase.CanAttack, .95);
                        
                        // If a formation has any other units in reload phase, pause their attacking to sync volley waves
                        var agentWeaponClassIsReloading = formation.HasUnitsWithCondition(otherAgent => otherAgent.WieldedWeapon.ReloadPhase == (short) ReloadPhase.Loading && WeaponClassesMatch(agent, otherAgent));

                        // Pause this unit if he is ready to attack but his comrades are not
                        if ((agentWeaponClassIsReloading && agent.WieldedWeapon.ReloadPhase == (short)ReloadPhase.CanAttack)) //  || (agent.LastRangedAttackTime > agent.WieldedWeapon.CurrentUsageItem
                        {
                            // TODO: Don't pause this unit if he hasn't fired in a while (3x reload time. find sweet spot)
                            //  if (agent.LastRangedAttackTime > 60)
                            //  {
                            //      agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
                            //      continue;
                            //  }
                            agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanAttack);
                        }
                        else
                        {
                            agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
                        }
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

        bool IsFormationUnderMeleeAttack(Formation formation)
        {
            return formation == null || (formation.GetUnderAttackTypeOfUnits() & Agent.UnderAttackType.UnderMeleeAttack) == Agent.UnderAttackType.UnderMeleeAttack;
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
            return agent.IsHuman && agent.Origin.IsUnderPlayersCommand;
        }
    }
}

namespace CustomExtensions
{
    public static class EnumerableExtension
    {
        public static bool Most<T>(this IEnumerable<T> array, Func<T, bool> predicate, double percent)
        {
            var total = array.Count();
            var passing = array.Where(predicate).Count();

            var result = ((double)passing / total) >= percent;
            return result;
        }
    }
}