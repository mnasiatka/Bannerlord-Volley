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
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            MessageManager.DisplayMessage("Toggle Volleys with \"U\" for each formation");
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            if (mission.CombatType == Mission.MissionCombatType.Combat)//&& mission.IsFieldBattle)
            {
                mission.AddMissionBehavior(new VolleyMissionBehavior());
            }
        }
    }
}
 