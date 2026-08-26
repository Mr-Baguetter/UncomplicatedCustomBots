using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Steam;
using UncomplicatedCustomBots.API.Struct;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Managers
{
    public static class RuntimeMeshReader
    {
        private readonly record struct MeshEntry(string Key, Vector3[] Positions, int[] Indices);
        private static readonly Dictionary<string, DecodedMeshData> DecodedRawCache = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, List<MeshEntry>> Index => field ??= LoadIndex();

        private static Dictionary<string, List<MeshEntry>> LoadIndex()
        {
            Stopwatch sw = Stopwatch.StartNew();
            Dictionary<string, List<MeshEntry>> index = new(StringComparer.OrdinalIgnoreCase);

            if (SteamServerInfo.Version != "14.2.7")
                LogManager.Warn($"The version {SteamServerInfo.Version} dosent match the compiled version (14.2.7). Some meshes may be missing.");

            try
            {
                using Stream? stream = OpenBinStream();
                if (stream == null)
                {
                    LogManager.Info("RuntimeMeshReader: no ExportedMeshes found. Mesh loading disabled.");
                    return index;
                }

                using BinaryReader reader = new(stream);

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = ReadPrefixedString(reader);
                    string key = ReadPrefixedString(reader);

                    int vertCount = reader.ReadInt32();
                    Vector3[] positions = new Vector3[vertCount];
                    for (int v = 0; v < vertCount; v++)
                        positions[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    int idxCount = reader.ReadInt32();
                    int[] indices = new int[idxCount];
                    for (int idx = 0; idx < idxCount; idx++)
                        indices[idx] = reader.ReadInt32();

                    if (!index.TryGetValue(name, out List<MeshEntry> list))
                        index[name] = list = [];

                    list.Add(new MeshEntry(key, positions, indices));
                }
            }
            catch (Exception ex)
            {
                LogManager.Warn($"RuntimeMeshReader: failed to load ExportedMeshes: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return index;
            }

            int totalMeshes = 0;
            foreach (List<MeshEntry> list in index.Values)
            {
                totalMeshes += list.Count;
            }

            LogManager.Info($"RuntimeMeshReader: loaded {totalMeshes} mesh entries ({index.Count} distinct names) in {sw.ElapsedMilliseconds}ms.");
            return index;
        }

        private static Stream? OpenBinStream()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("ExportedMeshes.bin", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                return null;

            Stream raw = assembly.GetManifestResourceStream(resourceName)!;
            return new GZipStream(raw, CompressionMode.Decompress);
        }

        private static string ReadPrefixedString(BinaryReader reader) => Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));

        private static bool TryGetMesh(string meshName, int expectedVertexCount, out Vector3[] positions, out int[] indices)
        {
            positions = null!;
            indices = null!;

            if (!Index.TryGetValue(meshName, out List<MeshEntry> candidates) || candidates.Count == 0)
                return false;

            MeshEntry chosen = candidates[0];
            if (candidates.Count > 1)
            {
                int bestDelta = int.MaxValue;
                foreach (MeshEntry candidate in candidates)
                {
                    int delta = Math.Abs(candidate.Positions.Length - expectedVertexCount);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        chosen = candidate;
                    }
                }

                if (bestDelta != 0)
                    LogManager.Warn($"RuntimeMeshReader: {candidates.Count} meshes named '{meshName}' found, best vertex-count match is off by {bestDelta} - result may be wrong.");
            }

            positions = chosen.Positions;
            indices = chosen.Indices;
            return true;
        }

        public static bool TryDecodeMeshRaw(string meshName, int expectedVertexCount, out DecodedMeshData data)
        {
            string cacheKey = $"{meshName}#{expectedVertexCount}";
            if (DecodedRawCache.TryGetValue(cacheKey, out data))
                return data.Positions != null;

            if (TryGetMesh(meshName, expectedVertexCount, out Vector3[] positions, out int[] indices))
            {
                bool use32Bit = false;
                if (indices.Length > 0)
                {
                    int maxIdx = 0;
                    for (int i = 0; i < indices.Length; i++)
                    {
                        if (indices[i] > maxIdx)
                            maxIdx = indices[i];
                    }

                    use32Bit = maxIdx > 65535 || positions.Length > 65535;
                }
                
                data = new DecodedMeshData(positions, indices, use32Bit);
                DecodedRawCache[cacheKey] = data;
                return true;
            }

            LogManager.Warn($"RuntimeMeshReader: '{meshName}' not found in ExportedMeshes.");
            DecodedRawCache[cacheKey] = default;
            data = default;
            return false;
        }
    }
}