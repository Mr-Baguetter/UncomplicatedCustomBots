using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Features.Scenarios;

namespace UncomplicatedCustomBots.API.Features
{
    public abstract class Scenario
    {
        public static Scenario Create(RoleTypeId roleTypeId)
        {
            return roleTypeId switch
            {
                RoleTypeId.ClassD => new DClassScenario(),
                _ => null,
            };
        }
    }
}
