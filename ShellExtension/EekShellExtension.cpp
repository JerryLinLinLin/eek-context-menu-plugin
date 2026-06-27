#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <strsafe.h>

#include <new>
#include <string>

namespace
{
// {4B4490A7-D5BE-48CB-B133-3D87E6E2B9EF}
const CLSID CLSID_EekExplorerCommand =
{ 0x4b4490a7, 0xd5be, 0x48cb, { 0xb1, 0x33, 0x3d, 0x87, 0xe6, 0xe2, 0xb9, 0xef } };

long g_objects = 0;
long g_locks = 0;
HMODULE g_module = nullptr;

std::wstring JoinPath(std::wstring left, const wchar_t* right)
{
    if (!left.empty() && left.back() != L'\\' && left.back() != L'/')
    {
        left += L'\\';
    }

    left += right;
    return left;
}

std::wstring ParentPath(const std::wstring& path)
{
    const auto slash = path.find_last_of(L"\\/");
    return slash == std::wstring::npos ? std::wstring{} : path.substr(0, slash);
}

bool FileExists(const std::wstring& path)
{
    const DWORD attrs = GetFileAttributesW(path.c_str());
    return attrs != INVALID_FILE_ATTRIBUTES && (attrs & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

bool FileSystemItemExists(const std::wstring& path)
{
    return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
}

std::wstring QuoteArg(const std::wstring& value)
{
    std::wstring quoted = L"\"";
    size_t backslashes = 0;

    for (const wchar_t character : value)
    {
        if (character == L'\\')
        {
            ++backslashes;
            continue;
        }

        if (character == L'"')
        {
            quoted.append(backslashes * 2 + 1, L'\\');
            quoted += character;
            backslashes = 0;
            continue;
        }

        quoted.append(backslashes, L'\\');
        backslashes = 0;
        quoted += character;
    }

    quoted.append(backslashes * 2, L'\\');
    quoted += L'"';
    return quoted;
}

std::wstring ReadStringSetting(const wchar_t* name, const wchar_t* fallback)
{
    DWORD bytes = 0;
    LSTATUS status = RegGetValueW(
        HKEY_CURRENT_USER,
        L"Software\\EekContextMenu",
        name,
        RRF_RT_REG_SZ,
        nullptr,
        nullptr,
        &bytes);

    if (status != ERROR_SUCCESS || bytes <= sizeof(wchar_t))
    {
        return fallback;
    }

    std::wstring value(bytes / sizeof(wchar_t), L'\0');
    status = RegGetValueW(
        HKEY_CURRENT_USER,
        L"Software\\EekContextMenu",
        name,
        RRF_RT_REG_SZ,
        nullptr,
        value.data(),
        &bytes);

    if (status != ERROR_SUCCESS)
    {
        return fallback;
    }

    value.resize(wcslen(value.c_str()));
    return value.empty() ? fallback : value;
}

bool IsEnabled()
{
    DWORD enabled = 0;
    DWORD bytes = sizeof(enabled);
    const LSTATUS status = RegGetValueW(
        HKEY_CURRENT_USER,
        L"Software\\EekContextMenu",
        L"Enabled",
        RRF_RT_REG_DWORD,
        nullptr,
        &enabled,
        &bytes);

    return status == ERROR_SUCCESS && enabled != 0;
}

std::wstring GetEekRoot()
{
    return ReadStringSetting(L"EekRoot", L"C:\\EEK");
}

std::wstring GetScannerPath()
{
    return JoinPath(JoinPath(GetEekRoot(), L"bin64"), L"a2cmd.exe");
}

HRESULT GetSingleItemPath(IShellItemArray* items, std::wstring& itemPath)
{
    if (items == nullptr)
    {
        return E_INVALIDARG;
    }

    DWORD count = 0;
    HRESULT hr = items->GetCount(&count);
    if (FAILED(hr))
    {
        return hr;
    }

    if (count != 1)
    {
        return E_INVALIDARG;
    }

    IShellItem* item = nullptr;
    hr = items->GetItemAt(0, &item);
    if (FAILED(hr))
    {
        return hr;
    }

    PWSTR path = nullptr;
    hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
    if (SUCCEEDED(hr))
    {
        itemPath = path;
        CoTaskMemFree(path);
    }

    item->Release();
    return hr;
}

HRESULT LaunchScan(const std::wstring& target)
{
    const std::wstring scanner = GetScannerPath();
    if (!FileExists(scanner))
    {
        MessageBoxW(nullptr, scanner.c_str(), L"EEK scanner was not found", MB_OK | MB_ICONERROR);
        return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
    }

    wchar_t modulePath[MAX_PATH]{};
    if (GetModuleFileNameW(g_module, modulePath, ARRAYSIZE(modulePath)) == 0)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    const std::wstring appFolder = ParentPath(modulePath);
    const std::wstring appPath = JoinPath(appFolder, L"EekContextMenu.exe");
    if (!FileExists(appPath))
    {
        MessageBoxW(nullptr, appPath.c_str(), L"EEK context menu app was not found", MB_OK | MB_ICONERROR);
        return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
    }

    const std::wstring parameters = L"--scan " + QuoteArg(target);

    SHELLEXECUTEINFOW executeInfo{ sizeof(executeInfo) };
    executeInfo.fMask = SEE_MASK_NOASYNC;
    executeInfo.lpVerb = L"runas";
    executeInfo.lpFile = appPath.c_str();
    executeInfo.lpParameters = parameters.c_str();
    executeInfo.lpDirectory = appFolder.c_str();
    executeInfo.nShow = SW_SHOWNORMAL;

    if (!ShellExecuteExW(&executeInfo))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    return S_OK;
}

class EekExplorerCommand final : public IExplorerCommand
{
public:
    EekExplorerCommand() : _refs(1)
    {
        InterlockedIncrement(&g_objects);
    }

    ~EekExplorerCommand()
    {
        InterlockedDecrement(&g_objects);
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** object) override
    {
        if (object == nullptr)
        {
            return E_POINTER;
        }

        *object = nullptr;
        if (riid == IID_IUnknown || riid == IID_IExplorerCommand)
        {
            *object = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_refs);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG refs = InterlockedDecrement(&_refs);
        if (refs == 0)
        {
            delete this;
        }

        return refs;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* title) override
    {
        return title == nullptr ? E_POINTER : SHStrDupW(L"Scan with EEK", title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        if (icon == nullptr)
        {
            return E_POINTER;
        }

        const std::wstring iconPath = GetScannerPath() + L",0";
        return SHStrDupW(iconPath.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* tooltip) override
    {
        return tooltip == nullptr ? E_POINTER : SHStrDupW(L"Scan this item with Emsisoft Emergency Kit.", tooltip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* commandName) override
    {
        if (commandName == nullptr)
        {
            return E_POINTER;
        }

        *commandName = CLSID_EekExplorerCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        if (state == nullptr)
        {
            return E_POINTER;
        }

        if (!IsEnabled())
        {
            *state = ECS_HIDDEN;
            return S_OK;
        }

        DWORD count = 0;
        if (items == nullptr || FAILED(items->GetCount(&count)) || count != 1)
        {
            *state = ECS_DISABLED;
            return S_OK;
        }

        std::wstring target;
        if (FAILED(GetSingleItemPath(items, target)) || !FileSystemItemExists(target))
        {
            *state = ECS_DISABLED;
            return S_OK;
        }

        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        if (!IsEnabled())
        {
            return S_OK;
        }

        std::wstring target;
        const HRESULT hr = GetSingleItemPath(items, target);
        return FAILED(hr) ? hr : LaunchScan(target);
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (flags == nullptr)
        {
            return E_POINTER;
        }

        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) override
    {
        if (enumCommands == nullptr)
        {
            return E_POINTER;
        }

        *enumCommands = nullptr;
        return E_NOTIMPL;
    }

private:
    long _refs;
};

class ClassFactory final : public IClassFactory
{
public:
    ClassFactory() : _refs(1)
    {
        InterlockedIncrement(&g_objects);
    }

    ~ClassFactory()
    {
        InterlockedDecrement(&g_objects);
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** object) override
    {
        if (object == nullptr)
        {
            return E_POINTER;
        }

        *object = nullptr;
        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *object = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_refs);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG refs = InterlockedDecrement(&_refs);
        if (refs == 0)
        {
            delete this;
        }

        return refs;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** object) override
    {
        if (outer != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        EekExplorerCommand* command = new (std::nothrow) EekExplorerCommand();
        if (command == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        const HRESULT hr = command->QueryInterface(riid, object);
        command->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
        {
            InterlockedIncrement(&g_locks);
        }
        else
        {
            InterlockedDecrement(&g_locks);
        }

        return S_OK;
    }

private:
    long _refs;
};
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return g_objects == 0 && g_locks == 0 ? S_OK : S_FALSE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID riid, void** object)
{
    if (object == nullptr)
    {
        return E_POINTER;
    }

    *object = nullptr;
    if (clsid != CLSID_EekExplorerCommand)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    ClassFactory* factory = new (std::nothrow) ClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    const HRESULT hr = factory->QueryInterface(riid, object);
    factory->Release();
    return hr;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
    }

    return TRUE;
}
