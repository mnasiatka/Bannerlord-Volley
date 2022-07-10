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
        private Dictionary<FormationClass, int> CurrentRankByFormation = new Dictionary<FormationClass, int>();
        private Dictionary<(FormationClass, WeaponClass?), bool> FormationHasUnitsReloading = new Dictionary<(FormationClass, WeaponClass?), bool>();
        private Dictionary<FormationClass, Formation> FormationByFormationClass = new Dictionary<FormationClass, Formation>();

        private Dictionary<(FormationClass, int), List<int>> ExpectedShotsByFormationAndRank = new Dictionary<(FormationClass, int), List<int>>();
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

        //public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        //{
        //    var formation = shooterAgent.Formation;
        //    var formationIndex = formation.FormationIndex;
        //    var formationAndRankTuple = (formationIndex, (shooterAgent as IFormationUnit).FormationRankIndex);
        //    ExpectedShotsByFormationAndRank[formationAndRankTuple].Remove(shooterAgent.Index);

        //    if (FormationRankHasFired(formationIndex))
        //    {
        //        IncrementFormationRank(shooterAgent.Formation);
        //        SetNextFormationRankUnits(shooterAgent.Formation);
        //    }
        //}

        public override void OnMissionTick(float dt)
        {
            if (Agent.Main == null || CurrentRankByFormation == null)
            {
                return;
            }
            RunVolleyLogic();
        }

        void RunProtectFlankFormation()
        {

        }

        void RunReverseSkeinFormation() { }

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
                CurrentRankByFormation[SelectedFormation] = 0;
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
                    ResetFormationRank(SelectedFormation);
                    Yell(mainAgent, SkinVoiceManager.VoiceType.FireAtWill);
                }
                else
                {
                    ResetFormationRank(SelectedFormation);
                    var formation = allFormations.SingleOrDefault(x => x.FormationIndex == SelectedFormation);
                    //ExpectedShotsByFormationAndRank[(SelectedFormation, CurrentRankByFormation[SelectedFormation])] = formation.CollectUnitIndices().ToList();
                    Yell(mainAgent, SkinVoiceManager.VoiceType.HorseRally);
                }
            }

            var agents = Mission.Current.PlayerTeam.ActiveAgents
                .Where(agent => IsAgentInEnabledFormations(agent));
            //.OrderBy(agent => agent.AgentDrivenProperties.ReloadSpeed);

            //foreach (var formation in allFormations.Where(x => VolleyActiveMap.TryGetValue(x.FormationIndex, out var isActive) && isActive))
            //{
            //    var formationIndex = formation.FormationIndex;
            //    var rankHasUnitThatCanAttack = formation.HasUnitsWithCondition(agent =>
            //        UnitInCurrentRank(agent) &&
            //        UnitCanShoot(agent) &&
            //        AgentHasVisibleTarget(agent)
            //    );

            //    //if (FormationRankHasFired(formationIndex))
            //    if (!rankHasUnitThatCanAttack)
            //    {
            //        IncrementFormationRank(formation);
            //        //SetNextFormationRankUnits(formation);
            //        // ExpectedShotsByFormationAndRank[(formationIndex, CurrentRankByFormation[formationIndex])] = formation.CollectUnitIndices().ToList();
            //    }
            //}
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
                    //FormationHasUnitsReloading[(agent.Formation.FormationIndex, GetAgentWeaponClass(agent))] = false;


                    //var agentWeaponClassIsReloading = agent.Formation.HasUnitsWithCondition(otherAgent =>
                    //    UnitInCurrentRank(otherAgent) &&
                    //    WeaponClassesMatch(agent, otherAgent) &&
                    //    !UnitCanShoot(otherAgent)
                    //);

                    //if ((agentWeaponClassIsReloading && UnitCanShoot(agent)) || !UnitInCurrentRank(agent))
                    //{
                    //    SetUnitNoAttack(agent);
                    //}
                    //else
                    //{
                    //    SetUnitAttack(agent);
                    //}
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

        void ResetFormationRank(FormationClass formationIndex)
        {
            CurrentRankByFormation[formationIndex] = 0;
        }
        
        void IncrementFormationRank(Formation formation)
        {
            var maxRank = (formation.GetFirstUnit() as IFormationUnit).Formation.RankCount;
            CurrentRankByFormation[formation.FormationIndex]++;
            if (CurrentRankByFormation[formation.FormationIndex] >= maxRank)
            {
                ResetFormationRank(SelectedFormation);
            }
        }

        void SetNextFormationRankUnits(Formation formation)
        {
            ExpectedShotsByFormationAndRank[(formation.FormationIndex, CurrentRankByFormation[formation.FormationIndex])] = new List<int>();
            formation.ApplyActionOnEachUnit(agent => {
                if (AgentHasVisibleTarget(agent))
                {
                    ExpectedShotsByFormationAndRank[(formation.FormationIndex, CurrentRankByFormation[formation.FormationIndex])].Add(agent.Index);
                }
            });
        }

        bool FormationRankHasFired(FormationClass formationIndex)
        {
            return ExpectedShotsByFormationAndRank.TryGetValue((formationIndex, CurrentRankByFormation[formationIndex]), out var currentExpectedList) && currentExpectedList.IsEmpty();
        }

        bool AgentHasVisibleTarget(Agent agent)
        {
            var visibilityState = agent.GetLastTargetVisibilityState();
            return visibilityState == AITargetVisibilityState.TargetIsClear;
        }

        void SetUnitAttack(Agent agent)
        {
            agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanAttack);
        }

        void SetUnitNoAttack(Agent agent)
        {
            agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanAttack);
        }

        bool UnitInCurrentRank(Agent agent)
        {
            return ReleaseByRank ? (agent as IFormationUnit).FormationRankIndex == CurrentRankByFormation[agent.Formation.FormationIndex] : true;
        }

        bool UnitCanShoot(Agent agent)
        {
            return !agent.WieldedWeapon.IsReloading; // || agent.WieldedWeapon.ReloadPhase == agent.WieldedWeapon.ReloadPhaseCount;
        }

        WeaponClass? GetAgentWeaponClass(Agent agent)
        {
            return agent?.WieldedWeapon.CurrentUsageItem?.WeaponClass;
        }

        bool WeaponClassesMatch(Agent a1, Agent a2)
        {
            return a1 != null && a2 != null && GetAgentWeaponClass(a1) == GetAgentWeaponClass(a2);
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
            // agent.Origin.IsUnderPlayersCommand doesn't exist for NPCs in towns apparently
            try
            {
                return agent != null && agent.IsHuman && agent.Origin.IsUnderPlayersCommand;
            } catch (Exception e)
            {
                return false;
            }
        }
    }
}
