using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace DevilSpireLauncher;

public class MainForm : Form
{
    private CheckedListBox lstMods;
    private Button btnLaunch;
    private Button btnRefresh;
    private Button btnMoveUp;
    private Button btnMoveDown;
    private Button btnCreateMod;
    private Button btnOpenModFolder;
    private Label lblStatus;
    private RichTextBox txtLogs;
    private TextBox txtModDetails;
    private string gamePath = "";
    private string defaultSteamPath = @"C:\Steam\steamapps\common\Devil Spire Falls";
    private Dictionary<string, string> modDetailsMap = new Dictionary<string, string>();

    public MainForm()
    {
        InitializeComponent();
        DetectGamePath();
        Log("Launcher started (DLL Injection Mode).");
        CheckGamePath();
        ScanMods();
    }

    private void DetectGamePath()
    {
        // 1. Check current directory
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(currentDir, "Devil Spire Falls.exe")))
        {
            gamePath = currentDir;
            return;
        }

        // 2. Fallback to default steam path
        gamePath = defaultSteamPath;
    }

    private void InitializeComponent()
    {
        this.Text = "Devil Spire Extender (DSE) Launcher";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterScreen;

        Label lblTitle = new Label() { Text = "Installed Mods (Check to enable)", Location = new Point(10, 10), AutoSize = true };
        lstMods = new CheckedListBox() { Location = new Point(10, 30), Size = new Size(380, 200), CheckOnClick = true };
        lstMods.SelectedIndexChanged += LstMods_SelectedIndexChanged;
        lstMods.ItemCheck += (s, e) => {
            if (this.IsHandleCreated)
                BeginInvoke(new Action(SaveLoadOrder));
        };
        
        Label lblDetails = new Label() { Text = "Mod Details", Location = new Point(400, 10), AutoSize = true };
        txtModDetails = new TextBox() { Location = new Point(400, 30), Size = new Size(370, 200), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

        Label lblLogs = new Label() { Text = "Launcher Logs", Location = new Point(10, 270), AutoSize = true };
        txtLogs = new RichTextBox() { Location = new Point(10, 290), Size = new Size(760, 210), ReadOnly = true, BackColor = Color.Black, ForeColor = Color.LightGreen };

        btnRefresh = new Button() { Text = "Refresh", Location = new Point(10, 235), Size = new Size(80, 30) };
        btnRefresh.Click += (s, e) => ScanMods();

        btnMoveUp = new Button() { Text = "↑ Up", Location = new Point(100, 235), Size = new Size(60, 30) };
        btnMoveUp.Click += BtnMoveUp_Click;

        btnMoveDown = new Button() { Text = "↓ Down", Location = new Point(170, 235), Size = new Size(60, 30) };
        btnMoveDown.Click += BtnMoveDown_Click;

        btnLaunch = new Button() { Text = "Launch Game", Location = new Point(670, 510), Size = new Size(100, 40), Font = new Font(this.Font, FontStyle.Bold) };
        btnLaunch.Click += BtnLaunch_Click;

        btnCreateMod = new Button() { Text = "Create Mod", Location = new Point(560, 510), Size = new Size(100, 40) };
        btnCreateMod.Click += BtnCreateMod_Click;

        btnOpenModFolder = new Button() { Text = "Mod Folder", Location = new Point(450, 510), Size = new Size(100, 40) };
        btnOpenModFolder.Click += BtnOpenModFolder_Click;

        lblStatus = new Label() { Text = "Status: Ready", Location = new Point(10, 520), AutoSize = true };

        this.Controls.Add(lblTitle);
        this.Controls.Add(lstMods);
        this.Controls.Add(lblDetails);
        this.Controls.Add(txtModDetails);
        this.Controls.Add(btnRefresh);
        this.Controls.Add(btnMoveUp);
        this.Controls.Add(btnMoveDown);
        this.Controls.Add(btnCreateMod);
        this.Controls.Add(btnOpenModFolder);
        this.Controls.Add(lblLogs);
        this.Controls.Add(txtLogs);
        this.Controls.Add(btnLaunch);
        this.Controls.Add(lblStatus);
    }

    private void Log(string message)
    {
        if (txtLogs.InvokeRequired)
        {
            txtLogs.Invoke(new Action(() => Log(message)));
            return;
        }
        string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}";
        txtLogs.AppendText(logLine + "\n");
        txtLogs.ScrollToCaret();

        // Also save to file
        try
        {
            if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
            {
                File.AppendAllText(Path.Combine(gamePath, "dse_launcher.log"), logLine + Environment.NewLine);
            }
        }
        catch { }
    }

    private void CheckGamePath()
    {
        if (!Directory.Exists(gamePath))
        {
            Log("ERROR: Game directory not found at " + gamePath);
            lblStatus.Text = "Status: Game directory not found!";
            lblStatus.ForeColor = Color.Red;
            btnLaunch.Enabled = false;
        }
        else
        {
            Log("Game directory found: " + gamePath);
        }
    }

    private void BtnMoveUp_Click(object? sender, EventArgs e)
    {
        int index = lstMods.SelectedIndex;
        if (index > 0)
        {
            object item = lstMods.Items[index];
            bool isChecked = lstMods.GetItemChecked(index);
            lstMods.Items.RemoveAt(index);
            lstMods.Items.Insert(index - 1, item);
            lstMods.SetItemChecked(index - 1, isChecked);
            lstMods.SelectedIndex = index - 1;
            SaveLoadOrder();
        }
    }

    private void BtnMoveDown_Click(object? sender, EventArgs e)
    {
        int index = lstMods.SelectedIndex;
        if (index != -1 && index < lstMods.Items.Count - 1)
        {
            object item = lstMods.Items[index];
            bool isChecked = lstMods.GetItemChecked(index);
            lstMods.Items.RemoveAt(index);
            lstMods.Items.Insert(index + 1, item);
            lstMods.SetItemChecked(index + 1, isChecked);
            lstMods.SelectedIndex = index + 1;
            SaveLoadOrder();
        }
    }

    private void SaveLoadOrder()
    {
        try
        {
            string loadOrderPath = Path.Combine(gamePath, "load_order.txt");
            List<string> enabledMods = new List<string>();
            foreach (var item in lstMods.CheckedItems)
            {
                enabledMods.Add(item.ToString()!);
            }
            
            File.WriteAllLines(loadOrderPath, enabledMods);
            
            // Also save the full list and their checked state to a separate file for the launcher
            string launcherStatePath = Path.Combine(gamePath, "dse_state.txt");
            List<string> stateLines = new List<string>();
            for (int i = 0; i < lstMods.Items.Count; i++)
            {
                string modName = lstMods.Items[i].ToString()!;
                bool isChecked = lstMods.GetItemChecked(i);
                stateLines.Add($"{modName}|{isChecked}");
            }
            File.WriteAllLines(launcherStatePath, stateLines);
            
            Log("Load order and launcher state saved.");
        }
        catch (Exception ex)
        {
            Log("Error saving load order: " + ex.Message);
        }
    }

    private void ScanMods()
    {
        string modsDir = Path.Combine(gamePath, "mods");
        if (!Directory.Exists(modsDir))
        {
            Log("Creating mods directory...");
            Directory.CreateDirectory(modsDir);
        }

        Log("Scanning for mods...");
        
        // Load existing state if available
        string launcherStatePath = Path.Combine(gamePath, "dse_state.txt");
        Dictionary<string, bool> modState = new Dictionary<string, bool>();
        List<string> orderedMods = new List<string>();
        
        if (File.Exists(launcherStatePath))
        {
            foreach (var line in File.ReadAllLines(launcherStatePath))
            {
                var parts = line.Split('|');
                if (parts.Length == 2)
                {
                    modState[parts[0]] = bool.Parse(parts[1]);
                    orderedMods.Add(parts[0]);
                }
            }
        }

        lstMods.Items.Clear();
        modDetailsMap.Clear();
        
        List<string> foundMods = new List<string>();

        // Scan for .pck files
        if (Directory.Exists(modsDir))
        {
            string[] modFiles = Directory.GetFiles(modsDir, "*.pck");
            foreach (var mod in modFiles)
            {
                string modName = Path.GetFileName(mod);
                foundMods.Add(modName);
                modDetailsMap[modName] = $"Type: PCK Mod\r\nFile: {modName}\r\nPath: {mod}";
            }

            // Scan for folder-based mods with mod.json
            string[] modDirs = Directory.GetDirectories(modsDir);
            foreach (var dir in modDirs)
            {
                string jsonPath = Path.Combine(dir, "mod.json");
                string modName = Path.GetFileName(dir);
                if (File.Exists(jsonPath))
                {
                    foundMods.Add(modName);
                    try 
                    {
                        string jsonContent = File.ReadAllText(jsonPath);
                        modDetailsMap[modName] = $"Type: Folder Mod\r\nFolder: {modName}\r\nMetadata:\r\n{jsonContent}";
                    }
                    catch (Exception ex)
                    {
                        modDetailsMap[modName] = $"Type: Folder Mod\r\nError reading mod.json: {ex.Message}";
                    }
                }
            }
        }

        // Add mods in stored order first
        foreach (var mod in orderedMods)
        {
            if (foundMods.Contains(mod))
            {
                lstMods.Items.Add(mod, modState.ContainsKey(mod) && modState[mod]);
                foundMods.Remove(mod);
            }
        }

        // Add any new mods found (default to disabled)
        foreach (var mod in foundMods)
        {
            lstMods.Items.Add(mod, false);
            Log($"New mod detected: {mod}");
        }
        
        if (lstMods.Items.Count == 0)
        {
            Log("No mods detected.");
        }
        else
        {
            Log($"Total mods found: {lstMods.Items.Count}");
            SaveLoadOrder(); // Sync file
        }
    }

    private void LstMods_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (lstMods.SelectedItem != null)
        {
            string modName = lstMods.SelectedItem.ToString() ?? "";
            if (modDetailsMap.ContainsKey(modName))
            {
                txtModDetails.Text = modDetailsMap[modName];
            }
            else
            {
                txtModDetails.Text = "No details available for this item.";
            }
        }
    }

    private void BtnOpenModFolder_Click(object? sender, EventArgs e)
    {
        string modsDir = Path.Combine(gamePath, "mods");
        if (lstMods.SelectedItem != null)
        {
            string modName = lstMods.SelectedItem.ToString()!;
            string modPath = Path.Combine(modsDir, modName);
            if (Directory.Exists(modPath))
            {
                Process.Start("explorer.exe", modPath);
                return;
            }
        }
        
        if (Directory.Exists(modsDir))
        {
            Process.Start("explorer.exe", modsDir);
        }
        else
        {
            Log("Mods folder not found.");
        }
    }

    private void BtnCreateMod_Click(object? sender, EventArgs e)
    {
        string modsDir = Path.Combine(gamePath, "mods");
        string starterModDir = Path.Combine(modsDir, "StarterMod");
        
        try
        {
            if (Directory.Exists(starterModDir))
            {
                Log("StarterMod already exists.");
                return;
            }

            Directory.CreateDirectory(starterModDir);
            
            string modJson = "{\n  \"id\": \"com.dse.startermod\",\n  \"name\": \"Starter Mod\",\n  \"version\": \"1.0.0\",\n  \"author\": \"DSE Team\",\n  \"description\": \"A simple GDScript-first mod template for Devil Spire.\"\n}";
            File.WriteAllText(Path.Combine(starterModDir, "mod.json"), modJson);
            
            string initGd = @"extends Node

func _ready():
	var dse = get_node_or_null('/root/DSE')
	if dse:
		dse.log_info('StarterMod: GDScript-first API detected!')
		
		# Register a hook for enemy death
		dse.add_hook('enemy_died', _on_enemy_died)
		
		dse.log_info('StarterMod: Ready to track kills!')

func _on_enemy_died(enemy):
	var dse = get_node_or_null('/root/DSE')
	if dse:
		dse.log_info('StarterMod: An enemy just died! ' + str(enemy.name))
";
            File.WriteAllText(Path.Combine(starterModDir, "init.gd"), initGd);
            
            Log("StarterMod created in /mods/StarterMod");
            ScanMods();
        }
        catch (Exception ex)
        {
            Log("Error creating starter mod: " + ex.Message);
        }
    }

    private string? FindInParents(string relativePath, int maxDepth = 6)
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        while (current != null)
        {
            string candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
            
            var parent = Directory.GetParent(current);
            current = parent?.FullName;
            
            if (--maxDepth <= 0) break;
        }
        return null;
    }

    private string? FindProjectRoot()
    {
        // Search for a unique file that identifies the root
        string? path = FindInParents("devilspire.sln");
        return path != null ? Path.GetDirectoryName(path) : null;
    }

    private void PackCore()
    {
        try
        {
            string corePckPath = Path.Combine(gamePath, "DSECore.pck");
            Dictionary<string, string> files = new Dictionary<string, string>();
            
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Add API core
            files["res://System/dse.gd"] = Path.Combine(baseDir, "dse_core.gd");
            
            // Hijack Global with Bootstrap
            files["res://System/global.gd"] = Path.Combine(baseDir, "DSEBootstrap.gd");
            
            // Provide original Global for extension
            // We now have it in patches/game/System/global_orig.gd manually copied.
            // But let's keep a safety check to ensure it's packed.
            
            // Add patches
            string? patchesDir = FindInParents(Path.Combine("patches", "game"));
            if (patchesDir != null)
            {
                foreach (string file in Directory.GetFiles(patchesDir, "*.gd", SearchOption.AllDirectories))
                {
                    // Map patches to their original game paths
                    string relPath = Path.GetRelativePath(patchesDir, file).Replace("\\", "/");
                    files["res://" + relPath] = file;
                    Log($"  Patch: {relPath}");
                }
            }
            else
            {
                Log("WARNING: Patches directory not found!");
            }

            PckPacker.CreatePck(corePckPath, files);
            Log("DSECore.pck created successfully.");
        }
        catch (Exception ex)
        {
            Log("Error packing DSE Core: " + ex.Message);
        }
    }

    private void PackMods()
    {
        try
        {
            string modsDir = Path.Combine(gamePath, "mods");
            
            // Clean up old temporary PCKs first
            if (Directory.Exists(modsDir))
            {
                foreach (string file in Directory.GetFiles(modsDir, "*.pck"))
                {
                    try { File.Delete(file); } catch { }
                }
            }

            foreach (var item in lstMods.CheckedItems)
            {
                string modName = item.ToString()!;
                if (modName.EndsWith(".pck")) continue; // Already a PCK
                
                string modFolder = Path.Combine(modsDir, modName);
                if (Directory.Exists(modFolder))
                {
                    // Create a temporary PCK for the folder mod
                    string pckPath = Path.Combine(modsDir, modName + ".pck");
                    Dictionary<string, string> files = new Dictionary<string, string>();
                    foreach (string file in Directory.GetFiles(modFolder, "*", SearchOption.AllDirectories))
                    {
                        string relPath = Path.GetRelativePath(modFolder, file).Replace("\\", "/");
                        files["res://mods/" + modName + "/" + relPath] = file;
                    }
                    PckPacker.CreatePck(pckPath, files);
                    Log($"  Packed folder mod to PCK: {modName}");
                }
            }
        }
        catch (Exception ex)
        {
            Log("Error packing mods: " + ex.Message);
        }
    }

    private async void BtnLaunch_Click(object? sender, EventArgs e)
    {
        if (Process.GetProcessesByName("Devil Spire Falls").Length > 0)
        {
            MessageBox.Show("The game is already running. Please close it before launching.", "Game Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            Log("ERROR: Game path is invalid.");
            return;
        }

        string exePath = Path.Combine(gamePath, "Devil Spire Falls.exe");
        if (!File.Exists(exePath))
        {
            Log("ERROR: Executable not found at " + exePath);
            return;
        }

        string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DSELoader.dll");
        if (!File.Exists(dllPath))
        {
            Log("ERROR: DSELoader.dll not found in launcher directory!");
            MessageBox.Show("DSELoader.dll not found. Please ensure the loader DLL is in the launcher folder.", "Missing Component", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            Log("Preparing DSE Core...");
            PackCore();
            
            Log("Packing folder mods...");
            PackMods();

            Log("Launching game with DLL injection...");
            
            // 1. Launch process suspended
            var pi = Injector.LaunchSuspended(exePath, gamePath);
            if (pi.hProcess == IntPtr.Zero)
            {
                Log("ERROR: Failed to launch game process suspended.");
                return;
            }

            Log($"Game launched suspended (PID: {pi.dwProcessId})");

            // 2. Inject DLL
            bool injected = Injector.InjectDLL(pi.hProcess, dllPath);
            if (!injected)
            {
                Log("ERROR: DLL injection failed! Terminating process.");
                Injector.Terminate(pi);
                return;
            }

            Log("DLL injected successfully.");

            // 3. Resume thread
            Injector.Resume(pi);
            Log("Game resumed. Injection complete.");

            lblStatus.Text = "Status: Game running...";
            btnLaunch.Enabled = false;

            // Monitor process exit
            _ = Task.Run(async () =>
            {
                using var process = Process.GetProcessById((int)pi.dwProcessId);
                while (!process.HasExited)
                {
                    await Task.Delay(2000);
                }

                this.Invoke(new Action(() =>
                {
                    Log("Game exited.");
                    btnLaunch.Enabled = true;
                    lblStatus.Text = "Status: Ready";
                }));
                
                Injector.CloseHandles(pi);
            });
        }
        catch (Exception ex)
        {
            Log("Error during launch: " + ex.Message);
            btnLaunch.Enabled = true;
        }
    }
}

public static class Injector
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr hThread);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    public const uint CREATE_SUSPENDED = 0x00000004;
    public const uint MEM_COMMIT = 0x00001000;
    public const uint MEM_RESERVE = 0x00002000;
    public const uint PAGE_READWRITE = 0x04;

    public struct STARTUPINFO { public uint cb; public string lpReserved; public string lpDesktop; public string lpTitle; public uint dwX; public uint dwY; public uint dwXSize; public uint dwYSize; public uint dwXCountChars; public uint dwYCountChars; public uint dwFillAttribute; public uint dwFlags; public ushort wShowWindow; public ushort cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    public struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public uint dwProcessId; public uint dwThreadId; }

    public static PROCESS_INFORMATION LaunchSuspended(string exePath, string workingDir)
    {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf(si);
        PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

        bool success = CreateProcess(exePath, null, IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, workingDir, ref si, out pi);
        return success ? pi : new PROCESS_INFORMATION();
    }

    public static bool InjectDLL(IntPtr hProcess, string dllPath)
    {
        byte[] pathBytes = Encoding.ASCII.GetBytes(dllPath + "\0");
        IntPtr remoteMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        if (remoteMem == IntPtr.Zero) return false;

        if (!WriteProcessMemory(hProcess, remoteMem, pathBytes, (uint)pathBytes.Length, out _)) return false;

        IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
        IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, remoteMem, 0, IntPtr.Zero);
        
        if (hThread == IntPtr.Zero) return false;

        CloseHandle(hThread);
        return true;
    }

    public static void Resume(PROCESS_INFORMATION pi) => ResumeThread(pi.hThread);
    public static void Terminate(PROCESS_INFORMATION pi) => TerminateProcess(pi.hProcess, 0);
    public static void CloseHandles(PROCESS_INFORMATION pi) { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); }
}
