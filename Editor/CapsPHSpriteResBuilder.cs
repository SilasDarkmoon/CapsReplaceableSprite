using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    public class CapsPHSpriteResBuilder : CapsResBuilder.IResBuilderEx
    {
        public void Prepare(string output)
        {
            CapsPHSpriteEditor.RestoreAllReplacement();
        }
        public void Cleanup()
        {
            CapsPHSpriteEditor.RemakeAllReplacement();
        }
        public void OnSuccess()
        {
        }

        private class BuildingItemInfo
        {
            public string Asset;
            public string Mod;
            public string Dist;
            public string Norm;
            public string Bundle;
            public string Variant;
        }
        private BuildingItemInfo _Building;
        public string FormatBundleName(string asset, string mod, string dist, string norm)
        {
            _Building = null;
            if (CapsPHSpriteEditor._CachedSpritePlaceHolder.ContainsKey(norm))
            {
                System.Text.StringBuilder sbbundle = new System.Text.StringBuilder();
                sbbundle.Append("v-");
                sbbundle.Append(norm.ToLower());
                sbbundle.Replace('\\', '-');
                sbbundle.Replace('/', '-');
                sbbundle.Append(".ab");
                _Building = new BuildingItemInfo()
                {
                    Asset = asset,
                    Mod = mod,
                    Dist = dist,
                    Norm = norm,
                    Bundle = sbbundle.ToString(),
                    Variant = "m-" + (mod ?? "").ToLower() + "-d-" + (dist ?? "").ToLower(),
                };
                return _Building.Bundle + "." + _Building.Variant;
            }
            return null;
        }
        public bool CreateItem(CapsResManifestNode node)
        {
            if (_Building != null)
            {
                return true;
            }
            return false;
        }
        public void ModifyItem(CapsResManifestItem item)
        {
            if (_Building != null)
            {
                item.Type = CapsPHSpriteLoader.CapsResManifestItemType_Virtual;
                item.BRef = null;

                var asset = _Building.Asset;
                string rootpath = "Assets/CapsRes/";
                bool inPackage = false;
                if (asset.StartsWith("Assets/Mods/") || (inPackage = asset.StartsWith("Packages/")))
                {
                    int index;
                    if (inPackage)
                    {
                        index = asset.IndexOf('/', "Packages/".Length);
                    }
                    else
                    {
                        index = asset.IndexOf('/', "Assets/Mods/".Length);
                    }
                    if (index > 0)
                    {
                        rootpath = asset.Substring(0, index) + "/CapsRes/";
                    }
                }
                var dist = _Building.Dist;
                if (string.IsNullOrEmpty(dist))
                {
                    rootpath += "virtual/";
                }
                else
                {
                    rootpath = rootpath + "dist/" + dist + "/virtual/";
                }

                var newpath = rootpath + _Building.Bundle.ToLower();
                CapsResManifestNode newnode = item.Manifest.AddOrGetItem(newpath);
                var newitem = new CapsResManifestItem(newnode);
                newitem.Type = (int)CapsResManifestItemType.Redirect;
                newitem.BRef = null;
                newitem.Ref = item;
                newnode.Item = newitem;
            }
        }

        public void GenerateBuildWork(string bundleName, IList<string> assets, ref AssetBundleBuild abwork, CapsResBuilder.CapsResBuildWork modwork, int abindex)
        {
            if (bundleName.StartsWith("v-"))
            {
                var split = bundleName.IndexOf(".ab.m-");
                if (split > 0)
                {
                    var name = bundleName.Substring(0, split + ".ab".Length);
                    var variant = bundleName.Substring(split + ".ab.".Length);
                    abwork.assetBundleName = name;
                    abwork.assetBundleVariant = variant;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class CapsPHSpriteResBuilderEntry
    {
        private static CapsPHSpriteResBuilder _Builder = new CapsPHSpriteResBuilder();
        static CapsPHSpriteResBuilderEntry()
        {
            CapsResBuilder.ResBuilderEx.Add(_Builder);
        }
    }
}