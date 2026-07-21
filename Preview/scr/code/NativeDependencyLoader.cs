using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CheryFramework.Preview;

/// <summary>
/// Makes Hexa.NET's native libraries available when the preview is published
/// as one executable. HexaGen loads these libraries by name, so relying only
/// on the .NET single-file extraction directory is not sufficient.
/// </summary>
internal sealed class NativeDependencyLoader : IDisposable
{
    private const string CImGuiResource = "CheryFramework.Preview.Native.cimgui.dll";
    private const string ImGuiImplResource = "CheryFramework.Preview.Native.ImGuiImpl.dll";

    private readonly nint _cimguiHandle;
    private readonly nint _imguiImplHandle;
    private bool _disposed;

    private NativeDependencyLoader(nint cimguiHandle, nint imguiImplHandle)
    {
        _cimguiHandle = cimguiHandle;
        _imguiImplHandle = imguiImplHandle;
    }

    public static NativeDependencyLoader Load()
    {
        byte[] cimgui = ReadResource(CImGuiResource);
        byte[] imguiImpl = ReadResource(ImGuiImplResource);
        string versionKey = ComputeVersionKey(cimgui, imguiImpl);
        string directory = Path.Combine(Path.GetTempPath(), "CheryFramework.Preview.Native", versionKey);
        Directory.CreateDirectory(directory);

        string cimguiPath = WriteOnce(directory, "cimgui.dll", cimgui);
        string imguiImplPath = WriteOnce(directory, "ImGuiImpl.dll", imguiImpl);

        if (!SetDllDirectory(directory))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Unable to register the preview native-library directory.");
        }

        nint cimguiHandle = NativeLibrary.Load(cimguiPath);
        try
        {
            nint imguiImplHandle = NativeLibrary.Load(imguiImplPath);
            return new NativeDependencyLoader(cimguiHandle, imguiImplHandle);
        }
        catch
        {
            NativeLibrary.Free(cimguiHandle);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeLibrary.Free(_imguiImplHandle);
        NativeLibrary.Free(_cimguiHandle);
        SetDllDirectory(null);
    }

    private static byte[] ReadResource(string name)
    {
        Assembly assembly = typeof(NativeDependencyLoader).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded native library was not found: {name}");
        using MemoryStream output = new();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static string ComputeVersionKey(byte[] first, byte[] second)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(first);
        hash.AppendData(second);
        return Convert.ToHexString(hash.GetHashAndReset()).Substring(0, 16);
    }

    private static string WriteOnce(string directory, string fileName, byte[] bytes)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
        {
            string temporaryPath = path + "." + Environment.ProcessId + ".tmp";
            File.WriteAllBytes(temporaryPath, bytes);
            try
            {
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        return path;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string? pathName);
}
