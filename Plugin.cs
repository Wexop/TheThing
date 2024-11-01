using System.Collections.Generic;
using BepInEx;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using TheThing.Utils;
using TheThing.Scripts;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using UnityEngine;
using LethalLib.Modules;

namespace TheThing
{
    [BepInDependency(StaticNetcodeLib.StaticNetcodeLib.Guid)]
    [BepInDependency("evaisa.lethallib", "0.15.1")]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class TheThingPlugin : BaseUnityPlugin
    {

        const string GUID = "wexop.the_thing";
        const string NAME = "TheThing";
        const string VERSION = "1.0.3";

        public GameObject roomObject;
        public GameObject actualRoomObjectInstantiated;
        public ThingRoomManager actualRoomObjectManager;

        public Dictionary<int, LightInformation> lightsInUse = new Dictionary<int, LightInformation>();

        public static TheThingPlugin instance;

        public ConfigEntry<string> spawnMoonRarity;
        
        public ConfigEntry<int> maxSeePlayerCount;
        public ConfigEntry<float> timeBetweenTeleport;
        public ConfigEntry<float> minDistanceBetweenPlayerToTeleport;
        public ConfigEntry<float> TimeToEscapeRoom;
        public ConfigEntry<int> monsterToHitToEscapeRoom;
        public ConfigEntry<float> enemyPower;

        void Awake()
        {
            instance = this;
            
            Logger.LogInfo($"TheThing starting....");

            string assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "thing");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);
            
            Logger.LogInfo($"TheThing bundle found !");
            
            LoadConfigs();
            RegisterMonster(bundle);
            LoadRoom(bundle);
            
            
            Logger.LogInfo($"TheThing is ready!");
        }

        void LoadConfigs()
        {
            
            //GENERAL
            spawnMoonRarity = Config.Bind("General", "SpawnRarity", 
                "Modded:40,ExperimentationLevel:20,AssuranceLevel:20,VowLevel:20,OffenseLevel:25,MarchLevel:25,RendLevel:30,DineLevel:30,TitanLevel:40,Adamance:35,Embrion:40,Artifice:45", 
                "Chance for thing to spawn for any moon, example => assurance:100,offense:50 . You need to restart the game.");
            CreateStringConfig(spawnMoonRarity, true);
            enemyPower = Config.Bind("General", "EnemyPower", 
                    2f,
                "Enemy power for Thing monster. You need to restart the game.");
            CreateFloatConfig(enemyPower,0f, 10f );
            
            //BEHAVIOR
            maxSeePlayerCount = Config.Bind("Behavior", "MaxSeePlayerCount", 4,
                "Max player see by the monster before teleporting him into his room. No need to restart the game !");
            CreateIntConfig(maxSeePlayerCount);
            
            timeBetweenTeleport = Config.Bind("Behavior", "timeBetweenTeleport", 20f,
                "Time to wait before teleport to another place after the thing see a player. No need to restart the game !");
            CreateFloatConfig(timeBetweenTeleport);
            
            minDistanceBetweenPlayerToTeleport = Config.Bind("Behavior", "minDistanceBetweenPlayerToTeleport", 45f,
                "Minimum distance to have with any player to teleport somewhere to wait. No need to restart the game !");
            CreateFloatConfig(minDistanceBetweenPlayerToTeleport);
            
            TimeToEscapeRoom = Config.Bind("Behavior", "TimeToEscapeRoom", 90f,
                "Time to escape the room. No need to restart the game !");
            CreateFloatConfig(TimeToEscapeRoom, 0f, 300f);
            
            monsterToHitToEscapeRoom = Config.Bind("Behavior", "MonsterToHitToEscapeRoom", 2,
                "Monsters to hit to escape the room. No need to restart the game !");
            CreateIntConfig(monsterToHitToEscapeRoom);
            
        }
        
        

        void RegisterMonster(AssetBundle bundle)
        {
            //creature
            EnemyType creature = bundle.LoadAsset<EnemyType>("Assets/LethalCompany/Mods/ThingMonster/Thing.asset");
            
            Logger.LogInfo($"{creature.name} FOUND");
            Logger.LogInfo($"{creature.enemyPrefab} prefab");
            NetworkPrefabs.RegisterNetworkPrefab(creature.enemyPrefab);
            Utilities.FixMixerGroups(creature.enemyPrefab);

            TerminalNode terminalNodeBigEyes = new TerminalNode();
            terminalNodeBigEyes.creatureName = "Thing";
            terminalNodeBigEyes.displayText = "";

            TerminalKeyword terminalKeywordBigEyes = new TerminalKeyword();
            terminalKeywordBigEyes.word = "Thing";
            
            
            RegisterUtil.RegisterEnemyWithConfig(spawnMoonRarity.Value, creature,terminalNodeBigEyes , terminalKeywordBigEyes, instance.enemyPower.Value, creature.MaxCount);

        }

        void LoadRoom(AssetBundle bundle)
        {
            GameObject room = bundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/ThingMonster/ThingRoom.prefab");
            roomObject = room;
            Logger.LogInfo($"{room.name} FOUND");
            
            Utilities.FixMixerGroups(room);
        }

        public void InstantiateRoom()
        {
            if (actualRoomObjectInstantiated != null)
            {
                Destroy(actualRoomObjectInstantiated);
                Destroy(actualRoomObjectManager);
                actualRoomObjectManager = null;
                actualRoomObjectInstantiated = null;
            }
            actualRoomObjectInstantiated = Instantiate(roomObject, Vector3.up * -500, Quaternion.identity);
            actualRoomObjectManager = actualRoomObjectInstantiated.GetComponent<ThingRoomManager>();
        }
        
        private void CreateFloatConfig(ConfigEntry<float> configEntry, float min = 0f, float max = 100f)
        {
            var exampleSlider = new FloatSliderConfigItem(configEntry, new FloatSliderOptions
            {
                Min = min,
                Max = max,
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        private void CreateIntConfig(ConfigEntry<int> configEntry, int min = 0, int max = 100)
        {
            var exampleSlider = new IntSliderConfigItem(configEntry, new IntSliderOptions()
            {
                Min = min,
                Max = max,
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        private void CreateStringConfig(ConfigEntry<string> configEntry, bool requireRestart = false)
        {
            var exampleSlider = new TextInputFieldConfigItem(configEntry, new TextInputFieldOptions()
            {
                RequiresRestart = requireRestart
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }


    }
}