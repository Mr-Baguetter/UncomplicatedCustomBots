using System.Collections.Generic;
using System.Linq;
using MapGeneration;

namespace UncomplicatedCustomBots.API.Features
{
    public class Objective
    {
        public static Dictionary<uint, Objective> Objectives { get; set; } = [];

        public static ICollection<Objective> ActiveObjectives => Objectives.Values;

        public Bot Bot { get; set; }
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Completed { get; set; }
        public ItemType ItemToGet { get; set; }
        public RoomName RoomToFind { get; set; }
        public List<RoomName> RoomsOfInterest { get; set; } = [];
        public List<ItemType> EitherItem { get; set; } = [];

        public static uint GetFirstFreeId(uint from = 1)
        {
            for (uint i = from; i < uint.MaxValue; i++)
            {
                if (!Objectives.ContainsKey(i))
                    return i;
            }

            return 0;
        }

        public void EndObjective(uint id)
        {
            Objectives.Remove(id);
        }

        public Objective(Bot bot, uint id, string name, string description, List<ItemType> eitheritemToGet)
        {
            Id = GetAvailableId(id);
            Bot = bot;
            Name = name;
            Description = description;
            EitherItem = eitheritemToGet;
            Objectives.Add(Id, this);
        }

        public Objective(Bot bot, uint id, string name, string description, List<ItemType> eitheritemToGet, List<RoomName> roomsOfinterest)
        {
            Id = GetAvailableId(id);
            Bot = bot;
            Name = name;
            Description = description;
            EitherItem = eitheritemToGet;
            RoomsOfInterest = roomsOfinterest;
            Objectives.Add(Id, this);
        }

        public Objective(Bot bot, uint id, string name, string description, ItemType itemToGet)
        {
            Id = GetAvailableId(id);
            Bot = bot;
            Name = name;
            Description = description;
            ItemToGet = itemToGet;
            Objectives.Add(Id, this);
        }

        public Objective(Bot bot, uint id, string name, string description, ItemType itemToGet, List<RoomName> roomsOfInterest)
        {
            Id = GetAvailableId(id);
            Bot = bot;
            Name = name;
            Description = description;
            ItemToGet = itemToGet;
            RoomsOfInterest = roomsOfInterest;
            Objectives.Add(Id, this);
        }

        public Objective(Bot bot, uint id, string name, string description, RoomName roomToFind)
        {
            Id = GetAvailableId(id);
            Bot = bot;
            Name = name;
            Description = description;
            RoomToFind = roomToFind;
            Objectives.Add(Id, this);
        }

        private static uint GetAvailableId(uint requested)
        {
            if (!Objectives.ContainsKey(requested))
                return requested;

            uint freeId = GetFirstFreeId();
            return freeId == 0 ? requested : freeId;
        }
    }
}