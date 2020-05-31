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
        private static string MOD_VERSION = "1.0.0";
        private static string MOD_ID = $"{MOD_NAME}:{MOD_VERSION}";
        private static Dictionary<FormationClass, bool> VolleyActiveMap = new Dictionary<FormationClass, bool>();
        private static FormationClass SelectedFormation;
        
        private enum ReloadPhase
        {
            Reloading = 0,
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
            if (Mission.Current == null || Mission.Current.CombatType != Mission.MissionCombatType.Combat || mainAgent == null)
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
                }
            }

            // Get all agents and formations in our army
            var selectedUnits = Mission.Current.Agents
                .Where(agent => IsAgentInEnabledFormations(agent));
            var formations = selectedUnits
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
                    // If a formation has any units in reload phase, pause their attacking to sync volley waves
                    var hasUnitsReloading = formation.HasUnitsWithCondition(agent => agent.WieldedWeapon.ReloadPhase == (short)ReloadPhase.Reloading);
                    if (hasUnitsReloading)
                    {
                        formation.ApplyActionOnEachUnit(agent => {
                            // But allow those who need to reload to do so (only needed for crossbows, as they have a reload time whereas bows do not)
                            if (agent.WieldedWeapon.ReloadPhase != (short)ReloadPhase.Reloading) {
                                agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanAttack);
                            }
                            else
                            {
                                agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
                            }
                        });
                        
                    }
                    else
                    {
                        formation.ApplyActionOnEachUnit(agent => agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack));
                    }
                }
            }
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


        //bool AnyWithJitter<T>(IEnumerable<T> array, Func<T, bool> predicate, double percent)
        //{
        //    var total = array.Count();
        //    var passing = array.Where(predicate).Count();

        //    return ((double) passing / total) > percent;
        //}
    }

}