using System;
using System.Collections.Generic;
using System.IO;
using ImGuiNET;
using UnityEngine;
using UnityEngine.UI;
using NumVector2 = System.Numerics.Vector2;

namespace CheryFramework
{
    public sealed unsafe class ImGuiController : MonoBehaviour
    {
        private IntPtr _context;
        private Texture2D _fontTexture;
        private Material _material;
        private Canvas _canvas;
        private readonly List<CanvasRenderer> _renderers = new List<CanvasRenderer>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Vector3[] _vertices = new Vector3[0];
        private Vector2[] _uvs = new Vector2[0];
        private Color32[] _colors = new Color32[0];
        private int[] _indices = new int[0];
        private int[] _commandIndices = new int[0];
        private bool _canvasHasContent;


        private void Awake()
        {
            _context = ImGui.CreateContext();
            ImGui.SetCurrentContext(_context);
            ImGui.GetIO().NativePtr->IniFilename = null;
            BuildFontAtlas();
            ApplyStyle();

            Shader shader = Shader.Find("UI/Default");
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = _fontTexture };

            GameObject canvasObject = new GameObject("CheryFramework_ImGui_Canvas");
            DontDestroyOnLoad(canvasObject);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32767;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
        }

        private void Update()
        {
            ImGui.SetCurrentContext(_context);

            if (Input.GetKeyDown(KeyCode.Insert))
            {
                FrameworkPanel.IsOpen = !FrameworkPanel.IsOpen;
            }

            if (!FrameworkPanel.IsOpen)
            {
                ClearCanvas();
                return;
            }

            float scale = Mathf.Clamp(Main.Settings != null ? Main.Settings.UiScale : 1f, 0.75f, 1.75f);
            ImGuiIOPtr io = ImGui.GetIO();
            io.DisplaySize = new NumVector2(Screen.width / scale, Screen.height / scale);
            io.DisplayFramebufferScale = NumVector2.One;
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f);

            io.AddMousePosEvent(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
            io.AddMouseButtonEvent(0, Input.GetMouseButton(0));
            io.AddMouseButtonEvent(1, Input.GetMouseButton(1));
            io.AddMouseButtonEvent(2, Input.GetMouseButton(2));
            io.AddMouseWheelEvent(Input.mouseScrollDelta.x, Input.mouseScrollDelta.y);
            AddKeyboardEvents(io);

            string text = Input.inputString;
            for (int i = 0; i < text.Length; i++) io.AddInputCharacter(text[i]);

            ImGui.NewFrame();
            FrameworkPanel.Draw();
            ImGui.Render();
            UpdateCanvas(scale);
        }

        private static void AddKeyboardEvents(ImGuiIOPtr io)
        {
            AddKey(io, ImGuiKey.Tab, KeyCode.Tab);
            AddKey(io, ImGuiKey.LeftArrow, KeyCode.LeftArrow);
            AddKey(io, ImGuiKey.RightArrow, KeyCode.RightArrow);
            AddKey(io, ImGuiKey.UpArrow, KeyCode.UpArrow);
            AddKey(io, ImGuiKey.DownArrow, KeyCode.DownArrow);
            AddKey(io, ImGuiKey.PageUp, KeyCode.PageUp);
            AddKey(io, ImGuiKey.PageDown, KeyCode.PageDown);
            AddKey(io, ImGuiKey.Home, KeyCode.Home);
            AddKey(io, ImGuiKey.End, KeyCode.End);
            AddKey(io, ImGuiKey.Insert, KeyCode.Insert);
            AddKey(io, ImGuiKey.Delete, KeyCode.Delete);
            AddKey(io, ImGuiKey.Backspace, KeyCode.Backspace);
            AddKey(io, ImGuiKey.Space, KeyCode.Space);
            io.AddKeyEvent(ImGuiKey.Enter, Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter));
            AddKey(io, ImGuiKey.Escape, KeyCode.Escape);
            AddKey(io, ImGuiKey.A, KeyCode.A);
            AddKey(io, ImGuiKey.C, KeyCode.C);
            AddKey(io, ImGuiKey.V, KeyCode.V);
            AddKey(io, ImGuiKey.X, KeyCode.X);
            AddKey(io, ImGuiKey.Y, KeyCode.Y);
            AddKey(io, ImGuiKey.Z, KeyCode.Z);
            io.AddKeyEvent(ImGuiKey.ModCtrl, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            io.AddKeyEvent(ImGuiKey.ModShift, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            io.AddKeyEvent(ImGuiKey.ModAlt, Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            io.AddKeyEvent(ImGuiKey.ModSuper, Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand));
        }

        private static void AddKey(ImGuiIOPtr io, ImGuiKey imguiKey, KeyCode unityKey)
        {
            io.AddKeyEvent(imguiKey, Input.GetKey(unityKey));
        }

        private void BuildFontAtlas()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            string path = Path.Combine(Main.ModEntry.Path, "Resources", "MiSans-Bold.ttf");
            if (File.Exists(path))
            {
                io.Fonts.AddFontFromFileTTF(path, 20f, null, io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            }
            else
            {
                io.Fonts.AddFontDefault();
                Main.Logger.Log("MiSans 字体不存在：" + path);
            }

            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
            _fontTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _fontTexture.LoadRawTextureData(pixels, width * height * bytesPerPixel);
            _fontTexture.Apply(false, false);
            io.Fonts.SetTexID((IntPtr)_fontTexture.GetInstanceID());
            io.Fonts.ClearTexData();
        }

        private static void ApplyStyle()
        {
            // Keep this in lockstep with CheryTools ImGuiController.ClampPanelStyle().
            // Do not add preview-only padding, colors or scaling here.
            ImGuiStylePtr style = ImGui.GetStyle();
            style.WindowRounding = 6f;
            style.ChildRounding = 5f;
            style.PopupRounding = 5f;
            style.FrameRounding = 4f;
            style.GrabRounding = 4f;
            style.ScrollbarRounding = 6f;
            style.TabRounding = 4f;
        }

        private unsafe void UpdateCanvas(float scale)
        {
            ImDrawDataPtr drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0)
            {
                ClearCanvas();
                return;
            }

            _canvasHasContent = true;
            int renderedCommands = 0;
            float offsetX = -Screen.width * 0.5f;
            float offsetY = Screen.height * 0.5f;

            for (int listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
            {
                ImDrawListPtr list = drawData.CmdLists[listIndex];
                int vertexCount = list.VtxBuffer.Size;
                int indexCount = list.IdxBuffer.Size;
                EnsureArrayCapacity(vertexCount, indexCount);

                ImDrawVert* vertexPtr = (ImDrawVert*)list.VtxBuffer.Data;
                for (int i = 0; i < vertexCount; i++)
                {
                    _vertices[i] = new Vector3(offsetX + vertexPtr[i].pos.X * scale, offsetY - vertexPtr[i].pos.Y * scale, 0f);
                    _uvs[i] = new Vector2(vertexPtr[i].uv.X, vertexPtr[i].uv.Y);
                    uint color = vertexPtr[i].col;
                    _colors[i] = new Color32((byte)color, (byte)(color >> 8), (byte)(color >> 16), (byte)(color >> 24));
                }

                ushort* indexPtr = (ushort*)list.IdxBuffer.Data;
                for (int i = 0; i < indexCount; i++) _indices[i] = indexPtr[i];

                for (int commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
                {
                    ImDrawCmdPtr command = list.CmdBuffer[commandIndex];
                    if (command.UserCallback != IntPtr.Zero) continue;
                    EnsureRenderer(renderedCommands);

                    int elementCount = (int)command.ElemCount;
                    if (_commandIndices.Length < elementCount) _commandIndices = new int[elementCount];
                    int vertexOffset = (int)command.VtxOffset;
                    for (int i = 0; i < elementCount; i++)
                    {
                        _commandIndices[i] = _indices[(int)command.IdxOffset + i] + vertexOffset;
                    }

                    Mesh mesh = _meshes[renderedCommands];
                    mesh.Clear();
                    mesh.SetVertices(_vertices, 0, vertexCount);
                    mesh.SetUVs(0, _uvs, 0, vertexCount);
                    mesh.SetColors(_colors, 0, vertexCount);
                    mesh.SetIndices(_commandIndices, 0, elementCount, MeshTopology.Triangles, 0);

                    CanvasRenderer renderer = _renderers[renderedCommands];
                    float clipX = offsetX + command.ClipRect.X * scale;
                    float clipY = offsetY - command.ClipRect.W * scale;
                    float clipW = (command.ClipRect.Z - command.ClipRect.X) * scale;
                    float clipH = (command.ClipRect.W - command.ClipRect.Y) * scale;
                    renderer.EnableRectClipping(new Rect(clipX, clipY, clipW, clipH));
                    renderer.SetMaterial(_material, _fontTexture);
                    renderer.SetColor(Color.white);
                    renderer.SetMesh(mesh);
                    renderedCommands++;
                }
            }

            for (int i = renderedCommands; i < _renderers.Count; i++) _renderers[i].Clear();
        }

        private void EnsureArrayCapacity(int vertexCount, int indexCount)
        {
            if (_vertices.Length < vertexCount)
            {
                _vertices = new Vector3[vertexCount];
                _uvs = new Vector2[vertexCount];
                _colors = new Color32[vertexCount];
            }
            if (_indices.Length < indexCount) _indices = new int[indexCount];
        }

        private void EnsureRenderer(int index)
        {
            if (index < _renderers.Count) return;
            GameObject child = new GameObject("ImGui_Command_" + index, typeof(RectTransform));
            child.transform.SetParent(_canvas.transform, false);
            _renderers.Add(child.AddComponent<CanvasRenderer>());
            Mesh mesh = new Mesh { name = "CheryFramework_ImGuiMesh_" + index };
            mesh.MarkDynamic();
            _meshes.Add(mesh);
        }

        private void ClearCanvas()
        {
            if (!_canvasHasContent) return;
            for (int i = 0; i < _renderers.Count; i++) _renderers[i].Clear();
            _canvasHasContent = false;
        }

        private void OnDestroy()
        {
            if (_context != IntPtr.Zero)
            {
                ImGui.SetCurrentContext(_context);
                ImGui.DestroyContext(_context);
                _context = IntPtr.Zero;
            }
            if (_fontTexture != null) Destroy(_fontTexture);
            if (_material != null) Destroy(_material);
            if (_canvas != null) Destroy(_canvas.gameObject);
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) Destroy(_meshes[i]);
        }
    }
}
