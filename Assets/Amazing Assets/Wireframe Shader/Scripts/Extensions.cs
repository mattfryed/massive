// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;


[assembly: InternalsVisibleTo("AmazingAssets.WireframeShader.Editor")]
[assembly: InternalsVisibleTo("AmazingAssets.WireframeShaderEditor")]
namespace AmazingAssets.WireframeShader
{
    static internal class ExtensionsForMesh
    {
        public static int GetTrianglesCount(this Mesh mesh)
        {
            if (mesh == null)
                return 0;

            return (int)(mesh.GetIndexStart(mesh.subMeshCount - 1) + mesh.GetIndexCount(mesh.subMeshCount - 1));
        }
    }
    static internal class ExtensionsForArray
    {
        public static void Populate<T>(this T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
        }
        public static void Populate<T>(this List<T> list, int count, T value)
        {
            list.Clear(); 
            for (int i = 0; i < count; i++)
            {
                list.Add(value);
            }
        }
    }
    static internal class ExtensionForString
    {
        public static string RemoveWhiteSpace(this string str)
        {
            return string.Join("", str.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }
    }
}