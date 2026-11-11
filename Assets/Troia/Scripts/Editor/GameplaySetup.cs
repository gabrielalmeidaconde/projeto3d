using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Troia.Editor
{
    public static class GameplaySetup
    {
        const string Root = "Assets/Troia/Resources/Troia";
        [MenuItem("Troia/Gameplay/Criar configuracao e combos")]
        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameplayConfig>(Root + "/Gameplay.asset")) return;
            Directory.CreateDirectory(Root + "/Combos"); Directory.CreateDirectory(Root + "/Andares"); Directory.CreateDirectory(Root + "/Bencaos");
            AssetDatabase.Refresh();
            var config = GameplayDefaults.Create();
            var visited = new HashSet<ComboSO>();
            foreach (var combo in new[] { config.light, config.heavy, config.launcher, config.airLight, config.slam }) SaveCombo(combo, visited);
            foreach (var floor in config.floors) AssetDatabase.CreateAsset(floor, Root + "/Andares/" + floor.name + ".asset");
            foreach (var blessing in config.blessings) AssetDatabase.CreateAsset(blessing, Root + "/Bencaos/" + blessing.name + ".asset");
            AssetDatabase.CreateAsset(config, Root + "/Gameplay.asset"); AssetDatabase.SaveAssets();
            Debug.Log("Gameplay: configuracao, 11 golpes, 20 andares e 6 bencaos criados.");
        }
        static void SaveCombo(ComboSO combo, HashSet<ComboSO> visited)
        {
            if (!combo || !visited.Add(combo)) return;
            AssetDatabase.CreateAsset(combo, Root + "/Combos/" + combo.name + ".asset");
            SaveCombo(combo.next, visited); SaveCombo(combo.delayedNext, visited);
            EditorUtility.SetDirty(combo);
        }
        public static void BuildWindows()
        {
            EnsureAssets();
            Directory.CreateDirectory("Builds/Windows");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity" },
                locationPathName = "Builds/Windows/Iliada.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded) throw new System.Exception("Build falhou: " + result.summary.result);
        }
    }
}
