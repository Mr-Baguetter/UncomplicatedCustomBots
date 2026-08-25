using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Interactables.Interobjects;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public static class ElevatorLinkRegistry
    {
        private static readonly List<NavMeshLinkInstance> _linkInstances = [];
        private static readonly List<GameObject> _linkOwners = [];
        private static bool _built = false;

        public static void Build()
        {
            if (_built)
                Clear();

            foreach (ElevatorChamber? chamber in ElevatorChamber.AllChambers)
            {
                if (chamber == null || chamber.FloorDoors == null || chamber.FloorDoors.Count < 2)
                    continue;

                ElevatorDoor[] doors = chamber.FloorDoors.ToArray();
                ElevatorDoor start = doors[0];
                ElevatorDoor end = doors[1];
                if (start == null || end == null)
                    continue;

                CreateLink(chamber, start.transform.position, end.transform.position);

                if (doors.Length > 2)
                {
                    ElevatorDoor end2 = doors[2];
                    if (end2 != null)
                        CreateLink(chamber, end.transform.position, end2.transform.position);
                }
            }
            _built = true;
        }

        private static void CreateLink(ElevatorChamber chamber, Vector3 startPos, Vector3 endPos)
        {
            startPos += Vector3.up * 0.1f;
            endPos += Vector3.up * 0.1f;

            NavMeshLinkData data = new()
            {
                startPosition = startPos,
                endPosition = endPos,
                costModifier = 100f,
                bidirectional = true,
                width = 2.2f,
                area = 0,
                agentTypeID = NavMeshManager.AgentTypeId
            };

            GameObject owner = new($"ElevLink_{chamber.AssignedGroup}_{_linkInstances.Count}");
            owner.transform.position = (startPos + endPos) * 0.5f;
            NavMeshLinkInstance inst = NavMesh.AddLink(data);
            NavMesh.SetLinkOwner(inst, owner);
            _linkInstances.Add(inst);
            _linkOwners.Add(owner);
        }

        public static void Clear()
        {
            foreach (NavMeshLinkInstance inst in _linkInstances)
            {
                if (NavMesh.IsLinkValid(inst))
                    NavMesh.RemoveLink(inst);
            }
            
            _linkInstances.Clear();

            foreach (GameObject go in _linkOwners)
            {
                if (go != null)
                    Object.Destroy(go);
            }

            _linkOwners.Clear();
            _built = false;
        }

        public static bool IsElevatorLink(NavMeshLinkInstance link) => _linkInstances.Contains(link);

        public static int LinkCount => _linkInstances.Count;
    }
}
