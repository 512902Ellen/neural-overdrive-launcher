namespace CodexBootAnimation;

internal sealed class SettingsText(UiLanguage language)
{
    public string this[string key] => language == UiLanguage.SimplifiedChinese ? Copy[key].Chinese : Copy[key].English;

    private static readonly Dictionary<string, (string English, string Chinese)> Copy = new()
    {
        ["Title"] = ("Launcher Settings", "启动器设置"),
        ["Minimize"] = ("Minimize", "最小化"),
        ["Close"] = ("Close", "关闭"),
        ["Theme"] = ("BUILT-IN THEME", "内置主题"),
        ["ThemeDescription"] = ("A neural core wakes, gathers energy, and hands off to the desktop client.", "神经核心逐步唤醒、汇聚能量，随后交接至桌面客户端。"),
        ["ThemePreviewUnavailable"] = ("Theme preview unavailable", "主题预览图不可用"),
        ["Mode"] = ("Startup mode", "启动模式"),
        ["Quick"] = ("Quick", "快速模式"),
        ["QuickTime"] = ("5 seconds", "5 秒"),
        ["Full"] = ("Cinematic", "电影模式"),
        ["FullTime"] = ("9 seconds", "9 秒"),
        ["Direct"] = ("Direct", "直接启动"),
        ["DirectTime"] = ("No animation", "无动画"),
        ["Preferences"] = ("Preferences", "偏好设置"),
        ["Language"] = ("Interface language", "界面语言"),
        ["Motion"] = ("Respect system reduced motion", "跟随系统减少动态效果"),
        ["Shortcuts"] = ("Desktop shortcuts", "桌面快捷方式"),
        ["CreateLaunch"] = ("Add Launch Codex", "添加 Codex 启动入口"),
        ["CreateSettings"] = ("Add Launcher Settings", "添加设置入口"),
        ["Remove"] = ("Remove launcher shortcuts", "移除启动器快捷方式"),
        ["Preview"] = ("Preview Animation", "预览动画"),
        ["PreviewHint"] = ("Play without opening Codex. Direct mode previews Quick.", "仅播放动画。直接启动模式下预览 5 秒动画。"),
        ["Launch"] = ("Launch Codex", "启动 Codex"),
        ["LaunchHint"] = ("Launch with the preferences shown here.", "使用当前显示的偏好启动。"),
        ["Save"] = ("Save Settings", "保存设置"),
        ["Reset"] = ("Reset to Defaults", "恢复默认设置"),
        ["Unofficial"] = ("Independent launcher · Unofficial project", "独立启动器 · 非官方项目"),
        ["Dirty"] = ("Unsaved changes", "有未保存的更改"),
        ["Recovered"] = ("Defaults loaded. Save settings to replace the unreadable or unsupported file.", "已加载默认设置。保存设置可替换无法读取或不受支持的文件。"),
        ["Saved"] = ("Settings saved.", "设置已保存。"),
        ["ResetDraft"] = ("Defaults restored. Save to keep them.", "已恢复默认值。保存后生效。"),
        ["SaveError"] = ("Could not save. Move the launcher to a writable folder and try again.", "无法保存。请将启动器移到可写目录后重试。"),
        ["LinkCreated"] = ("Desktop shortcut created.", "已创建桌面快捷方式。"),
        ["LinkExists"] = ("This desktop shortcut already exists.", "此桌面快捷方式已存在。"),
        ["LinkOld"] = ("The shortcut points to an older location. Remove launcher shortcuts, then add it again.", "快捷方式指向旧位置。请先移除启动器快捷方式，再重新添加。"),
        ["LinkConflict"] = ("A different desktop item uses this name. It was kept.", "桌面已有同名项目，已保留该项目。"),
        ["LinksRemoved"] = ("Launcher shortcuts removed.", "已移除启动器快捷方式。"),
        ["LinksPartial"] = ("Launcher shortcuts removed. Other desktop items were kept.", "已移除启动器快捷方式，其他桌面项目已保留。"),
        ["LinksMissing"] = ("No launcher shortcuts found on this desktop.", "此桌面上没有启动器快捷方式。"),
        ["LinkError"] = ("Could not update desktop shortcuts. Check that your desktop folder is available.", "无法更新快捷方式。请检查桌面文件夹是否可用。"),
        ["StartingPreview"] = ("Opening animation preview…", "正在打开动画预览…"),
        ["StartingClient"] = ("Launching Codex…", "正在启动 Codex…"),
        ["LaunchError"] = ("Could not start the launcher.", "无法启动启动器。"),
        ["PreviewError"] = ("Preview unavailable. Check the theme files and Evergreen WebView2 Runtime.", "无法预览。请检查主题文件及 Evergreen WebView2 Runtime。"),
        ["ClientError"] = ("Windows could not open ChatGPT. Check its entry in Start.", "Windows 无法打开 ChatGPT。请检查「开始」菜单中的入口。"),
        ["ClientTimeout"] = ("No stable ChatGPT window was found. Check the taskbar or Start.", "未找到稳定的 ChatGPT 窗口。请检查任务栏或「开始」菜单。"),
        ["ClientMissing"] = ("The supported ChatGPT desktop app is unavailable for this Windows user. Check its entry in Start.", "当前 Windows 用户没有可用的受支持 ChatGPT 桌面应用。请检查「开始」菜单中的入口。"),
        ["Usage"] = ("Options: --settings, --quick, --full, --direct, --preview, --diagnostics, --help.\nLanguage: --language en or --language zh-CN.\nMotion: --respect-reduced-motion or --ignore-reduced-motion.\nUse --preview with --quick or --full for animation only.",
            "选项：--settings、--quick、--full、--direct、--preview、--diagnostics、--help。\n语言：--language en 或 --language zh-CN。\n动态效果：--respect-reduced-motion 或 --ignore-reduced-motion。\n与 --quick 或 --full 一起使用 --preview 可仅播放动画。")
    };
}
