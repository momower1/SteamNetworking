using UnityEngine;
using UnityEditor;

namespace SteamNetworking
{
    public static class EditorCreateNetworkBehaviour
    {
        [MenuItem("Assets/Create/C# Script NetworkBehaviour", priority = 81)]
        static void CreateNetworkBehaviour()
        {
            string templatePath = "Assets/Scripts/Networking/NetworkBehaviourTemplate.cs.txt";
            string copyPath = AssetDatabase.GetAssetPath(Selection.activeObject) + "/NetworkBehaviourTemplate.cs";

            AssetDatabase.CopyAsset(templatePath, copyPath);
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(copyPath));
        }
    }
}