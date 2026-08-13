using System.IO;
using Unity.Services.Multiplayer.Components;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.Multiplayer.Editor.Components
{
    static class SessionConnectorCreateMenu
    {
        // @formatter: off
        const string k_ConnectorTypeSerializedPropertyName = "m_ConnectorType";
        const string k_CreateOrJoinSessionTitle            = "Create or Join Session";
        const string k_CreateSessionTitle                  = "Create Session";
        const string k_DefaultTargetFolder                 = "Assets";
        const string k_FilenameExtension                   = ".asset";
        const string k_MenuRoot                            = "Assets/Create/Services/Multiplayer/Session Connector/";
        // @formatter: on

        /// <summary>
        /// Gets the project folder in which a new asset is created,
        /// based on the current selection in the Project window.
        /// </summary>
        /// <returns>
        /// The path of the selected folder, or the folder containing the
        /// selected asset. Returns <c>"Assets"</c> when nothing is selected.
        /// </returns>
        static string GetTargetFolder()
        {
            if (Selection.activeObject == null)
            {
                return k_DefaultTargetFolder;
            }

            return GetTargetFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        /// <summary>
        /// Gets the nearest enclosing project
        /// folder for the specified asset path.
        /// </summary>
        /// <param name="path">
        /// The asset path to resolve. May be <see langword="null"/> or empty.
        /// </param>
        /// <returns>
        /// <paramref name="path"/> itself when it denotes a valid
        /// folder; the nearest ancestor folder when it denotes an asset
        /// file; or <c>"Assets"</c> when <paramref name="path"/> is
        /// <see langword="null"/> or empty or when we reach the root.
        /// </returns>
        static string GetTargetFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return k_DefaultTargetFolder;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            return GetTargetFolder(Path.GetDirectoryName(path));
        }

        internal static void CreateSessionConnectorAsset(SessionConnectorType connectorType, string defaultFileName)
        {
            var sessionConnector = ScriptableObject.CreateInstance<SessionConnector>();
            var serializedObject = new SerializedObject(sessionConnector);
            serializedObject.FindProperty(k_ConnectorTypeSerializedPropertyName).enumValueIndex = (int)connectorType;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var folder = GetTargetFolder();
            var path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(folder, defaultFileName + k_FilenameExtension));
            ProjectWindowUtil.CreateAsset(sessionConnector, path);
        }

        [MenuItem(k_MenuRoot + k_CreateSessionTitle, false, 0)]
        static void CreateSession()
        {
            CreateSessionConnectorAsset(SessionConnectorType.Create, k_CreateSessionTitle);
        }

        [MenuItem(k_MenuRoot + k_CreateOrJoinSessionTitle, false, 1)]
        static void CreateOrJoinSession()
        {
            CreateSessionConnectorAsset(SessionConnectorType.CreateOrJoin, k_CreateOrJoinSessionTitle);
        }
    }
}
