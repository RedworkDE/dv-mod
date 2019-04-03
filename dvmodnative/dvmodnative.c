#include "windows.h"
#include "urlmon.h"
#include "intrin.h"


LPCSTR regPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 588030";
LPCSTR regName = "InstallLocation";

HANDLE hHeap;

#define origin "dvmod://mod/couplers-overhaul"
//"Put mod uri here, space for up to 127 bytes ascii PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD PAD P"
#define memalloc(size) HeapAlloc(hHeap, HEAP_GENERATE_EXCEPTIONS, size)
#define memcalloc(size) HeapAlloc(hHeap, HEAP_ZERO_MEMORY, size)
#define memcpy(dst, src, size) __movsb(dst, src, size)
#define makepath(path, subpath) __movsb(path, subpath, sizeof(subpath))
#define download(path) {makepath(subPath, path); urlDownloadToFile(NULL,"http://dvmod.goip.de/" path, fullPath, 0, NULL);}

// note: this leaks memory and handles and is generally unsafe but ¯\_(ツ)_/¯
int mainCRTStartup()
{
	hHeap = GetProcessHeap();

	HMODULE urlmon = LoadLibraryA("urlmon.dll");
	HRESULT (*urlDownloadToFile)(LPUNKNOWN, LPCSTR, LPCSTR, DWORD, LPBINDSTATUSCALLBACK) = (void*)GetProcAddress(urlmon, "URLDownloadToFileA");

	const LONG fullPathLen = 1<<14;
	char* fullPath = memcalloc(fullPathLen);
	char* fullPathCpy = memcalloc(fullPathLen);
	char* subPath = fullPath;
	char* subPathCpy = fullPath;

	DWORD filledLen = fullPathLen;
	LSTATUS status = RegGetValueA(HKEY_LOCAL_MACHINE, regPath, regName, RRF_RT_REG_SZ | RRF_SUBKEY_WOW6432KEY, NULL, fullPath, &filledLen);
	if (status != ERROR_SUCCESS)
	{
		filledLen = fullPathLen;
		status = RegGetValueA(HKEY_LOCAL_MACHINE, regPath, regName, RRF_RT_REG_SZ | RRF_SUBKEY_WOW6464KEY, NULL, fullPath, &filledLen);
	}
	if (status == ERROR_SUCCESS)
	{
		fullPath[filledLen - 1] = '\\';
		fullPath[filledLen] = 0;
		subPath = fullPath + filledLen;
		subPathCpy = fullPathCpy + filledLen;
	}
	else
	{
		MessageBoxA(NULL, "unable to find dv", NULL, 0);
		return -1;
	}

	memcpy(fullPathCpy, fullPath, fullPathLen);

	makepath(subPath, "dvmod.exe");
	if (CreateFileA(fullPath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL) == INVALID_HANDLE_VALUE) {
		makepath(subPath, "doorstop_config.ini");
		makepath(subPathCpy, "doorstop_config.ini.old");
		MoveFileA(fullPath, fullPathCpy);

		download("dvmod.exe");
		download("doorstop_config.ini");
		download("version.dll");
	}

	makepath(subPath, "DerailValley.exe");
	makepath(subPathCpy, "");
	
	STARTUPINFO si = {sizeof(si),0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,};
	PROCESS_INFORMATION pi= {0,0,0,0};
	if (CreateProcessA(fullPath, "progname-dummy dvmod-handler install " origin, NULL, NULL, FALSE, 0, NULL, fullPathCpy, &si, &pi) == 0) {
		int error = GetLastError();
		MessageBoxA(NULL, "failed to execute", NULL, 0);
		LPSTR messageBuffer = NULL;
		size_t size = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
			NULL, error, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&messageBuffer, 0, NULL);
		MessageBoxA(NULL, messageBuffer, NULL, 0);
	}
		
	return 0;
}
