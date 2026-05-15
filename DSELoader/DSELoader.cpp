#include <windows.h>
#include <psapi.h>
#include <string>
#include <vector>
#include <fstream>
#include <filesystem>
#include <iostream>

namespace fs = std::filesystem;

// --- DSE Loader: Robust DLL Injection for Godot 4 ---

void LogMessage(const std::string& msg) {
    std::ofstream log("dse_loader.log", std::ios::app);
    log << "[" << GetTickCount() << "] " << msg << std::endl;
}

// --- Pattern Scanning ---
uintptr_t FindPattern(const char* pattern, const char* mask) {
    HMODULE hModule = GetModuleHandleA(NULL);
    if (!hModule) return 0;

    MODULEINFO mInfo;
    if (!GetModuleInformation(GetCurrentProcess(), hModule, &mInfo, sizeof(mInfo))) return 0;
    
    uintptr_t start = (uintptr_t)mInfo.lpBaseOfDll;
    uintptr_t size = (uintptr_t)mInfo.SizeOfImage;
    uintptr_t end = start + size;

    size_t patternLen = strlen(mask);

    for (uintptr_t i = start; i < end - patternLen; i++) {
        bool found = true;
        for (size_t j = 0; j < patternLen; j++) {
            if (mask[j] != '?' && (unsigned char)pattern[j] != *(unsigned char*)(i + j)) {
                found = false;
                break;
            }
        }
        if (found) return i;
    }
    return 0;
}

uintptr_t FindString(const std::string& str) {
    HMODULE hModule = GetModuleHandleA(NULL);
    if (!hModule) return 0;

    MODULEINFO mInfo;
    if (!GetModuleInformation(GetCurrentProcess(), hModule, &mInfo, sizeof(mInfo))) return 0;
    
    uintptr_t start = (uintptr_t)mInfo.lpBaseOfDll;
    uintptr_t size = (uintptr_t)mInfo.SizeOfImage;
    uintptr_t end = start + size;

    const char* s = str.c_str();
    size_t len = str.length();

    for (uintptr_t i = start; i < end - len; i++) {
        if (memcmp((void*)i, s, len) == 0) return i;
    }
    return 0;
}

// --- Godot 4 String Structure (Simplified) ---
struct GodotString {
    void* _cowdata = nullptr;
};

// --- Godot Engine Types ---
typedef bool (*LoadResourcePackFunc)(void* self, const GodotString* p_pack, bool p_replace_files, int p_offset);
typedef void (*StringConstructFunc)(GodotString* self, const char* p_str);
typedef void (*StringDestructFunc)(GodotString* self);

LoadResourcePackFunc g_load_resource_pack = nullptr;
StringConstructFunc g_string_construct = nullptr;
StringDestructFunc g_string_destruct = nullptr;
void** g_project_settings_singleton_ptr = nullptr;

void LogHex(const std::string& label, uintptr_t addr, size_t len) {
    std::ofstream log("dse_loader.log", std::ios::app);
    log << "[" << GetTickCount() << "] " << label << " at " << addr << ": ";
    unsigned char* p = (unsigned char*)addr;
    for (size_t i = 0; i < len; i++) {
        char buf[4];
        sprintf_s(buf, "%02X ", p[i]);
        log << buf;
    }
    log << std::endl;
}

bool FindGodotFunctions() {
    static bool ps_found = false;
    static bool lrp_found = false;
    static bool sc_found = false;
    static bool msg_logged = false;

    HMODULE hModule = GetModuleHandleA(NULL);
    MODULEINFO mInfo;
    GetModuleInformation(GetCurrentProcess(), hModule, &mInfo, sizeof(mInfo));
    uintptr_t start = (uintptr_t)mInfo.lpBaseOfDll;
    uintptr_t end = start + (uintptr_t)mInfo.SizeOfImage;

    // 1. Find ProjectSettings::singleton pointer
    if (!g_project_settings_singleton_ptr) {
        const char* ps_patterns[] = {
            "\x48\x8B\x05\x00\x00\x00\x00\x48\x85\xC0\x74\x00\xC3", // Pattern A
            "\x48\x8B\x05\x00\x00\x00\x00\xC3",                    // Pattern B
            "\x48\x8B\x05\x00\x00\x00\x00\x48\x85\xC0\x75",         // Pattern C
            "\x48\x8B\x0D\x00\x00\x00\x00\x48\x85\xC9\x74"          // Pattern D (mov rcx, [ProjectSettings::singleton])
        };
        const char* ps_masks[] = { "xxx????xxxx?x", "xxx????x", "xxx????xxxx", "xxx????xxxx" };
        
        for (int i = 0; i < 4; i++) {
            uintptr_t get_ps = FindPattern(ps_patterns[i], ps_masks[i]);
            if (get_ps) {
                int32_t offset = *(int32_t*)(get_ps + 3);
                g_project_settings_singleton_ptr = (void**)(get_ps + 7 + offset);
                break;
            }
        }
        
        if (g_project_settings_singleton_ptr && !ps_found) {
            LogMessage("[DSE] Found ProjectSettings::singleton pointer at " + std::to_string((uintptr_t)g_project_settings_singleton_ptr));
            ps_found = true;
        }
    }

    // 2. Find load_resource_pack address
    if (!g_load_resource_pack) {
        const char* lrp_patterns[] = {
            "\x48\x89\x5C\x24\x08\x48\x89\x6C\x24\x10\x48\x89\x74\x24\x18\x57\x48\x83\xEC\x30\x48\x8B\xE9\x48\x8B\xDA",
            "\x48\x8B\xC4\x48\x89\x58\x08\x48\x89\x68\x10\x48\x89\x70\x18\x48\x89\x78\x20\x41\x56\x48\x83\xEC\x30",
            "\x48\x89\x5C\x24\x08\x48\x89\x6C\x24\x10\x48\x89\x74\x24\x18\x57\x48\x83\xEC",
            "\x40\x53\x48\x83\xEC\x30\x48\x8B\xD9\x48\x8B\xFA\x48\x8B\x89",
            "\x48\x89\x5C\x24\x08\x48\x89\x74\x24\x10\x57\x48\x83\xEC\x30\x48\x8B\xF1"
        };
        const char* lrp_masks[] = { "xxxxxxxxxxxxxxxxxxxxxxxxx", "xxxxxxxxxxxxxxxxxxxxxxxxx", "xxxxxxxxxxxxxxxxxx", "xxxxxxxxxxxxxxx", "xxxxxxxxxxxxxxxx" };

        for (int i = 0; i < 5; i++) {
            g_load_resource_pack = (LoadResourcePackFunc)FindPattern(lrp_patterns[i], lrp_masks[i]);
            if (g_load_resource_pack) break;
        }
        
        if (!g_load_resource_pack) {
            const char* names[] = { "load_resource_pack", "_load_resource_pack" };
            for (const char* name : names) {
                uintptr_t str_addr = FindString(name);
                if (str_addr) {
                    static std::string last_logged_str = "";
                    if (last_logged_str != name) {
                        LogMessage("[DSE] Found string '" + std::string(name) + "' at " + std::to_string(str_addr));
                        last_logged_str = name;
                    }

                    for (uintptr_t i = start; i < end - 7; i++) {
                        if (*(unsigned char*)i == 0x48 && *(unsigned char*)(i + 1) == 0x8D) {
                             unsigned char reg = *(unsigned char*)(i + 2);
                             if (reg == 0x15 || reg == 0x0D || reg == 0x05 || reg == 0x1D) { // rdx, rcx, r8, r9
                                int32_t offset = *(int32_t*)(i + 3);
                                if (i + 7 + offset == str_addr) {
                                    LogMessage("[DSE] Found LEA reference to '" + std::string(name) + "' at " + std::to_string(i));
                                    
                                    for (uintptr_t j = i - 512; j < i + 512; j++) {
                                        if (j < start || j > end - 7) continue;
                                        if (*(unsigned char*)j == 0x48 && *(unsigned char*)(j + 1) == 0x8D) {
                                            uintptr_t candidate = j + 7 + *(int32_t*)(j + 3);
                                            if (candidate > start && candidate < end && candidate != str_addr) {
                                                if (*(unsigned char*)candidate == 0x48 && (*(unsigned char*)(candidate + 1) == 0x89 || *(unsigned char*)(candidate + 1) == 0x83 || *(unsigned char*)(candidate+1) == 0x8B || *(unsigned char*)(candidate+1) == 0x40)) {
                                                    g_load_resource_pack = (LoadResourcePackFunc)candidate;
                                                    LogMessage("[DSE] String-Ref: Found load_resource_pack candidate at " + std::to_string(candidate));
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                             }
                        }
                        if (g_load_resource_pack) break;
                    }
                }
                if (g_load_resource_pack) break;
            }
        }

        if (g_load_resource_pack && !lrp_found) {
            LogMessage("[DSE] SUCCESS: Found load_resource_pack at " + std::to_string((uintptr_t)g_load_resource_pack));
            LogHex("[DSE] LRP Prologue", (uintptr_t)g_load_resource_pack, 16);
            lrp_found = true;
        }
    }
    
    // 3. Find String(const char*) constructor
    if (!g_string_construct) {
        const char* sc_patterns[] = {
            "\x48\x89\x5C\x24\x08\x57\x48\x83\xEC\x20\x48\x8B\xDA\x48\x8B\xF9\xE8",
            "\x40\x53\x48\x83\xEC\x20\x48\x8B\xD9\x48\x8B\xFA\xE8",
            "\x48\x89\x5C\x24\x08\x48\x89\x74\x24\x10\x57\x48\x83\xEC\x20",
            "\x48\x89\x5C\x24\x08\x57\x48\x83\xEC\x20\x48\x8B\xF1\x48\x8B\xDA",
            "\x40\x53\x48\x83\xEC\x20\x48\x8B\xDA\x48\x8B\xD9\xE8",
            "\x48\x89\x5C\x24\x08\x57\x48\x83\xEC\x20\x48\x8B\xF9\x48\x8B\xD1"
        };
        const char* sc_masks[] = { "xxxxxxxxxxxxxxxx", "xxxxxxxxxxxxx", "xxxxxxxxxxxxxxx", "xxxxxxxxxxxxxxxx", "xxxxxxxxxxxx", "xxxxxxxxxxxxxxxx" };

        for (int i = 0; i < 6; i++) {
            g_string_construct = (StringConstructFunc)FindPattern(sc_patterns[i], sc_masks[i]);
            if (g_string_construct) break;
        }

        if (!g_string_construct) {
            uintptr_t nul_str = FindString("NUL character");
            if (nul_str) {
                LogMessage("[DSE] Found 'NUL character' string at " + std::to_string(nul_str));
                for (uintptr_t i = start; i < end - 7; i++) {
                    if (*(unsigned char*)i == 0x48 && *(unsigned char*)(i + 1) == 0x8D && (*(unsigned char*)(i + 2) == 0x15 || *(unsigned char*)(i+2) == 0x0D)) {
                        int32_t offset = *(int32_t*)(i + 3);
                        if (i + 7 + offset == nul_str) {
                            LogMessage("[DSE] Found reference to 'NUL character' at " + std::to_string(i));
                            for (uintptr_t j = i - 512; j < i; j++) {
                                if (*(unsigned char*)j == 0x48 && *(unsigned char*)(j+1) == 0x89 && *(unsigned char*)(j+2) == 0x5C) {
                                    g_string_construct = (StringConstructFunc)j;
                                    LogMessage("[DSE] String-Ref: Found string_construct candidate at " + std::to_string(j));
                                    break;
                                }
                                if (*(unsigned char*)j == 0x40 && *(unsigned char*)(j+1) == 0x53 && *(unsigned char*)(j+2) == 0x48) {
                                    g_string_construct = (StringConstructFunc)j;
                                    LogMessage("[DSE] String-Ref: Found string_construct candidate (alt) at " + std::to_string(j));
                                    break;
                                }
                            }
                        }
                    }
                    if (g_string_construct) break;
                }
            }
        }

        if (g_string_construct && !sc_found) {
            LogMessage("[DSE] SUCCESS: Found string_construct at " + std::to_string((uintptr_t)g_string_construct));
            LogHex("[DSE] SC Prologue", (uintptr_t)g_string_construct, 16);
            sc_found = true;
        }
    }

    bool all_found = (g_load_resource_pack != nullptr && g_project_settings_singleton_ptr != nullptr && g_string_construct != nullptr);
    if (!all_found && !msg_logged) {
        LogMessage("[DSE] Functions missing: PS=" + std::to_string(ps_found) + " LRP=" + std::to_string(lrp_found) + " SC=" + std::to_string(sc_found));
        msg_logged = true;
    }
    return all_found;
}

void LoadPck(const std::string& path) {
    if (!g_load_resource_pack || !*g_project_settings_singleton_ptr) return;

    GodotString g_path;
    g_string_construct(&g_path, path.c_str());
    
    LogMessage("[DSE] Calling load_resource_pack for: " + path);
    bool success = g_load_resource_pack(*g_project_settings_singleton_ptr, &g_path, true, 0);
    
    LogMessage(success ? "[DSE]   Success!" : "[DSE]   Failed!");
    
    // String destructor usually just clears cowdata if refcount hits 0
    // Simplified for now, might leak if not careful but we only load a few pcks.
}

void LoadMods() {
    LogMessage("[DSE] Starting mod loading sequence...");

    // Note: FindGodotFunctions was already called in InitializationThread
    
    char exe_path[MAX_PATH];
    GetModuleFileNameA(NULL, exe_path, MAX_PATH);
    fs::path exe_dir = fs::path(exe_path).parent_path();

    // 1. Load DSECore.pck
    fs::path core_pck = exe_dir / "DSECore.pck";
    if (fs::exists(core_pck)) {
        LoadPck(core_pck.string());
    } else {
        LogMessage("[DSE] WARNING: DSECore.pck not found!");
    }

    // 2. Load mods from mods folder
    fs::path mods_dir = exe_dir / "mods";
    if (fs::exists(mods_dir)) {
        for (const auto& entry : fs::directory_iterator(mods_dir)) {
            if (entry.path().extension() == ".pck") {
                LoadPck(entry.path().string());
            }
        }
    }

    LogMessage("[DSE] Mod loading sequence finished.");
}

DWORD WINAPI InitializationThread(LPVOID lpParam) {
    LogMessage("[DSE] Waiting for engine initialization...");

    // Poll aggressively for functions and singleton
    int attempts = 0;
    while (attempts < 200) {
        if (FindGodotFunctions()) {
            // Wait for ProjectSettings::singleton to be non-null
            if (*g_project_settings_singleton_ptr != nullptr) {
                LogMessage("[DSE] Engine ready! Loading mods...");
                LoadMods();
                return 0;
            }
        }
        
        Sleep(100);
        attempts++;
    }

    LogMessage("[DSE] ERROR: Timed out waiting for engine or failed to find functions.");
    return 0;
}

// --- Main Entry ---
BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved) {
    switch (reason) {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        std::ofstream("dse_loader.log", std::ios::trunc).close();
        LogMessage("[DSE] DSELoader attached to process.");

        // Godot 4 initialization is complex.
        // We'll create a thread to wait for the engine to be ready.
        CreateThread(NULL, 0, InitializationThread, NULL, 0, NULL);
        break;
    }
    return TRUE;
}
