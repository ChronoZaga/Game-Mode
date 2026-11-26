@echo off
setlocal

rem ===== Version number for GameMode.exe (edit this) =====
set "VERSION=2.0.0.0"
rem =======================================================

rem == EXE + Game Mode button icon, from Windows (Xbox) ===
set "ICON_DLL=%SystemRoot%\System32\DDORes.dll"
set "ICON_ID=2207"
rem =======================================================

rem ====== Desktop Mode button icon (Desktop), from Windows ======
set "DESK_DLL=%SystemRoot%\System32\imageres.dll"
set "DESK_ID=110"
rem =======================================================

rem ====== Settings button icon (gear), from Windows ======
set "GEAR_DLL=%SystemRoot%\System32\shell32.dll"
set "GEAR_ID=16826"
rem =======================================================

cd /d "%~dp0"

rem Use the C# compiler built into Windows (.NET Framework 4.x, 64-bit) - no Visual Studio needed
set "REF=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319"
set "CSC=%REF%\csc.exe"
if not exist "%CSC%" (
    echo Built-in C# compiler not found: %CSC%
    goto :fail
)
if not exist "%REF%\mscorlib.dll" (
    echo .NET Framework 4 assemblies not found: %REF%
    goto :fail
)

for %%f in (GameMode.cs gamemode.wav) do (
    if not exist "%%f" (
        echo Missing file: %%f
        goto :fail
    )
)

rem Extract the icons from Windows (script is at the end of this file)
set "ICON_TMP=%TEMP%\GameMode_Icon_%RANDOM%.ico"
set "DESK_TMP=%TEMP%\GameMode_Desktop_%RANDOM%.ico"
set "GEAR_TMP=%TEMP%\GameMode_Gear_%RANDOM%.ico"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=[IO.File]::ReadAllText('%~f0') -split '(?m)^:ICON_SCRIPT\r?\n',2; iex $s[1]"
if errorlevel 1 (
    echo Could not extract icons from Windows.
    del "%ICON_TMP%" "%DESK_TMP%" "%GEAR_TMP%" >nul 2>&1
    goto :fail
)

rem Generate version attributes
set "VERFILE=%TEMP%\GameMode_Version_%RANDOM%.cs"
> "%VERFILE%" echo [assembly: System.Reflection.AssemblyVersion("%VERSION%")]
>> "%VERFILE%" echo [assembly: System.Reflection.AssemblyFileVersion("%VERSION%")]

echo Building GameMode.exe v%VERSION% (x64)...
"%CSC%" /nologo /noconfig /nostdlib+ /target:winexe /platform:x64 /optimize+ /warn:4 /define:TRACE /debug- ^
 "/win32icon:%ICON_TMP%" ^
 "/resource:%ICON_TMP%,Game_Mode.game.ico" ^
 "/resource:%DESK_TMP%,Game_Mode.desktop.ico" ^
 "/resource:%GEAR_TMP%,Game_Mode.settings.ico" ^
 /resource:gamemode.wav,Game_Mode.gamemode.wav ^
 "/r:%REF%\mscorlib.dll" "/r:%REF%\System.dll" "/r:%REF%\System.Core.dll" "/r:%REF%\System.Drawing.dll" ^
 "/r:%REF%\System.Windows.Forms.dll" "/r:%REF%\System.Management.dll" "/r:%REF%\Microsoft.CSharp.dll" ^
 /out:GameMode.exe GameMode.cs "%VERFILE%"
set "ERR=%ERRORLEVEL%"
del "%VERFILE%" >nul 2>&1
del "%ICON_TMP%" "%DESK_TMP%" "%GEAR_TMP%" >nul 2>&1
if not "%ERR%"=="0" goto :fail

rem Verify the PE header is x64 (AMD64 = 0x8664)
powershell -NoProfile -Command "$b=[IO.File]::ReadAllBytes('GameMode.exe');$o=[BitConverter]::ToInt32($b,0x3C);if([BitConverter]::ToUInt16($b,$o+4) -ne 0x8664){exit 1}"
if errorlevel 1 (
    echo Build is NOT x64.
    goto :fail
)

echo Done: GameMode.exe v%VERSION% (x64)
pause
exit /b 0

:fail
echo Build failed.
pause
exit /b 1

rem PowerShell below: copies each icon group from its DLL into a temp .ico file
:ICON_SCRIPT
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class IconGrab
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryEx(string f, IntPtr h, uint flags);
    [DllImport("kernel32")] static extern bool FreeLibrary(IntPtr h);
    [DllImport("kernel32")] static extern IntPtr FindResource(IntPtr h, IntPtr name, IntPtr type);
    [DllImport("kernel32")] static extern IntPtr LoadResource(IntPtr h, IntPtr r);
    [DllImport("kernel32")] static extern IntPtr LockResource(IntPtr r);
    [DllImport("kernel32")] static extern uint SizeofResource(IntPtr h, IntPtr r);

    static byte[] Res(IntPtr h, int id, int type)
    {
        IntPtr r = FindResource(h, (IntPtr)id, (IntPtr)type);
        if (r == IntPtr.Zero) throw new Exception("Resource " + id + " not found");
        int n = (int)SizeofResource(h, r);
        byte[] b = new byte[n];
        Marshal.Copy(LockResource(LoadResource(h, r)), b, 0, n);
        return b;
    }

    public static void Save(string dll, int id, string outPath)
    {
        IntPtr h = LoadLibraryEx(dll, IntPtr.Zero, 0x22); // AS_DATAFILE | AS_IMAGE_RESOURCE
        if (h == IntPtr.Zero) throw new Exception("Cannot load " + dll);
        try
        {
            byte[] grp = Res(h, id, 14); // RT_GROUP_ICON
            int count = BitConverter.ToUInt16(grp, 4);
            byte[][] imgs = new byte[count][];
            for (int i = 0; i < count; i++)
                imgs[i] = Res(h, BitConverter.ToUInt16(grp, 6 + i * 14 + 12), 3); // RT_ICON
            using (var w = new BinaryWriter(File.Create(outPath)))
            {
                w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)count);
                int offset = 6 + 16 * count;
                for (int i = 0; i < count; i++)
                {
                    w.Write(grp, 6 + i * 14, 8);
                    w.Write((uint)imgs[i].Length);
                    w.Write((uint)offset);
                    offset += imgs[i].Length;
                }
                foreach (byte[] img in imgs) w.Write(img);
            }
        }
        finally { FreeLibrary(h); }
    }
}
'@
[IconGrab]::Save($env:ICON_DLL, [int]$env:ICON_ID, $env:ICON_TMP)
[IconGrab]::Save($env:DESK_DLL, [int]$env:DESK_ID, $env:DESK_TMP)
[IconGrab]::Save($env:GEAR_DLL, [int]$env:GEAR_ID, $env:GEAR_TMP)
