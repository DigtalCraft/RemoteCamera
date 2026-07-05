namespace RemoteCamera
{
    /// <summary>
    /// Windows ログオン時にアプリを起動するための登録を行う。
    /// </summary>
    internal static class StartupRegistration
    {
        private const string ShortcutName = "RemoteCamera.lnk";

        /// <summary>
        /// 現在のユーザーに対して自動起動を有効化する。
        /// </summary>
        public static void EnsureEnabled()
        {
            try
            {
                var executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return;
                }

                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (string.IsNullOrWhiteSpace(startupFolder))
                {
                    return;
                }

                var shortcutPath = Path.Combine(startupFolder, ShortcutName);
                DeleteDuplicateShortcuts(startupFolder, shortcutPath);
                CreateOrUpdateShortcut(shortcutPath, executablePath);
            }
            catch
            {
                // 自動起動の登録に失敗しても、アプリ本体の起動は継続する。
            }
        }

        /// <summary>
        /// スタートアップ用のショートカットを作成する。
        /// </summary>
        /// <param name="shortcutPath">ショートカットの保存先。</param>
        /// <param name="targetPath">起動対象の実行ファイル。</param>
        private static void CreateOrUpdateShortcut(string shortcutPath, string targetPath)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);

            try
            {
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                shortcut.Description = "RemoteCamera";
                shortcut.IconLocation = targetPath;
                shortcut.Save();
            }
            finally
            {
                try
                {
                    if (shortcut is not null)
                    {
                        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                    }
                }
                catch
                {
                    // 破棄時は続行する。
                }

                try
                {
                    if (shell is not null)
                    {
                        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                    }
                }
                catch
                {
                    // 破棄時は続行する。
                }
            }
        }

        /// <summary>
        /// 同名系の古いスタートアップショートカットを削除する。
        /// </summary>
        /// <param name="startupFolder">スタートアップフォルダー。</param>
        /// <param name="currentShortcutPath">現在利用するショートカット。</param>
        private static void DeleteDuplicateShortcuts(string startupFolder, string currentShortcutPath)
        {
            if (!Directory.Exists(startupFolder))
            {
                return;
            }

            foreach (var shortcutPath in Directory.EnumerateFiles(startupFolder, "RemoteCamera*.lnk"))
            {
                if (shortcutPath.Equals(currentShortcutPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(shortcutPath);
                }
                catch
                {
                    // 古いショートカットの削除に失敗しても、現在の登録は続行する。
                }
            }
        }
    }
}
