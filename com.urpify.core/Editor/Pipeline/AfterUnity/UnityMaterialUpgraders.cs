using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Unity's own Built-in to URP material upgraders, reached by reflection (their list is internal): the fixes after
    /// Unity's converter use them to treat a material exactly as Unity's converter treats its shader.
    /// </summary>
    public static class UnityMaterialUpgraders
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
#if !URPIFY_ASSET_STORE
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
#endif

        /// <summary>
        /// Unity's Built-in to URP material upgraders: IMaterialUpgradersProvider on URP 17, GetUpgraders on URP 14. The Asset
        /// Store build uses no internal Editor API (Submission Guidelines 2.5.g): there the list is the public
        /// MaterialUpgrader.FetchAllUpgradersForPipeline (URP 17.3 and later), and on older URP there is none.
        /// </summary>
        public static IList All()
        {
#if !URPIFY_ASSET_STORE
            if (!BuildInfo.AssetStore) return FromProviders();
#endif
            return FromPublicList();
        }

        // Only Unity's render pipeline assemblies: Urpify's tests declare a fake converter of the same name (M2Tests).
        static Assembly[] PipelineAssemblies() =>
            AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("Unity.RenderPipelines.", StringComparison.Ordinal)).ToArray();

#if !URPIFY_ASSET_STORE
        static IList FromProviders()
        {
            var asms = PipelineAssemblies();
            var baseType = asms.Select(a => a.GetType("UnityEditor.Rendering.MaterialUpgrader", false)).FirstOrDefault(t => t != null);
            if (baseType == null) return null;
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(baseType));
            var providerType = asms.Select(a => a.GetType("UnityEditor.Rendering.IMaterialUpgradersProvider", false)).FirstOrDefault(t => t != null);
            if (providerType != null)
            {
                foreach (var t in TypeCache.GetTypesDerivedFrom(providerType).Where(t => !t.IsAbstract && t.Namespace == "UnityEditor.Rendering.Universal" && !t.Name.Contains("2D")))
                {
                    var provider = Activator.CreateInstance(t, true);
                    if (t.GetMethod("GetUpgraders", Any)?.Invoke(provider, null) is IEnumerable ups)
                        foreach (var u in ups) list.Add(u);
                }
                return list.Count > 0 ? list : null;
            }
            var urp14 = asms.Select(a => a.GetType("UnityEditor.Rendering.Universal.UniversalRenderPipelineMaterialUpgrader", false)).FirstOrDefault(t => t != null);
            var get = urp14?.GetMethod("GetUpgraders", Any);
            if (get == null) return null;
            var args = new object[] { list };
            get.Invoke(null, args);
            return (IList)args[0];
        }
#endif

        /// <summary>
        /// MaterialUpgrader.FetchAllUpgradersForPipeline(UniversalRenderPipelineAsset), without the upgraders for the 2D
        /// Renderer (they turn Standard into Mesh2D-Lit-Default), as <see cref="All"/> leaves them out on URP 17.
        /// </summary>
        static IList FromPublicList()
        {
            var asms = PipelineAssemblies();
            var baseType = asms.Select(a => a.GetType("UnityEditor.Rendering.MaterialUpgrader", false)).FirstOrDefault(t => t != null);
            var assetType = asms.Select(a => a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset", false)).FirstOrDefault(t => t != null);
            var fetch = baseType?.GetMethod("FetchAllUpgradersForPipeline", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type) }, null);
            if (fetch == null || assetType == null) return null;
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(baseType));
            if (fetch.Invoke(null, new object[] { assetType }) is IEnumerable ups)
                foreach (var u in ups)
                    if (u != null && !For2D(u.GetType())) list.Add(u);
            return list.Count > 0 ? list : null;
        }

        static bool For2D(Type t) =>
            t.Name.IndexOf("2D", StringComparison.OrdinalIgnoreCase) >= 0 || t.GetInterfaces().Any(i => i.Name.IndexOf("2d", StringComparison.OrdinalIgnoreCase) >= 0);

        public static void Upgrade(IList upgraders, Material m)
        {
            var baseType = upgraders.GetType().GetGenericArguments()[0];
            var flagsType = baseType.GetNestedType("UpgradeFlags", BindingFlags.Public);
            var method = baseType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == "Upgrade" && x.GetParameters().Length == 3 && x.GetParameters()[1].ParameterType == upgraders.GetType());
            method?.Invoke(null, new object[] { m, upgraders, Enum.ToObject(flagsType, 0) });
        }

        /// <summary>The Built-in shader name an upgrader converts from.</summary>
        public static string OldShader(object upgrader)
        {
            if (upgrader?.GetType().GetProperty("OldShaderPath", Public)?.GetValue(upgrader) is string s) return s;
#if !URPIFY_ASSET_STORE
            if (!BuildInfo.AssetStore) return upgrader?.GetType().GetField("m_OldShader", Any)?.GetValue(upgrader) as string;
#endif
            return null;
        }
    }
}
