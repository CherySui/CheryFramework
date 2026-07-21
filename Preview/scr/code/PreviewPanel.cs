using System.Numerics;
using Hexa.NET.ImGui;

namespace CheryFramework.Preview;

internal static class PreviewPanel
{
    private static bool _uiOpen;
    private static bool _showFeature1;
    private static bool _showFeature2;
    private static bool _showFeature3;
    private static bool _showSettings;
    private static bool _enableExampleBackground;
    private static ImTextureRef _exampleBackgroundTexture;
    private static Vector2 _exampleBackgroundSize;

    public static void SetExampleBackground(ImTextureRef texture, Vector2 pixelSize)
    {
        _exampleBackgroundTexture = texture;
        _exampleBackgroundSize = pixelSize;
    }

    public static void ClearExampleBackground()
    {
        _exampleBackgroundTexture = default;
        _exampleBackgroundSize = default;
    }

    public static void Draw()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Insert, false)) _uiOpen = !_uiOpen;

        DrawExampleBackground();
        if (!_uiOpen) return;

        DrawTopBar();
        DrawFeatureWindow("示例功能1", "CheryFrameworkFeature1", ref _showFeature1);
        DrawFeatureWindow("示例功能2", "CheryFrameworkFeature2", ref _showFeature2);
        DrawFeatureWindow("示例功能3", "CheryFrameworkFeature3", ref _showFeature3);
        DrawSettingsWindow();
    }

    private static void DrawExampleBackground()
    {
        if (!_enableExampleBackground || _exampleBackgroundTexture.TexID.IsNull) return;

        Vector2 display = ImGui.GetIO().DisplaySize;
        if (display.X <= 0f || display.Y <= 0f || _exampleBackgroundSize.X <= 0f || _exampleBackgroundSize.Y <= 0f) return;

        float scale = MathF.Max(display.X / _exampleBackgroundSize.X, display.Y / _exampleBackgroundSize.Y);
        Vector2 drawSize = _exampleBackgroundSize * scale;
        Vector2 topLeft = (display - drawSize) * 0.5f;
        ImGui.GetBackgroundDrawList().AddImage(_exampleBackgroundTexture, topLeft, topLeft + drawSize);
    }

    private static void DrawTopBar()
    {
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg, new Vector4(0.067f, 0.082f, 0.106f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(1f, 1f, 1f, 0.14f));
        try
        {
            if (!ImGui.BeginMainMenuBar()) return;
            try
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.21f, 0.85f, 1f, 1f));
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

        Vector2 display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(display * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f));
        ImGui.SetNextWindowSize(new Vector2(620f, 430f), ImGuiCond.FirstUseEver);

        if (ImGui.Begin(title + "##" + id, ref visible))
        {
            ImGui.TextDisabled("Content");
        }
        ImGui.End();
    }

    private static void DrawSettingsWindow()
    {
        if (!_showSettings) return;

        Vector2 display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(display * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f));
        ImGui.SetNextWindowSize(new Vector2(460f, 260f), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("设置##CheryFrameworkSettings", ref _showSettings))
        {
            ImGui.Checkbox("启用示例背景图片", ref _enableExampleBackground);
        }
        ImGui.End();
    }

    private static bool DrawTopBarButton(string label, bool active)
    {
        Vector4 normal = new(0f, 0f, 0f, 0f);
        Vector4 hovered = new(1f, 1f, 1f, 0.10f);
        Vector4 selected = new(0.10f, 0.42f, 0.95f, 0.86f);
        Vector4 selectedHovered = new(0.14f, 0.50f, 1f, 0.94f);

        ImGui.PushStyleColor(ImGuiCol.Button, active ? selected : normal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active ? selectedHovered : hovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.07f, 0.32f, 0.78f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.97f, 0.99f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 3f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        bool clicked = ImGui.Button(label);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        return clicked;
    }
}
