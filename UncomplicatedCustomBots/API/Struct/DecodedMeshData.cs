using UnityEngine;

namespace UncomplicatedCustomBots.API.Struct
{
    public struct DecodedMeshData
    {
        public readonly Vector3[] Positions;
        public readonly int[] Indices;
        public readonly bool Use32BitIndices;

        public DecodedMeshData(Vector3[] positions, int[] indices, bool use32BitIndices)
        {
            Positions = positions;
            Indices = indices;
            Use32BitIndices = use32BitIndices;
        }
    }
}