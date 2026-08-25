using System;
using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using UnityEngine;
using PrimitiveObjectToy = LabApi.Features.Wrappers.PrimitiveObjectToy;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class ClientSidePrimitive
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public PrimitiveType PrimitiveType { get; set; }
        public Color Color { get; set; }
        public PrimitiveFlags PrimitiveFlags { get; set; }
        public SpawnMessage SpawnMessage { get; set; }
        public ObjectDestroyMessage DestroyMessage { get; set; }
        public uint NetId { get; set; }
        public PrimitiveObjectToy Primitive { get; set; }

        public ClientSidePrimitive(PrimitiveObjectToy primitive)
        {
            Position = primitive.Position;
            Rotation = primitive.Rotation;
            Scale = primitive.Scale;
            PrimitiveType = primitive.Type;
            Color = primitive.Color;
            PrimitiveFlags = primitive.Flags;
            NetId = NetworkIdentity.GetNextNetworkId();
            Primitive = primitive;
            GenerateNetworkMessages();
        }

        private void GenerateNetworkMessages()
        {
            NetworkWriterPooled writer = NetworkWriterPool.Get();
            try
            {
                writer.Write<byte>(1);
                writer.Write<byte>(67);
                writer.Write(Position);
                writer.Write(Rotation);
                writer.Write(Scale);
                writer.Write<byte>(0);
                writer.Write(false);
                writer.Write((int)PrimitiveType);
                writer.Write(Color);
                writer.Write((byte)PrimitiveFlags);
                writer.Write<uint>(0);

                ArraySegment<byte> segment = writer.ToArraySegment();
                byte[] payloadCopy = new byte[segment.Count];
                Buffer.BlockCopy(segment.Array!, segment.Offset, payloadCopy, 0, segment.Count);
                ArraySegment<byte> copiedSegment = new(payloadCopy);

                SpawnMessage = new SpawnMessage()
                {
                    netId = NetId,
                    isLocalPlayer = false,
                    isOwner = false,
                    sceneId = 0,
                    assetId = Primitive.GameObject.GetComponent<NetworkIdentity>().assetId,
                    position = Position,
                    rotation = Rotation,
                    scale = Scale,
                    payload = copiedSegment
                };

                DestroyMessage = new ObjectDestroyMessage()
                {
                    netId = NetId,
                };
            }
            finally
            {
                NetworkWriterPool.Return(writer);
            }
        }

        public void DestroyForEveryone()
        {
            foreach (Player player in Player.ReadyList)
            {
                if (player == null || player.IsNpc || player.IsDummy)
                    continue;

                DestroyClientPrimitive(player);
            }
        }

        public void DestroyClientPrimitive(Player target)
        {
            if (target == null || target.IsHost)
                return;

            target.Connection?.Send(DestroyMessage);
        }

        public void SpawnForEveryone()
        {
            foreach (Player player in Player.ReadyList)
            {
                if (player == null || player.IsNpc || player.IsDummy)
                    continue;
                    
                SpawnClientPrimitive(player);
            }
        }

        public void SpawnClientPrimitive(Player target)
        {
            if (target == null || target.IsHost)
                return;

            target.Connection?.Send(SpawnMessage);
        }
    }
}
