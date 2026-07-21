# CheryFramework

一个基于 Dear ImGui 的 ADOFAI Mod UI 初始框架，包含顶部菜单栏、独立功能窗口、MiSans 中文字体和基础输入支持。

框架只提供最基础的 UI 与 Mod 生命周期，不包含具体业务功能，也不会自动部署到游戏目录。

## 快速使用

主要文件：

```text
Mod\src\Main.cs             UMM 加载、启用和设置入口
Mod\src\FrameworkPanel.cs   顶部栏与功能窗口
Mod\src\ImGuiController.cs  ImGui 输入和 Unity 渲染
Mod\Info.json               Mod 名称、版本和入口信息
```

开发新功能时，主要修改 `Mod\src\FrameworkPanel.cs`：

1. 在顶部栏中加入功能按钮。
2. 为功能创建对应的显示状态。
3. 在独立 ImGui 窗口中编写功能界面。
4. 按 `Insert` 呼出或隐藏整个 Mod UI。

需要修改 Mod 名称、作者或版本时，编辑 `Mod\Info.json`；修改程序集名称或入口命名空间时，还需要同步修改 `Mod\CheryFramework.csproj` 和相关 C# 文件。

## 构建

构建前确认已安装 .NET SDK，并检查 `Mod\CheryFramework.csproj` 中的 `GameExePath` 是否指向本机的游戏 EXE。

在 PowerShell 中运行：

```powershell
cd E:\ADOFAI-MOD\CheryTools-main\CheryFramework
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建产物：

```text
artifacts\Mod                       UMM Mod 文件
artifacts\CheryFramework-UMM.zip   UMM 可用压缩包
Preview\exe\CheryFramework.Preview.exe
```

框架不会自动复制到游戏的 `Mods` 目录。需要进游戏测试时，请手动安装 `artifacts\Mod` 或解压 `artifacts\CheryFramework-UMM.zip`。

## 查看初始模板

直接运行：

```text
Preview\exe\CheryFramework.Preview.exe
```

启动后按 `Insert`，即可查看初始顶部栏和示例功能窗口的实际效果。

## 开源协议

本项目采用 [GNU General Public License v3.0](LICENSE)。发布 CheryFramework 的修改版或衍生作品时，需要依照 GPL-3.0 提供对应源代码并保留相同协议。
