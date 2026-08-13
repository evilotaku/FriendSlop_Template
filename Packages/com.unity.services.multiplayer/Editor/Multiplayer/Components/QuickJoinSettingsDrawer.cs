using Unity.Services.Multiplayer.Components;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Services.Multiplayer.Editor.Components
{
    [CustomPropertyDrawer(typeof(QuickJoinSettings))]
    class QuickJoinSettingsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var timeoutField = new PropertyField(property.FindPropertyRelative("m_Timeout"), "Matchmaking Timeout (s)");
            var createSessionField = new PropertyField(property.FindPropertyRelative("m_CreateSession"), "Create Session Fallback");

            container.Add(timeoutField);
            container.Add(createSessionField);

            return container;
        }
    }
}
