using MioGame;
using MioModdingApi;
using MioModLoader;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using static On.MioGame.On_Stream;

namespace InteractiveMapMod
{
    public class InteractiveMapMod : Mod
    {
        public Dictionary<dynamic, int> GameKeyConvert = new Dictionary<dynamic, int>
        {
            [Key.Key_dummy] = 0,
            [Key.Key_0] = 48,
            [Key.Key_1] = 49,
            [Key.Key_2] = 50,
            [Key.Key_3] = 51,
            [Key.Key_4] = 52,
            [Key.Key_5] = 53,
            [Key.Key_6] = 54,
            [Key.Key_7] = 55,
            [Key.Key_8] = 56,
            [Key.Key_9] = 57,
            [Key.Key_A] = 65,
            [Key.Key_B] = 66,
            [Key.Key_C] = 67,
            [Key.Key_D] = 68,
            [Key.Key_E] = 69,
            [Key.Key_F] = 70,
            [Key.Key_G] = 71,
            [Key.Key_H] = 72,
            [Key.Key_I] = 73,
            [Key.Key_J] = 74,
            [Key.Key_K] = 75,
            [Key.Key_L] = 76,
            [Key.Key_M] = 77,
            [Key.Key_N] = 78,
            [Key.Key_O] = 79,
            [Key.Key_P] = 80,
            [Key.Key_Q] = 81,
            [Key.Key_R] = 82,
            [Key.Key_S] = 83,
            [Key.Key_T] = 84,
            [Key.Key_U] = 85,
            [Key.Key_V] = 86,
            [Key.Key_W] = 87,
            [Key.Key_X] = 88,
            [Key.Key_Y] = 89,
            [Key.Key_Z] = 90,
        };

        private const int VK_CONTROL = 0x11;
        private const int VK_M = 0x4D;
        private const int VK_MENU = 0x12;

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        public override void Initialize()
        {
            Hooks();

            TriggerRequests();

            ConnectMenus();
        }
        public void TriggerRequests()
        {
            _ = Task.Run(MakeRequests);
        }

        private static ModMenuPage? modPage;
        private static ToggleOption? needCtrlMod;
        private static ToggleOption? needCtrlMap;
        private static ToggleOption? keybindNeedsFocus;
        private static KeybindOption? keyToOpen;
        public void ConnectMenus()
        {
            modPage = ModMenu.RegisterMod(this, "Interactive Map Mod", "Mod for the Interactive Map");
            modPage.RegisterButton("open_map", "Open Map", ToggleMap);
            needCtrlMod = modPage.RegisterToggle("need_ctrl_mod", "Need CTRL to open", true, "Whether you need to hold control to use the open hotkey, when opening the map.");
            needCtrlMap = modPage.RegisterToggle("need_ctrl_map", "Need CTRL to close", true, "Whether you need to hold control to use the open hotkey, when closing the map.");
            keybindNeedsFocus = modPage.RegisterToggle("keybind_needs_focus", "Need focus to toggle", true, "If either the map or the game needs to be the focused window in order to use the switch hotkey.");
            keyToOpen = modPage.RegisterKeybind("open_key", "Open Keybind", Key.Key_M, Gamepad_button.Btn_guide, description: "The keybind to open the map, used in conjunction with CTRL. Please only put letter and number keys here.");
        }

        private unsafe void Hooks()
        {
            On.MioGame.On_Game.fixed_update.Prefix += Fixed_update_Prefix;
        }

        private unsafe void Fixed_update_Prefix(MioGame.Game* __this)
        {
            try
            {
                PollOpen();
            }
            catch (Exception ex)
            {
                LogMessage("Could not poll map opening: " + ex);
            }
        }


        private bool mapInstalled = MapInstalled();
        private bool mapOpen = false;
        private bool mapActive = false;

        private bool waitingOnInstall = false;

        private bool ctrlPressed = false;
        private bool mPressed = false;

        private void PollOpen()
        {
            bool bothPressedLastFrame = ctrlPressed & mPressed;
            bool mPressedLastFrame = mPressed;

            ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            if (GameKeyConvert.ContainsKey(keyToOpen.Key))
            {
                mPressed = (GetAsyncKeyState(GameKeyConvert[keyToOpen.Key]) & 0x8000) != 0;
            }
            else
            {
                mPressed = (GetAsyncKeyState(VK_M) & 0x8000) != 0;
            }
            bool bothPressed = ctrlPressed && mPressed;

            if (mapOpen ? !needCtrlMap.Value : !needCtrlMod.Value)
            {
                if (mPressedLastFrame) return;
                if (!mPressed) return;
            }
            else
            {
                if (bothPressedLastFrame) return;
                if (!bothPressed) return;
            }

            if (keybindNeedsFocus.Value && !AcceptingInput()) return;

            ToggleMap();
        }

        private void ToggleMap()
        {
            if (!mapOpen)
            {
                if (!mapInstalled)
                {
                    _ = InstallMap();
                    return;
                }
                LaunchMap();
            }
            else if (mapActive)
            {
                SwitchFromMap();
            }
            else
            {
                SwitchToMap();
            }
        }


        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        public static readonly IntPtr HWND_TOP_FLAG = new IntPtr(-1);
        public static readonly IntPtr HWND_TOP_NO_FLAG = new IntPtr(-2);


        private Process? GetMapProcess()
        {
            try
            {
                return Process.GetProcessesByName("WindowsMioIM")[0];
            }
            catch (Exception ex)
            {
                LogMessage("Could not find map executable: " + ex);
                return null;
            }
        }

        private void SwitchToMap()
        {
            Process? mapProcess = GetMapProcess();

            if (mapProcess == null) return;
            IntPtr mapHWnd = mapProcess.MainWindowHandle;
            SetForegroundWindow(mapHWnd);

            mapActive = true;
        }
   
        private void SwitchFromMap()
        {
            IntPtr currentHWnd = Process.GetCurrentProcess().MainWindowHandle;

            if (currentHWnd != 0)
            {
                SetWindowPos(currentHWnd, HWND_TOP_FLAG, 0, 0, 0, 0, 0x0003);
                SetWindowPos(currentHWnd, HWND_TOP_NO_FLAG, 0, 0, 0, 0, 0x0003);
                keybd_event(VK_MENU, 0, 0, 0);
                SetForegroundWindow(currentHWnd);
                keybd_event(VK_MENU, 0, 0x0002, 0);

                mapActive = false;
            }
        }

        private static readonly string MOD_BASE_DIR = @".\mods\InteractiveMapMod\";
        private static readonly string MAP_DIR_PATH = Path.Combine(MOD_BASE_DIR, @"WindowsMioIM\");
        private static readonly string MAP_PATH = Path.Combine(MAP_DIR_PATH, "WindowsMioIM.exe");
        private static readonly ProcessStartInfo MAP_START_INFO = new ProcessStartInfo
        {
            FileName = MAP_PATH,
            WorkingDirectory = MAP_DIR_PATH,
            WindowStyle = ProcessWindowStyle.Maximized
        };

        private void LaunchMap()
        {
            try
            {
                Process.Start(MAP_START_INFO);
            }
            catch (Exception ex)
            {
                LogMessage("Failed to open map: " + ex);
            }
            

            mapOpen = true;
            mapActive = true;
        }

        private static readonly HttpClient requester = new HttpClient();
        private const string MAP_REPONAME = "MIO-Modding/interactive-map";
        private const string MAP_RELEASES = $"https://api.github.com/repos/{MAP_REPONAME}/releases?per_page=100";
        private const string MAP_GITHUB = $"https://github.com/{MAP_REPONAME}/";
        private const string MAP_DOWNLOAD = MAP_GITHUB + "releases/latest/download/WindowsMioIM.zip";
        private static string mapLatestRelease = "0.0.0";


        private async Task MakeRequests()
        {
            requester.DefaultRequestHeaders.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("MioIMMod", "0.0.1")
            );

            string dataString = "";
            try { dataString = System.Text.Encoding.UTF8.GetString(await requester.GetByteArrayAsync(MAP_RELEASES)); }
            catch (Exception ex) { LogMessage("Could not request release data: " + ex); }

            Dictionary<string, dynamic>[]? data = { };
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, dynamic>[]>(dataString);
            }
            catch (Exception ex) { LogMessage("Could not parse release data: " + ex); }

            if (data != null)
            {
                try
                {
                    Dictionary<string, dynamic> latestReleaseData = data[0];
                    mapLatestRelease = latestReleaseData["html_url"].GetString().Split("/tag/")[1];
                }
                catch (Exception ex) { LogMessage("Could not get tag from release data: " + ex); }
                //LogMessage("Latest release: " + mapLatestRelease);
            }

            if (!MapInstalled(true)) await InstallMap();
        }

        private async Task InstallMap()
        {
            if (waitingOnInstall) return;
            waitingOnInstall = true;

            byte[] bytes = [];

            try
            {
                requester.Timeout = TimeSpan.FromSeconds(500);
                bytes = await requester.GetByteArrayAsync(MAP_DOWNLOAD);
            }
            catch (Exception ex)
            {
                LogMessage("Failed to request map zip: " + ex);
            }

            try
            {
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    using (ZipArchive zArchive = new ZipArchive(ms, ZipArchiveMode.Read))
                    {
                        if (!Directory.Exists(MAP_DIR_PATH)) Directory.CreateDirectory(MAP_DIR_PATH);

                        zArchive.ExtractToDirectory(MAP_DIR_PATH, true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage("Could not unzip map: " + ex);
            }

            mapInstalled = true;
            waitingOnInstall = false;
        }


        private static bool MapInstalled(bool mustMatchVersion = false)
        {
            if (Directory.Exists(MAP_DIR_PATH))
            {
                if (mustMatchVersion)
                {
                    Debug.Assert(System.IO.File.Exists(MAP_PATH));
                    string? version = System.Diagnostics.FileVersionInfo.GetVersionInfo(MAP_PATH).FileVersion;
                    if (version == null) return false;
                    return version[..^2] == mapLatestRelease;
                }
                return true;
            }
            return false;
        }


        private bool AcceptingInput()
        {
            IntPtr current = GetForegroundWindow();
            Process? mapProcess = GetMapProcess();
            if (mapProcess == null) return IsWindowSelf(current);
            return IsWindowSelf(current) || current == mapProcess.MainWindowHandle;
        }

        private bool IsWindowSelf(IntPtr handle)
        {
            return handle == Process.GetCurrentProcess().MainWindowHandle;
        }
    }
}
