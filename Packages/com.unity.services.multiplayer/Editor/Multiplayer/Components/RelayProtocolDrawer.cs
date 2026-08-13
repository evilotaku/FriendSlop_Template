using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Services.Multiplayer.Editor.Components
{
    [CustomPropertyDrawer(typeof(RelayProtocol))]
    class RelayProtocolDrawer : PropertyDrawer
    {
        static readonly List<RelayProtocol> k_Choices = new()
        {
            RelayProtocol.UDP,
            RelayProtocol.DTLS,
            RelayProtocol.WSS,
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var current = (RelayProtocol)property.intValue;
            if (!k_Choices.Contains(current))
                current = RelayProtocol.Default;

            var field = new PopupField<RelayProtocol>(property.displayName, k_Choices, current);
            field.AddToClassList(BaseField<RelayProtocol>.alignedFieldUssClassName);

            field.RegisterValueChangedCallback(evt =>
            {
                property.intValue = (int)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });

            return field;
        }
    }
}
