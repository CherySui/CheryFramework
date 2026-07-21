using ImGuiNET;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryFramework
{
    public sealed class FrameworkPanel : UnityEngine.MonoBehaviour
    {
        public static bool IsOpen = false;

        private static bool _showFeature1;
        private static bool _showFeature2;
        private static bool _showFeature3;
        private static bool _showSettings;

        public static void Draw()
        {
            if (!IsOpen) return;

            DrawTopBar();
            DrawFeatureWindow("示例功能1", "CheryFrameworkFeature1", ref _showFeature1);
            DrawFeatureWindow("示例功能2", "CheryFrameworkFeature2", ref _showFeature2);
            DrawFeatureWindow("示例功能3", "CheryFrameworkFeature3", ref _showFeature3);
            DrawSettingsWindow();
        }

        private static void DrawTopBar()
        {
            ImGui.PushStyleColor(ImGuiCol.MenuBarBg, new NumVector4(0.067f, 0.082f, 0.106f, 0.94f));
            ImGui.PushStyleColor(ImGuiCol.Separator, new NumVector4(1f, 1f, 1f, 0.14f));
            try
            {
                if (!ImGui.BeginMainMenuBar()) return;
                try
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(0.21f, 0.85f, 1f, 1f));
                    ImGui.TextUnformatted("CheryFramework");
                    ImGui.PopStyleColor();
                    ImGui.Separator();

                    if (DrawTopBarButton("示例功能1", _showFeature1)) _showFeature1 = !_showFeature1;
                    ImGui.SameLine();
                    if (DrawTopBarButton("示例功能2", _showFeature2)) _showFeature2 = !_showFeature2;
                    ImGui.SameLine();
                    if (DrawTopBarButton("示例功能3", _showFeature3)) _showFeature3 = !_showFeature3;
                    ImGui.SameLine();
                    if (DrawTopBarButton("设置", _showSettings)) _showSettings = !_showSettings;
                }
                finally
                {
                    ImGui.EndMainMenuBar();
                }
            }
            finally
            {
                ImGui.PopStyleColor(2);
            }
        }

        private static void DrawFeatureWindow(string title, string id, ref bool visible)
        {
            if (!visible) return;

            NumVector2 display = ImGui.GetIO().DisplaySize;
            ImGui.SetNextWindowPos(display * 0.5f, ImGuiCond.Appearing, new NumVector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new NumVector2(620f, 430f), ImGuiCond.FirstUseEver);

            if (ImGui.Begin(title + "##" + id, ref visible))
            {
                ImGui.TextDisabled("Content");
            }
            ImGui.End();
        }

        private static void DrawSettingsWindow()
        {
            if (!_showSettings) return;

            NumVector2 display = ImGui.GetIO().DisplaySize;
            ImGui.SetNextWindowPos(display * 0.5f, ImGuiCond.Appearing, new NumVector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new NumVector2(460f, 260f), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("设置##CheryFrameworkSettings", ref _showSettings))
            {
                ImGui.TextDisabled("Content");
            }
            ImGui.End();
        }

        private static bool DrawTopBarButton(string label, bool active)
        {
            NumVector4 normal = new NumVector4(0f, 0f, 0f, 0f);
            NumVector4 hovered = new NumVector4(1f, 1f, 1f, 0.10f);
            NumVector4 selected = new NumVector4(0.10f, 0.42f, 0.95f, 0.86f);
            NumVector4 selectedHovered = new NumVector4(0.14f, 0.50f, 1f, 0.94f);

            ImGui.PushStyleColor(ImGuiCol.Button, active ? selected : normal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active ? selectedHovered : hovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new NumVector4(0.07f, 0.32f, 0.78f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(0.95f, 0.97f, 0.99f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NumVector2(10f, 3f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
            bool clicked = ImGui.Button(label);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(4);
            return clicked;
        }
    }
}
