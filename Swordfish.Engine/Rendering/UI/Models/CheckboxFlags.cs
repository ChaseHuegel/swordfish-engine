using System;
using System.Xml.Serialization;
using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements;

namespace Swordfish.Engine.Rendering.UI.Models
{
    public class CheckboxFlags : Element
    {
        public int Value;

        public string TypeName;

        [XmlIgnore]
        public EventHandler<EventArgs> ValueChanged;

        private Type Type => type ?? (type = Type.GetType(TypeName));
        private Type type;
        private int oldValue;

        public CheckboxFlags() {}

        public CheckboxFlags(Type type) {
            this.type = type;
            TypeName = Type.ToString();
        }

        public override void OnShow()
        {
            base.OnShow();

            ImGui.BeginGroup();
            
            oldValue = Value;
            foreach (int enumValue in Enum.GetValues(Type))
                ImGui.CheckboxFlags($"{Enum.GetName(Type, enumValue)}##{Name}{enumValue}", ref Value, enumValue);
            
            ImGui.EndGroup();
            base.TryShowTooltip();

            if (oldValue != Value)
                ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
