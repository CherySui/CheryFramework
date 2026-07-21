using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityModManagerNet;

namespace CheryFramework
{
    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static UnityModManager.ModEntry.ModLogger Logger { get; private set; }
        public static Settings Settings { get; private set; }
        public static bool IsEnabled { get; private set; }

        private static GameObject _host;
        private static IntPtr _cimguiHandle = IntPtr.Zero;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            Logger = modEntry.Logger;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry) ?? new Settings();
            Settings.Normalize();
            LoadNativeImGui(modEntry.Path);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            IsEnabled = value;
            if (value)
            {
                if (_host == null)
                {
                    _host = new GameObject("CheryFramework_ImGui");
                    UnityEngine.Object.DontDestroyOnLoad(_host);
                    _host.AddComponent<FrameworkPanel>();
                    _host.AddComponent<ImGuiController>();
                }
            }
            else if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("CheryFramework：最小 Dear ImGui Mod UI 框架");
            GUILayout.Label("按 Insert 显示或隐藏顶部栏与全部功能窗口。");

            GUILayout.BeginHorizontal();
            GUILayout.Label("界面缩放", GUILayout.Width(110f));
            Settings.UiScale = GUILayout.HorizontalSlider(Settings.UiScale, 0.75f, 1.75f, GUILayout.Width(260f));
            GUILayout.Label(Settings.UiScale.ToString("0.00"), GUILayout.Width(45f));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("打开 / 关闭 Mod UI", GUILayout.Width(240f)))
            {
                FrameworkPanel.IsOpen = !FrameworkPanel.IsOpen;
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }

        private static void LoadNativeImGui(string modPath)
        {
            if (_cimguiHandle != IntPtr.Zero) return;
            string path = System.IO.Path.Combine(modPath, "cimgui.dll");
            if (!System.IO.File.Exists(path))
            {
                Logger.Log("cimgui.dll 不存在：" + path);
                return;
            }

            _cimguiHandle = LoadLibrary(path);
            if (_cimguiHandle == IntPtr.Zero)
            {
                Logger.Log("加载 cimgui.dll 失败，Win32Error=" + Marshal.GetLastWin32Error());
            }
        }
    }
}
