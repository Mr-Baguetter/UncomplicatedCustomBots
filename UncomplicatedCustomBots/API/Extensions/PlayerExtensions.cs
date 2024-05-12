using Exiled.API.Features;
using Exiled.API.Features.Roles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomBots.API.Enums;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class PlayerExtensions
    {
        public static void Move(this Player player, DirectionType directionType)
        {
            if (directionType is DirectionType.None)
            {
                return;
            }

            if (player.RoleManager.CurrentRole is FpcStandardRoleBase fpcStandardRoleBase)
            {
                var animatedCharacterModel = fpcStandardRoleBase.FpcModule.CharacterModelInstance as AnimatedCharacterModel;

                animatedCharacterModel.PlayFootstep();
                animatedCharacterModel.PlayFootstepAudioClip(animatedCharacterModel._footstepClips[0], 1, 1);
            }
        }
    }
}
