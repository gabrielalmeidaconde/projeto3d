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
            var existing = AssetDatabase.LoadAssetAtPath<GameplayConfig>(Root + "/Gameplay.asset");
            if (existing)
            {
                if (existing.combatVersion < ArsenalDefaults.Version || existing.weapons == null || existing.weapons.Length == 0)
                {
                    if (existing.combatVersion == 3 && existing.weapons != null && existing.weapons.Length == 3)
                        CombatFlowTuning.Apply(existing);
                    else ArsenalDefaults.Populate(existing);
                    SaveArsenal(existing); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets();
                }
                return;
            }
            Directory.CreateDirectory(Root + "/Combos"); Directory.CreateDirectory(Root + "/Andares"); Directory.CreateDirectory(Root + "/Bencaos");
            AssetDatabase.Refresh();
            var config = GameplayDefaults.Create();
            var visited = new HashSet<ComboSO>();
            foreach (var combo in new[] { config.light, config.heavy, config.launcher, config.airLight, config.slam }) SaveCombo(combo, visited);
            SaveArsenal(config);
            foreach (var floor in config.floors) AssetDatabase.CreateAsset(floor, Root + "/Andares/" + floor.name + ".asset");
            foreach (var blessing in config.blessings) AssetDatabase.CreateAsset(blessing, Root + "/Bencaos/" + blessing.name + ".asset");
            AssetDatabase.CreateAsset(config, Root + "/Gameplay.asset"); AssetDatabase.SaveAssets();
            Debug.Log("Gameplay: arsenal, combos, 20 andares e 6 bencaos criados.");
        }
        static void SaveCombo(ComboSO combo, HashSet<ComboSO> visited)
        {
            if (!combo || !visited.Add(combo)) return;
            if (!AssetDatabase.Contains(combo)) AssetDatabase.CreateAsset(combo, Root + "/Combos/" + combo.name + ".asset");
            SaveCombo(combo.next, visited); SaveCombo(combo.delayedNext, visited); SaveCombo(combo.heavyNext, visited);
            EditorUtility.SetDirty(combo);
        }
        static void SaveArsenal(GameplayConfig config)
        {
            Directory.CreateDirectory(Root + "/Armas"); AssetDatabase.Refresh();
            var saved = new Dictionary<ComboSO, ComboSO>();
            for (int i = 0; i < config.weapons.Length; i++)
            {
                var weapon = config.weapons[i];
                weapon.light = PersistMove(weapon.light, saved); weapon.heavy = PersistMove(weapon.heavy, saved);
                weapon.launcher = PersistMove(weapon.launcher, saved); weapon.airLight = PersistMove(weapon.airLight, saved);
                weapon.slam = PersistMove(weapon.slam, saved); weapon.dash = PersistMove(weapon.dash, saved); weapon.airDash = PersistMove(weapon.airDash, saved);
                string path = Root + "/Armas/" + weapon.name + ".asset";
                var prior = AssetDatabase.LoadAssetAtPath<WeaponSO>(path);
                if (prior && prior != weapon) { EditorUtility.CopySerialized(weapon, prior); Object.DestroyImmediate(weapon); weapon = prior; }
                else if (!prior) AssetDatabase.CreateAsset(weapon, path);
                config.weapons[i] = weapon;
                EditorUtility.SetDirty(weapon);
            }
        }
        static ComboSO PersistMove(ComboSO move, Dictionary<ComboSO, ComboSO> saved)
        {
            if (!move) return null;
            if (saved.TryGetValue(move, out var known)) return known;
            string path = Root + "/Combos/" + move.name + ".asset";
            var result = AssetDatabase.LoadAssetAtPath<ComboSO>(path);
            if (!result) { result = move; AssetDatabase.CreateAsset(result, path); }
            else if (result != move) EditorUtility.CopySerialized(move, result);
            saved[move] = result;
            result.next = PersistMove(move.next, saved); result.delayedNext = PersistMove(move.delayedNext, saved);
            result.heavyNext = PersistMove(move.heavyNext, saved); EditorUtility.SetDirty(result);
            return result;
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
