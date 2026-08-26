using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Yaml.CustomConverters;
using Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace UncomplicatedCustomBots.API.Managers
{
    public class YamlLoader
    {
        public static IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .IgnoreFields()
        .WithTypeConverter(new CustomVectorConverter())
        .WithTypeConverter(new CustomColor32Converter())
        .WithTypeConverter(new CustomColorConverter())
        .WithTypeConverter(new CustomQuaternionConverter())
        .Build();

        public static ISerializer Serializer = new SerializerBuilder()
        .WithEmissionPhaseObjectGraphVisitor(visitor => new CommentsObjectGraphVisitor(visitor.InnerVisitor))
        .WithTypeInspector(typeInspector => new CommentGatheringTypeInspector(typeInspector))
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .DisableAliases()
        .IgnoreFields()
        .WithTypeConverter(new CustomVectorConverter())
        .WithTypeConverter(new CustomColor32Converter())
        .WithTypeConverter(new CustomColorConverter())
        .WithTypeConverter(new CustomQuaternionConverter())
        .Build();

        public static string Dir() => Path.Combine(PathManager.Configs.ToString(), "UncomplicatedCustomBots");
        public static string Dir(string[] path) => Path.Combine([Dir(), .. path]);

        public static void TryCreateDirectory(string name) => Directory.CreateDirectory(Dir([name]));
        public static void TryCreateDirectory(string[] path) => Directory.CreateDirectory(Dir(path));

        public static string[] GetFilesInDirectory(string name, string filter = "*") => Directory.GetFiles(Dir([name]), filter);

        public static void LoadEmbeddedAsset<T>(string name, Action<T> onParsed)
        {
            string resourceName = Plugin.Instance.Assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("Resource not found", name);
            Stream raw = Plugin.Instance.Assembly.GetManifestResourceStream(resourceName)!;

            using StreamReader reader = new(raw);
            T result = Deserializer.Deserialize<T>(reader);

            onParsed(result);
        }

        public static void ParseYamlFiles<T>(string name, Action<T> onParsed)
        {
            TryCreateDirectory(name);
            int count = 0;

            foreach (string file in GetFilesInDirectory(name, "*.yml"))
            {
                try
                {
                    string text = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        LogManager.Warn($"Skipping empty yaml file {Path.GetFileName(file)}");
                        continue;
                    }

                    T result = Deserializer.Deserialize<T>(text);
                    if (result == null)
                    {
                        LogManager.Warn($"Yaml file {Path.GetFileName(file)} deserialized to null, skipping");
                        continue;
                    }

                    onParsed(result);
                    count++;
                    LogManager.Debug($"Loaded {typeof(T).Name} from {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    LogManager.Error($"Failed to parse {Path.GetFileName(file)} as {typeof(T).Name}: {ex.Message}");
                }
            }

            LogManager.Debug($"Loaded {count} {typeof(T).Name}(s) from {name}");
        }

        public static void CreateDefaultFile<T>(string name, string fileName) where T : new()
        {
            if (Directory.Exists(Dir([name])))
                return;

            TryCreateDirectory(name);
            File.WriteAllText(Path.Combine(PathManager.Configs.ToString(), "UncomplicatedCustomBots", name, fileName), Serializer.Serialize(new T()));
        }

        public static void CreateDefaultFiles<T>(string name, List<(string fileName, T item)> items) where T : new()
        {
            if (Directory.Exists(Dir([name])))
                return;

            TryCreateDirectory(name);
            foreach ((string fileName, T item) in items)
            {
                if (item != null)
                    File.WriteAllText(Path.Combine(PathManager.Configs.ToString(), "UncomplicatedCustomBots", name, fileName), Serializer.Serialize(item));
            }
        }
    }
}