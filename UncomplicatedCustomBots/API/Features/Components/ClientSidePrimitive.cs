using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using System.Linq;
using UnityEngine;
using PrimitiveObjectToy = LabApi.Features.Wrappers.PrimitiveObjectToy;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class ClientSidePrimitive
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 scale { get; set; }
        public PrimitiveType primitiveType { get; set; }
        public Color color { get; set; }
        public PrimitiveFlags primitiveFlags { get; set; }
        public SpawnMessage spawnMessage { get; set; }
        public ObjectDestroyMessage destroyMessage { get; set; }
        public uint netId { get; set; }
        public PrimitiveObjectToy primitive { get; set; }

        public ClientSidePrimitive(PrimitiveObjectToy primitive)
        {
            this.position = primitive.Position;
            this.rotation = primitive.Rotation;
            this.scale = primitive.Scale;
            this.primitiveType = primitive.Type;
            this.color = primitive.Color;
            this.primitiveFlags = primitive.Flags;
            this.netId = NetworkIdentity.GetNextNetworkId();
            this.primitive = primitive;
            GenerateNetworkMessages();
        }

        private void GenerateNetworkMessages()
        {
            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.Write<byte>(1);
            writer.Write<byte>(67);
            writer.Write<Vector3>(position);
            writer.Write<Quaternion>(rotation);
            writer.Write<Vector3>(scale);
            writer.Write<byte>(0);
            writer.Write<bool>(false);
            writer.Write<int>((int)primitiveType);
            writer.Write<Color>(color);
            writer.Write<byte>((byte)(primitiveFlags));
            writer.Write<uint>(0);

            spawnMessage = new SpawnMessage()
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = primitive.GameObject.GetComponent<NetworkIdentity>().assetId,
                position = position,
                rotation = rotation,
                scale = scale,
                payload = writer.ToArraySegment()
            };

            destroyMessage = new ObjectDestroyMessage()
            {
                netId = netId,
            };

        }

        public void DestroyForEveryone()
        {
            foreach (Player player in Player.ReadyList.Where(p => p != null && !p.IsNpc && !p.IsDummy))
            {
                DestroyClientPrimitive(player);
            }
        }

        public void DestroyClientPrimitive(Player target)
        {
            if (target == null || target.IsHost) return;

            target.Connection?.Send(destroyMessage);
        }

        public void SpawnForEveryone()
        {
            foreach (Player player in Player.ReadyList.Where(p => p != null && !p.IsNpc && !p.IsDummy))
            {
                SpawnClientPrimitive(player);
            }
        }

        public void SpawnClientPrimitive(Player target)
        {
            if (target == null || target.IsHost) return;

            target.Connection?.Send(spawnMessage);
        }
    }
}
