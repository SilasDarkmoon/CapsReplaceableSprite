﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    [InitializeOnLoad]
    public static class CapsPHSpriteEditor
    {
        static CapsPHSpriteEditor()
        {
            if (!LoadCachedSpriteReplacement())
            {
                CacheAllSpriteReplacement();
            }
            CapsModEditor.ShouldAlreadyInit();
            CapsPackageEditor.OnPackagesChanged += CheckDistributeFlagsAndSpriteReplacement;
            CapsDistributeEditor.OnDistributeFlagsChanged += CheckDistributeFlagsAndSpriteReplacement;
        }

        public static void CreateReplaceableSprite(string assetpath)
        {
            var source = System.IO.Path.GetDirectoryName(assetpath) + "/." + System.IO.Path.GetFileName(assetpath);
            if (System.IO.File.Exists(source))
            {
                Debug.LogWarning("Already created replaceable sprite for " + assetpath);
            }
            else
            {
                string type, mod, dist;
                ResManager.GetAssetNormPath(assetpath, out type, out mod, out dist);
                if (type != "res")
                {
                    Debug.LogError("Can only create replaceable sprite in CapsRes folder. Current: " + assetpath);
                }
                else
                {
                    if (!string.IsNullOrEmpty(mod) && CapsModEditor.IsModOptional(mod) || !string.IsNullOrEmpty(dist))
                    {
                        Debug.LogError("Can only create replaceable sprite in non-mod & non-dist. Current: " + assetpath);
                    }
                    else
                    {
                        var norm = CapsResInfoEditor.GetAssetNormPath(assetpath);
                        _CachedSpritePlaceHolder[norm] = assetpath;
                        _CachedSpriteReplacement[assetpath] = assetpath;

                        var desc = ScriptableObject.CreateInstance<CapsPHSpriteDesc>();
                        AssetDatabase.CreateAsset(desc, assetpath + ".phs.asset");
                        PlatDependant.CopyFile(assetpath, source);
                        var meta = assetpath + ".meta";
                        if (PlatDependant.IsFileExist(meta))
                        {
                            PlatDependant.CopyFile(meta, meta + ".~");
                        }

                        var gitpath = System.IO.Path.GetDirectoryName(assetpath) + "/.gitignore";
                        AddGitIgnore(gitpath, System.IO.Path.GetFileName(assetpath), System.IO.Path.GetFileName(assetpath) + ".meta");

                        CheckSpriteReplacement(norm);
                        SaveCachedSpriteReplacement();
                    }
                }
            }
        }
        [MenuItem("Assets/Create/Replaceable Sprite", priority = 2020)]
        public static void CreateReplaceableSprite()
        {
            bool found = false;
            var guids = Selection.assetGUIDs;
            if (guids != null)
            {
                for (int i = 0; i < guids.Length; ++i)
                {
                    var guid = guids[i];
                    var assetpath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(assetpath))
                    {
                        var ai = AssetImporter.GetAtPath(assetpath);
                        if (ai is TextureImporter)
                        {
                            found = true;
                            CreateReplaceableSprite(assetpath);
                        }
                    }
                }
            }
            if (!found)
            {
                Debug.Log("Cannot create replaceable sprite. No Texture2D or Sprite selected.");
            }
        }
        [MenuItem("Assets/Create/Replaceable Asset (Experimental)", priority = 2021)]
        public static void CreateReplaceableAsset()
        {
            bool found = false;
            var guids = Selection.assetGUIDs;
            if (guids != null)
            {
                for (int i = 0; i < guids.Length; ++i)
                {
                    var guid = guids[i];
                    var assetpath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(assetpath))
                    {
                        if (PlatDependant.IsFileExist(assetpath))
                        {
                            found = true;
                            CreateReplaceableSprite(assetpath);
                        }
                    }
                }
            }
            if (!found)
            {
                Debug.Log("Cannot create replaceable asset. No asset selected.");
            }
        }

        internal readonly static List<string> _CachedDistributeFlags = new List<string>();
        // place holder -> replacement
        internal readonly static Dictionary<string, string> _CachedSpriteReplacement = new Dictionary<string, string>();
        // norm -> place holder full path
        internal readonly static Dictionary<string, string> _CachedSpritePlaceHolder = new Dictionary<string, string>();

        private static bool LoadCachedSpriteReplacement()
        {
            _CachedSpriteReplacement.Clear();
            _CachedSpritePlaceHolder.Clear();
            _CachedDistributeFlags.Clear();
            if (PlatDependant.IsFileExist("EditorOutput/Runtime/phsprite.txt"))
            {
                string json = "";
                using (var sr = PlatDependant.OpenReadText("EditorOutput/Runtime/phsprite.txt"))
                {
                    json = sr.ReadToEnd();
                }
                try
                {
                    var jo = new JSONObject(json);
                    try
                    {
                        var phr = jo["phsprites"] as JSONObject;
                        if (phr != null && phr.type == JSONObject.Type.OBJECT)
                        {
                            for (int i = 0; i < phr.list.Count; ++i)
                            {
                                var key = phr.keys[i];
                                var val = phr.list[i].str;
                                _CachedSpriteReplacement[key] = val;
                                var norm = CapsResInfoEditor.GetAssetNormPath(key);
                                _CachedSpritePlaceHolder[norm] = key;
                            }
                        }
                        var dists = jo["dflags"] as JSONObject;
                        if (dists != null && dists.type == JSONObject.Type.ARRAY)
                        {
                            for (int i = 0; i < dists.list.Count; ++i)
                            {
                                var val = dists.list[i].str;
                                _CachedDistributeFlags.Add(val);
                            }
                        }
                    }
                    catch { }
                }
                catch { }
                return true;
            }
            return false;
        }
        private static void SaveCachedSpriteReplacement()
        {
            var jo = new JSONObject(JSONObject.Type.OBJECT);
            var phs = new JSONObject(JSONObject.Type.OBJECT);
            jo["phsprites"] = phs;
            foreach (var kvp in _CachedSpriteReplacement)
            {
                phs[kvp.Key] = JSONObject.CreateStringObject(kvp.Value);
            }
            var dflags = new JSONObject(JSONObject.Type.ARRAY);
            jo["dflags"] = dflags;
            for (int i = 0; i < _CachedDistributeFlags.Count; ++i)
            {
                dflags.Add(_CachedDistributeFlags[i]);
            }
            using (var sw = PlatDependant.OpenWriteText("EditorOutput/Runtime/phsprite.txt"))
            {
                sw.Write(jo.ToString(true));
            }
        }
        private static void CacheAllSpriteReplacement()
        {
            var assets = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < assets.Length; ++i)
            {
                var asset = assets[i];
                if (asset.EndsWith(".phs.asset"))
                {
                    string type, mod, dist;
                    ResManager.GetAssetNormPath(asset, out type, out mod, out dist);
                    if (type == "res" && (string.IsNullOrEmpty(mod) || !CapsModEditor.IsModOptional(mod)) && string.IsNullOrEmpty(dist))
                    {
                        AddPHSprite(asset);
                    }
                }
            }
        }

        private static bool AddPHSprite(string descpath)
        {
            if (!string.IsNullOrEmpty(descpath) && descpath.EndsWith(".phs.asset"))
            {
                var asset = descpath.Substring(0, descpath.Length - ".phs.asset".Length);
                if (!_CachedSpriteReplacement.ContainsKey(asset))
                {
                    var norm = CapsResInfoEditor.GetAssetNormPath(asset);
                    _CachedSpritePlaceHolder[norm] = asset;
                    _CachedSpriteReplacement[asset] = "";
                    CheckSpriteReplacement(norm);
                    return true;
                }
            }
            return false;
        }
        private static void RemovePHSprite(string descpath)
        {
            if (!string.IsNullOrEmpty(descpath) && descpath.EndsWith(".phs.asset"))
            {
                var asset = descpath.Substring(0, descpath.Length - ".phs.asset".Length);
                var norm = CapsResInfoEditor.GetAssetNormPath(asset);
                _CachedSpritePlaceHolder.Remove(norm);
                _CachedSpriteReplacement.Remove(asset);
            }
        }
        private static void DeletePHSprite(string descpath)
        {
            RemovePHSprite(descpath);
            if (!string.IsNullOrEmpty(descpath) && descpath.EndsWith(".phs.asset"))
            {
                var asset = descpath.Substring(0, descpath.Length - ".phs.asset".Length);
                var source = System.IO.Path.GetDirectoryName(asset) + "/." + System.IO.Path.GetFileName(asset);
                if (System.IO.File.Exists(source))
                {
                    System.IO.File.Delete(asset);
                    System.IO.File.Move(source, asset);
                    var phmetasrc = asset + ".meta.~";
                    if (PlatDependant.IsFileExist(phmetasrc))
                    {
                        PlatDependant.MoveFile(phmetasrc, asset + ".meta");
                    }

                    var gitpath = System.IO.Path.GetDirectoryName(asset) + "/.gitignore";
                    RemoveGitIgnore(gitpath, System.IO.Path.GetFileName(asset), System.IO.Path.GetFileName(asset) + ".meta");

                    AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static bool CheckDistributeFlags()
        {
            var flags = ResManager.GetValidDistributeFlags();
            if (flags.Length != _CachedDistributeFlags.Count)
            {
                return true;
            }
            for (int i = 0; i < flags.Length; ++i)
            {
                if (flags[i] != _CachedDistributeFlags[i])
                {
                    return true;
                }
            }
            return false;
        }
        private static void CheckDistributeFlagsAndSpriteReplacement()
        {
            if (CheckDistributeFlags())
            {
                foreach (var kvp in _CachedSpritePlaceHolder)
                {
                    CheckSpriteReplacement(kvp.Key);
                }
                _CachedDistributeFlags.Clear();
                _CachedDistributeFlags.AddRange(ResManager.GetValidDistributeFlags());
                SaveCachedSpriteReplacement();
            }
        }

        private static bool CheckSpriteReplacement(string phnorm)
        {
            string phasset;
            if (_CachedSpritePlaceHolder.TryGetValue(phnorm, out phasset))
            {
                var real = ResManager.EditorResLoader.CheckDistributePath("CapsRes/" + phnorm, true);
                if (string.IsNullOrEmpty(real))
                {
                    real = phasset;
                }
                if (_CachedSpriteReplacement[phasset] != real || !PlatDependant.IsFileExist(phasset))
                {
                    _CachedSpriteReplacement[phasset] = real;
                    var phmeta = phasset + ".meta";
                    if (!PlatDependant.IsFileExist(phmeta))
                    {
                        var phmetasrc = phmeta + ".~";
                        if (PlatDependant.IsFileExist(phmetasrc))
                        {
                            PlatDependant.CopyFile(phmetasrc, phmeta);
                        }
                    }
                    if (real == phasset)
                    {
                        var source = System.IO.Path.GetDirectoryName(phasset) + "/." + System.IO.Path.GetFileName(phasset);
                        PlatDependant.CopyFile(source, phasset);
                    }
                    else
                    {
                        PlatDependant.CopyFile(real, phasset);
                    }
                    AssetDatabase.ImportAsset(phasset, ImportAssetOptions.ForceUpdate);
                    return true;
                }
            }
            return false;
        }

        internal static void RestoreAllReplacement()
        {
            foreach (var kvp in _CachedSpriteReplacement)
            {
                var phasset = kvp.Key;
                var source = System.IO.Path.GetDirectoryName(phasset) + "/." + System.IO.Path.GetFileName(phasset);
                if (PlatDependant.IsFileExist(source))
                {
                    PlatDependant.CopyFile(source, phasset);
                    AssetDatabase.ImportAsset(phasset, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        internal static void RemakeAllReplacement()
        {
            foreach (var kvp in _CachedSpriteReplacement)
            {
                var phasset = kvp.Key;
                var source = kvp.Value;
                if (PlatDependant.IsFileExist(source))
                {
                    PlatDependant.CopyFile(source, phasset);
                    AssetDatabase.ImportAsset(phasset, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static void AddGitIgnore(string gitignorepath, params string[] items)
        {
            List<string> lines = new List<string>();
            HashSet<string> lineset = new HashSet<string>();
            if (PlatDependant.IsFileExist(gitignorepath))
            {
                try
                {
                    using (var sr = PlatDependant.OpenReadText(gitignorepath))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            lines.Add(line);
                            lineset.Add(line);
                        }
                    }
                }
                catch { }
            }

            if (items != null)
            {
                for (int i = 0; i < items.Length; ++i)
                {
                    var item = items[i];
                    if (lineset.Add(item))
                    {
                        lines.Add(item);
                    }
                }
            }

            using (var sw = PlatDependant.OpenWriteText(gitignorepath))
            {
                for (int i = 0; i < lines.Count; ++i)
                {
                    sw.WriteLine(lines[i]);
                }
            }
        }
        private static void RemoveGitIgnore(string gitignorepath, params string[] items)
        {
            List<string> lines = new List<string>();
            HashSet<string> removes = new HashSet<string>();
            if (items != null)
            {
                removes.UnionWith(items);
            }
            if (PlatDependant.IsFileExist(gitignorepath))
            {
                try
                {
                    using (var sr = PlatDependant.OpenReadText(gitignorepath))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!removes.Contains(line))
                            {
                                lines.Add(line);
                            }
                        }
                    }
                }
                catch { }
            }
            if (lines.Count == 0)
            {
                PlatDependant.DeleteFile(gitignorepath);
            }
            else
            {
                using (var sw = PlatDependant.OpenWriteText(gitignorepath))
                {
                    for (int i = 0; i < lines.Count; ++i)
                    {
                        sw.WriteLine(lines[i]);
                    }
                }
            }
        }

        private class CapsPHSpritePostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                bool dirty = false;
                if (importedAssets != null)
                {
                    for (int i = 0; i < importedAssets.Length; ++i)
                    {
                        var asset = importedAssets[i];
                        if (asset.EndsWith(".phs.asset"))
                        {
                            dirty |= AddPHSprite(asset);
                        }
                        else //if (AssetImporter.GetAtPath(asset) is TextureImporter)
                        {
                            var norm = CapsResInfoEditor.GetAssetNormPath(asset);
                            if (_CachedSpritePlaceHolder.ContainsKey(norm))
                            {
                                if (!_CachedSpriteReplacement.ContainsKey(asset))
                                {
                                    dirty |= CheckSpriteReplacement(norm);
                                }
                            }
                        }
                    }
                }
                if (deletedAssets != null)
                {
                    for (int i = 0; i < deletedAssets.Length; ++i)
                    {
                        var asset = deletedAssets[i];
                        if (asset.EndsWith(".phs.asset"))
                        {
                            dirty = true;
                            DeletePHSprite(asset);
                        }
                        else
                        {
                            var norm = CapsResInfoEditor.GetAssetNormPath(asset);
                            if (_CachedSpritePlaceHolder.ContainsKey(norm))
                            {
                                dirty |= CheckSpriteReplacement(norm);
                            }
                        }
                    }
                }
                if (movedAssets != null)
                {
                    for (int i = 0; i < movedAssets.Length; ++i)
                    {
                        var asset = movedAssets[i];
                        if (asset.EndsWith(".phs.asset"))
                        {
                            dirty |= AddPHSprite(asset);
                        }
                        else //if (AssetImporter.GetAtPath(asset) is TextureImporter)
                        {
                            var norm = CapsResInfoEditor.GetAssetNormPath(asset);
                            if (_CachedSpritePlaceHolder.ContainsKey(norm))
                            {
                                if (!_CachedSpriteReplacement.ContainsKey(asset))
                                {
                                    dirty |= CheckSpriteReplacement(norm);
                                }
                            }
                        }
                    }
                }
                if (movedFromAssetPaths != null)
                {
                    for (int i = 0; i < movedFromAssetPaths.Length; ++i)
                    {
                        var asset = movedFromAssetPaths[i];
                        if (asset.EndsWith(".phs.asset"))
                        {
                            dirty = true;
                            DeletePHSprite(asset);
                        }
                        else
                        {
                            var norm = CapsResInfoEditor.GetAssetNormPath(asset);
                            if (_CachedSpritePlaceHolder.ContainsKey(norm))
                            {
                                dirty |= CheckSpriteReplacement(norm);
                            }
                        }
                    }
                }
                if (dirty)
                {
                    SaveCachedSpriteReplacement();
                }
            }
        }
    }
}