using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Object = UnityEngine.Object;

namespace Capstones.UnityEngineEx
{
    public static class CapsPHSpriteLoader
    {
        public const int CapsResManifestItemType_Virtual = 11;

        public class TypedResLoader_Virtual : ResManager.ClientResLoader.TypedResLoader_Normal, ResManager.IAssetBundleLoaderEx
        {
            public override int ResItemType { get { return CapsResManifestItemType_Virtual; } }

            public override string FormatBundleNameFor(CapsResManifestItem item)
            {
                var node = item.Node;
                var depth = node.GetDepth();
                string[] parts = new string[depth];
                for (int i = depth - 1; i >= 0; --i)
                {
                    parts[i] = node.PPath;
                    node = node.Parent;
                }

                var mod = item.Manifest.MFlag;
                var dist = item.Manifest.DFlag;
                var rootdepth = 2; // Assets/CapsRes/
                if (depth > 2 && parts[1] == "Mods")
                {
                    rootdepth += 2; // Assets/Mods/XXX/CapsRes/
                }
                else if (depth > 1 && parts[0] == "Packages")
                {
                    rootdepth += 1; // Packages/xx.xx.xx/CapsRes/
                }
                if (!string.IsNullOrEmpty(dist))
                {
                    rootdepth += 2; // .../dist/XXX/
                }

                System.Text.StringBuilder sbbundle = new System.Text.StringBuilder();
                sbbundle.Append("v");
                for (int i = rootdepth; i < depth - 1; ++i)
                {
                    sbbundle.Append("-");
                    sbbundle.Append(parts[i].ToLower());
                }
                var filename = item.Node.PPath;
                var assetName = parts[depth - 1];
                sbbundle.Append("-");
                sbbundle.Append(assetName.ToLower());
                sbbundle.Append(".ab");
                return sbbundle.ToString();
            }

            public bool LoadAssetBundle(string mod, string name, bool isContainingBundle, out ResManager.AssetBundleInfo bi)
            {
                bi = null;
                if (name.StartsWith("v-"))
                {
                    CapsResManifestNode bnode;
                    string bnorm = name;
                    var split = name.IndexOf(".ab.m-");
                    if (split > 0)
                    {
                        bnorm = name.Substring(0, split + ".ab".Length);
                    }
                    if (ResManager.ClientResLoader.CollapsedManifest.TryGetItem("virtual/" + bnorm, out bnode))
                    {
                        CapsResManifestItem bitem = bnode.Item;
                        if (bitem != null)
                        {
                            while (true)
                            {
                                if (bitem.Ref == null)
                                {
                                    break;
                                }
                                bitem = bitem.Ref;
                            }
                            var bopmod = bitem.Manifest.MFlag;
                            var bmod = bopmod;
                            if (bitem.Manifest.InMain)
                            {
                                bmod = "";
                            }
                            bi = ResManager.LoadAssetBundle(bmod, bnorm + ".m-" + (bopmod ?? "").ToLower() + "-d-" + (bitem.Manifest.DFlag ?? "").ToLower(), bnorm);
                        }
                    }
                    return true;
                }
                return false;
            }
        }
        public static readonly TypedResLoader_Virtual __TypedResLoader_Virtual = new TypedResLoader_Virtual();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnUnityStart()
        {
            ResManager.AssetBundleLoaderEx.Add(__TypedResLoader_Virtual);
        }
    }
}