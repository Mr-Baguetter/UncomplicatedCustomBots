using System.Collections.Generic;
using UnityEngine;

namespace UncomplicatedCustomBots.API.YamlObjects
{
    public class NavBlocker
    {
        public string RoomName { get; set; } = string.Empty;
        public List<Vector3> LocalPos { get; set; } = [];
    }
}